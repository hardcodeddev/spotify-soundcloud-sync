using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlaylistSync.Api.Services;
using PlaylistSync.Core;
using PlaylistSync.Infrastructure.Persistence;

namespace PlaylistSync.Api.Controllers;

[ApiController]
[Route("sync")]
public class SyncController(
    PlaylistSyncDbContext dbContext,
    ISyncExecutionService syncExecutionService,
    ICronScheduleValidator cronScheduleValidator) : ControllerBase
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

    [HttpPut("schedule")]
    public async Task<IActionResult> PutSchedule([FromBody] UpdateSyncScheduleRequest request, CancellationToken cancellationToken)
    {
        var user = await GetOrCreateUserAsync(cancellationToken);
        var profile = await dbContext.SyncProfiles.SingleOrDefaultAsync(x => x.UserAccountId == user.Id, cancellationToken);
        if (profile is null)
        {
            profile = new SyncProfile { UserAccountId = user.Id };
            dbContext.SyncProfiles.Add(profile);
        }

        if (string.IsNullOrWhiteSpace(request.CronExpression))
        {
            profile.ScheduleEnabled = false;
            profile.ScheduleCron = null;
            profile.ScheduleTimeZone = "UTC";
            profile.UpdatedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            return Ok(new { enabled = false });
        }

        if (!cronScheduleValidator.TryValidate(request.CronExpression, out var normalizedCronExpression, out var cronError))
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [nameof(request.CronExpression)] = [cronError ?? "Invalid cron expression."]
            }));
        }

        var timeZoneId = string.IsNullOrWhiteSpace(request.TimeZoneId) ? "UTC" : request.TimeZoneId.Trim();
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [nameof(request.TimeZoneId)] = ["Unknown timezone."]
            }));
        }

        profile.ScheduleEnabled = true;
        profile.ScheduleCron = normalizedCronExpression;
        profile.ScheduleTimeZone = timeZoneId;
        profile.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            enabled = profile.ScheduleEnabled,
            cronExpression = request.CronExpression,
            normalizedCronExpression = profile.ScheduleCron,
            timeZoneId = profile.ScheduleTimeZone
        });
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

        var (job, run) = await syncExecutionService.ExecuteForUserAsync(user, idempotencyKey, cancellationToken);
        return Accepted(ToRunResponse(job, run));
    }


    [HttpGet("runs/latest")]
    public async Task<IActionResult> GetLatestRun(CancellationToken cancellationToken)
    {
        var user = await GetOrCreateUserAsync(cancellationToken);

        var latestRun = await dbContext.SyncRuns
            .AsNoTracking()
            .Include(x => x.SyncJob)
            .Where(x => x.SyncJob.UserAccountId == user.Id)
            .OrderByDescending(x => x.StartedAt)
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
            .FirstOrDefaultAsync(cancellationToken);

        return latestRun is null ? NotFound() : Ok(latestRun);
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
        var userId = ResolveUserId();
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

    private string ResolveUserId()
    {
        var userIdFromHeader = Request.Headers["X-User-Id"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(userIdFromHeader))
        {
            WriteUserCookie(userIdFromHeader);
            return userIdFromHeader;
        }

        var userIdFromCookie = Request.Cookies["playlist_sync_user"];
        if (!string.IsNullOrWhiteSpace(userIdFromCookie))
        {
            return userIdFromCookie;
        }

        var generatedUserId = $"user-{Guid.NewGuid():N}";
        WriteUserCookie(generatedUserId);
        return generatedUserId;
    }

    private void WriteUserCookie(string userId)
    {
        Response.Cookies.Append("playlist_sync_user", userId, new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Secure = Request.IsHttps,
            MaxAge = TimeSpan.FromDays(30)
        });
    }

    private static object ToProfileResponse(SyncProfile profile) => new
    {
        profile.Id,
        direction = profile.Direction.ToString(),
        likesBehavior = profile.LikesBehavior.ToString(),
        profile.UpdatedAt,
        schedule = new
        {
            enabled = profile.ScheduleEnabled,
            cronExpression = profile.ScheduleCron,
            timeZoneId = profile.ScheduleTimeZone
        },
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

    public sealed record UpdateSyncScheduleRequest(string? CronExpression, string? TimeZoneId);
}
