using Quartz;

namespace PlaylistSync.Api.Services;

public interface ICronScheduleValidator
{
    bool TryValidate(string cronExpression, out string normalizedCronExpression, out string? validationError);
}

public sealed class CronScheduleValidator : ICronScheduleValidator
{
    private static readonly TimeSpan MinimumInterval = TimeSpan.FromMinutes(15);

    public bool TryValidate(string cronExpression, out string normalizedCronExpression, out string? validationError)
    {
        validationError = null;
        normalizedCronExpression = string.Empty;

        if (string.IsNullOrWhiteSpace(cronExpression))
        {
            validationError = "Cron expression is required.";
            return false;
        }

        var segments = cronExpression.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        normalizedCronExpression = segments.Length == 5
            ? $"0 {cronExpression}"
            : cronExpression;

        if (normalizedCronExpression.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length is < 6 or > 7)
        {
            validationError = "Cron expression must have 5, 6, or 7 fields.";
            return false;
        }

        if (!CronExpression.IsValidExpression(normalizedCronExpression))
        {
            validationError = "Cron expression is invalid.";
            return false;
        }

        var cron = new CronExpression(normalizedCronExpression);
        var now = DateTimeOffset.UtcNow;
        var first = cron.GetNextValidTimeAfter(now);
        var second = first is null ? null : cron.GetNextValidTimeAfter(first.Value);

        if (first is null || second is null)
        {
            validationError = "Cron expression does not produce future schedules.";
            return false;
        }

        if (second.Value - first.Value < MinimumInterval)
        {
            validationError = "Cron schedule is too frequent. Minimum supported interval is 15 minutes.";
            return false;
        }

        return true;
    }
}
