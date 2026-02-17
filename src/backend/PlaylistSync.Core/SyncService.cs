namespace PlaylistSync.Core;

public interface ISyncService
{
    Task RunPlaylistSyncAsync(CancellationToken cancellationToken = default);
}

public sealed class SyncService : ISyncService
{
    public Task RunPlaylistSyncAsync(CancellationToken cancellationToken = default)
    {
        // Placeholder for core sync orchestration logic.
        return Task.CompletedTask;
    }
}
