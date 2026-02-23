using Npgsql;
using Microsoft.EntityFrameworkCore;
using System.Data;
using PlaylistSync.Infrastructure.Persistence;
using Quartz;

namespace PlaylistSync.Api.Services;

public interface ISyncSchedulerService
{
    Task RegisterOrUpdateRecurringJobAsync(Guid userAccountId, string cronExpression, string timeZoneId, CancellationToken cancellationToken = default);
    Task RemoveRecurringJobAsync(Guid userAccountId, CancellationToken cancellationToken = default);
    Task RegisterAllSchedulesAsync(CancellationToken cancellationToken = default);
}

public sealed class SyncSchedulerService(
    ISchedulerFactory schedulerFactory,
    PlaylistSyncDbContext dbContext,
    ILogger<SyncSchedulerService> logger) : ISyncSchedulerService
{
    public async Task RegisterOrUpdateRecurringJobAsync(Guid userAccountId, string cronExpression, string timeZoneId, CancellationToken cancellationToken = default)
    {
        var scheduler = await schedulerFactory.GetScheduler(cancellationToken);
        var jobKey = JobKeyFor(userAccountId);
        var triggerKey = TriggerKeyFor(userAccountId);

        var jobDetail = JobBuilder.Create<ScheduledSyncJob>()
            .WithIdentity(jobKey)
            .UsingJobData("UserAccountId", userAccountId.ToString("N"))
            .StoreDurably()
            .Build();

        await scheduler.AddJob(jobDetail, true, true, cancellationToken);

        var trigger = TriggerBuilder.Create()
            .WithIdentity(triggerKey)
            .ForJob(jobKey)
            .WithCronSchedule(cronExpression, x => x.InTimeZone(TimeZoneInfo.FindSystemTimeZoneById(timeZoneId)))
            .Build();

        if (await scheduler.CheckExists(triggerKey, cancellationToken))
        {
            await scheduler.RescheduleJob(triggerKey, trigger, cancellationToken);
            return;
        }

        await scheduler.ScheduleJob(trigger, cancellationToken);
    }

    public async Task RemoveRecurringJobAsync(Guid userAccountId, CancellationToken cancellationToken = default)
    {
        var scheduler = await schedulerFactory.GetScheduler(cancellationToken);
        await scheduler.DeleteJob(JobKeyFor(userAccountId), cancellationToken);
    }

    public async Task RegisterAllSchedulesAsync(CancellationToken cancellationToken = default)
    {
        if (!await SyncProfilesTableExistsAsync(cancellationToken))
        {
            logger.LogWarning("Skipping schedule registration because table 'SyncProfiles' does not exist yet. If this is stale local state, run `docker compose down -v` and restart.");
            return;
        }

        var profiles = await dbContext.SyncProfiles
            .AsNoTracking()
            .Where(x => x.ScheduleEnabled && x.ScheduleCron != null)
            .Select(x => new { x.UserAccountId, x.ScheduleCron, x.ScheduleTimeZone })
            .ToListAsync(cancellationToken);

        foreach (var profile in profiles)
        {
            await RegisterOrUpdateRecurringJobAsync(profile.UserAccountId, profile.ScheduleCron!, profile.ScheduleTimeZone, cancellationToken);
        }
    }

    private async Task<bool> SyncProfilesTableExistsAsync(CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT to_regclass('public.\"SyncProfiles\"') IS NOT NULL;";

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is bool exists && exists;
    }

    private static JobKey JobKeyFor(Guid userAccountId) => new($"sync:user:{userAccountId:N}");
    private static TriggerKey TriggerKeyFor(Guid userAccountId) => new($"sync:user:{userAccountId:N}:trigger");
}

public sealed class ScheduledSyncJob(IServiceScopeFactory serviceScopeFactory) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlaylistSyncDbContext>();
        var syncExecutionService = scope.ServiceProvider.GetRequiredService<ISyncExecutionService>();

        var userIdRaw = context.JobDetail.JobDataMap.GetString("UserAccountId");
        if (!Guid.TryParse(userIdRaw, out var userAccountId))
        {
            return;
        }

        var user = await dbContext.UserAccounts.SingleOrDefaultAsync(x => x.Id == userAccountId, context.CancellationToken);
        if (user is null)
        {
            return;
        }

        await syncExecutionService.ExecuteForUserAsync(user, $"scheduled-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}", context.CancellationToken);
    }
}

public sealed class SyncSchedulingStartupService(
    ISyncSchedulerService schedulerService,
    ILogger<SyncSchedulingStartupService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await schedulerService.RegisterAllSchedulesAsync(cancellationToken);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            logger.LogWarning(ex, "Skipping schedule registration because required tables are missing. If this is a stale local database, reset it with `docker compose down -v` and restart.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
