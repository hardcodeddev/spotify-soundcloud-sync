using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlaylistSync.Core;
using PlaylistSync.Infrastructure.Persistence;

namespace PlaylistSync.Api.Controllers;

[ApiController]
[Route("sync")]
public class SyncController(PlaylistSyncDbContext dbContext, ISyncService syncService) : ControllerBase
{
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
    {
        var user = await GetOrCreateUserAsync(cancellationToken);

        var profile = await dbContext.SyncProfiles
            .Include(x => x.PlaylistMappings)
            .SingleOrDefaultAsync(x => x.UserAccountId == user.Id, cancellationToken);

        if (profile is null)
        {
            profile = new SyncProfile
            {
                UserAccountId = user.Id
            };
            dbContext.SyncProfiles.Add(profile);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return Ok(ToProfileResponse(profile));
    }

    [HttpPut("profile")]
    public async Task<IActionResult> PutProfile([FromBody] UpsertSyncProfileRequest request, CancellationToken cancellationToken)
    {
        var user = await GetOrCreateUserAsync(cancellationToken);

        var profile = await dbContext.SyncProfiles
            .Include(x => x.PlaylistMappings)
            .SingleOrDefaultAsync(x => x.UserAccountId == user.Id, cancellationToken);

        if (profile is null)
        {
            profile = new SyncProfile { UserAccountId = user.Id };
            dbContext.SyncProfiles.Add(profile);
        }

        profile.Direction = request.Direction;
        profile.LikesBehavior = request.LikesBehavior;
        profile.UpdatedAt = DateTimeOffset.UtcNow;

        dbContext.PlaylistMappings.RemoveRange(profile.PlaylistMappings);
        profile.PlaylistMappings = (request.PlaylistMappings ?? Array.Empty<PlaylistMappingRequest>()).Select(x => new PlaylistMapping
        {
            SourceProvider = x.SourceProvider,
            SourcePlaylistId = x.SourcePlaylistId,
            TargetProvider = x.TargetProvider,
            TargetPlaylistId = x.TargetPlaylistId
        }).ToList();

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(ToProfileResponse(profile));
    }


    [HttpPost("run")]
    public Task<IActionResult> Run(CancellationToken cancellationToken) => RunNow(cancellationToken);

    [HttpPost("run-now")]
    public async Task<IActionResult> RunNow(CancellationToken cancellationToken)
    {
        var user = await GetOrCreateUserAsync(cancellationToken);
        var idempotencyKey = Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            idempotencyKey = Guid.NewGuid().ToString("N");
        }

        var existingJob = await dbContext.SyncJobs
            .Include(x => x.Runs)
            .SingleOrDefaultAsync(x => x.UserAccountId == user.Id && x.RequestedIdempotencyKey == idempotencyKey, cancellationToken);

        if (existingJob is not null)
        {
            var existingRun = existingJob.Runs.OrderByDescending(x => x.StartedAt).FirstOrDefault();
            return Ok(ToRunResponse(existingJob, existingRun));
        }

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
            await syncService.RunPlaylistSyncAsync(cancellationToken);
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
        return Accepted(ToRunResponse(job, run));
    }

    [HttpGet("runs")]
    public async Task<IActionResult> GetRuns(CancellationToken cancellationToken)
    {
        var user = await GetOrCreateUserAsync(cancellationToken);

        var runs = await dbContext.SyncRuns
            .AsNoTracking()
            .Include(x => x.SyncJob)
            .Where(x => x.SyncJob.UserAccountId == user.Id)
            .OrderByDescending(x => x.StartedAt)
            .Take(25)
            .Select(x => new
            {
                x.Id,
                x.SyncJobId,
                status = x.Status.ToString(),
                x.StartedAt,
                x.EndedAt,
                x.ImportedCount,
                x.ExportedCount,
                x.SkippedCount,
                x.Error,
                idempotencyKey = x.SyncJob.RequestedIdempotencyKey
            })
            .ToListAsync(cancellationToken);

        return Ok(runs);
    }

    private async Task<UserAccount> GetOrCreateUserAsync(CancellationToken cancellationToken)
    {
        var userId = Request.Headers["X-User-Id"].FirstOrDefault() ?? "demo-user";
        var user = await dbContext.UserAccounts.SingleOrDefaultAsync(x => x.ExternalUserId == userId, cancellationToken);
        if (user is not null)
        {
            return user;
        }

        user = new UserAccount { ExternalUserId = userId };
        dbContext.UserAccounts.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);
        return user;
    }

    private static object ToProfileResponse(SyncProfile profile) => new
    {
        profile.Id,
        direction = profile.Direction.ToString(),
        likesBehavior = profile.LikesBehavior.ToString(),
        profile.UpdatedAt,
        playlistMappings = profile.PlaylistMappings.Select(x => new
        {
            x.SourceProvider,
            x.SourcePlaylistId,
            x.TargetProvider,
            x.TargetPlaylistId
        })
    };

    private static object ToRunResponse(SyncJob job, SyncRun? run) => new
    {
        job.Id,
        idempotencyKey = job.RequestedIdempotencyKey,
        status = job.Status.ToString(),
        job.StartedAt,
        job.EndedAt,
        runId = run?.Id,
        runStatus = run?.Status.ToString(),
        run?.ImportedCount,
        run?.ExportedCount,
        run?.SkippedCount,
        error = run?.Error ?? job.Error
    };

    public sealed record UpsertSyncProfileRequest(
        SyncDirection Direction,
        LikesSyncBehavior LikesBehavior,
        IReadOnlyList<PlaylistMappingRequest>? PlaylistMappings);

    public sealed record PlaylistMappingRequest(
        string SourceProvider,
        string SourcePlaylistId,
        string TargetProvider,
        string TargetPlaylistId);
}
