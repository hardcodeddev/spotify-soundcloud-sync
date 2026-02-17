using Microsoft.AspNetCore.Mvc;
using PlaylistSync.Core;

namespace PlaylistSync.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SyncController(ISyncService syncService) : ControllerBase
{
    [HttpPost("run")]
    public async Task<IActionResult> Run(CancellationToken cancellationToken)
    {
        await syncService.RunPlaylistSyncAsync(cancellationToken);
        return Accepted();
    }
}
