namespace PlaylistSync.Core;

public sealed class UserAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ExternalUserId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<ConnectedAccount> ConnectedAccounts { get; set; } = new List<ConnectedAccount>();
    public SyncProfile? SyncProfile { get; set; }
    public ICollection<SyncJob> SyncJobs { get; set; } = new List<SyncJob>();
}

public sealed class ConnectedAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserAccountId { get; set; }
    public UserAccount UserAccount { get; set; } = null!;
    public string Provider { get; set; } = string.Empty;
    public string ProviderUserId { get; set; } = string.Empty;
    public string AccessTokenRef { get; set; } = string.Empty;
    public string? RefreshTokenRef { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public string LastRefreshResult { get; set; } = "never";
    public DateTimeOffset? LastRefreshedAt { get; set; }
}

public enum SyncDirection
{
    OneWay = 1,
    TwoWay = 2
}

public enum LikesSyncBehavior
{
    Disabled = 1,
    SourceToTargetOnly = 2,
    TwoWay = 3
}

public sealed class SyncProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserAccountId { get; set; }
    public UserAccount UserAccount { get; set; } = null!;
    public SyncDirection Direction { get; set; } = SyncDirection.OneWay;
    public LikesSyncBehavior LikesBehavior { get; set; } = LikesSyncBehavior.Disabled;
    public string? ScheduleCron { get; set; }
    public string ScheduleTimeZone { get; set; } = "UTC";
    public bool ScheduleEnabled { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<PlaylistMapping> PlaylistMappings { get; set; } = new List<PlaylistMapping>();
}

public sealed class PlaylistMapping
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SyncProfileId { get; set; }
    public SyncProfile SyncProfile { get; set; } = null!;
    public string SourceProvider { get; set; } = string.Empty;
    public string SourcePlaylistId { get; set; } = string.Empty;
    public string TargetProvider { get; set; } = string.Empty;
    public string TargetPlaylistId { get; set; } = string.Empty;
}

public enum SyncRunStatus
{
    Pending = 1,
    Running = 2,
    Completed = 3,
    Failed = 4
}

public sealed class SyncJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserAccountId { get; set; }
    public UserAccount UserAccount { get; set; } = null!;
    public string RequestedIdempotencyKey { get; set; } = string.Empty;
    public SyncRunStatus Status { get; set; } = SyncRunStatus.Pending;
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EndedAt { get; set; }
    public int TotalImportedCount { get; set; }
    public int TotalExportedCount { get; set; }
    public int TotalSkippedCount { get; set; }
    public string? Error { get; set; }

    public ICollection<SyncRun> Runs { get; set; } = new List<SyncRun>();
}

public sealed class SyncRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SyncJobId { get; set; }
    public SyncJob SyncJob { get; set; } = null!;
    public SyncRunStatus Status { get; set; } = SyncRunStatus.Pending;
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EndedAt { get; set; }
    public int ImportedCount { get; set; }
    public int ExportedCount { get; set; }
    public int SkippedCount { get; set; }
    public string? Error { get; set; }
}
