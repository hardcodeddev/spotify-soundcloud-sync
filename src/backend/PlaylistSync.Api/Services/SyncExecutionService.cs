using PlaylistSync.Core;
using PlaylistSync.Infrastructure.Persistence;
using PlaylistSync.Infrastructure.Providers;

namespace PlaylistSync.Api.Services;

public interface ISyncExecutionService
{
    Task<(SyncJob Job, SyncRun Run)> ExecuteForUserAsync(UserAccount user, string idempotencyKey, CancellationToken cancellationToken = default);
}

public sealed class SyncExecutionService(PlaylistSyncDbContext dbContext, ISyncService syncService) : ISyncExecutionService
{
    public async Task<(SyncJob Job, SyncRun Run)> ExecuteForUserAsync(UserAccount user, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var job = new SyncJob
        {
            UserAccountId = user.Id,
            RequestedIdempotencyKey = idempotencyKey,
            Status = SyncRunStatus.Running,
            StartedAt = now
        };

        var run = new SyncRun
        {
            SyncJob = job,
            Status = SyncRunStatus.Running,
            StartedAt = now
        };

        dbContext.SyncJobs.Add(job);
        dbContext.SyncRuns.Add(run);
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            await ExecuteWithRetryAsync(cancellationToken);
            run.Status = SyncRunStatus.Completed;
            run.EndedAt = DateTimeOffset.UtcNow;
            job.Status = SyncRunStatus.Completed;
            job.EndedAt = run.EndedAt;
        }
        catch (Exception ex)
        {
            run.Status = SyncRunStatus.Failed;
            run.EndedAt = DateTimeOffset.UtcNow;
            run.Error = ex.Message;
            job.Status = SyncRunStatus.Failed;
            job.EndedAt = run.EndedAt;
            job.Error = ex.Message;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return (job, run);
    }

    private async Task ExecuteWithRetryAsync(CancellationToken cancellationToken)
    {
        const int maxAttempts = 4;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await syncService.RunPlaylistSyncAsync(cancellationToken);
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts && IsTransient(ex))
            {
                var delay = ComputeBackoffDelay(attempt, ex);
                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    private static TimeSpan ComputeBackoffDelay(int attempt, Exception ex)
    {
        if (ex is ProviderApiException apiException && apiException.RetryAfter is not null)
        {
            return apiException.RetryAfter.Value;
        }

        var jitter = Random.Shared.Next(150, 900);
        var baseMs = 500 * Math.Pow(2, attempt - 1);
        return TimeSpan.FromMilliseconds(baseMs + jitter);
    }

    private static bool IsTransient(Exception ex)
        => ex is ProviderApiException apiException && apiException.IsTransient
           || ex is HttpRequestException
           || ex is TimeoutException
           || ex is TaskCanceledException;
}
