using Microsoft.AspNetCore.Mvc;
using PlaylistSync.Core;

namespace PlaylistSync.Api.Controllers;

[ApiController]
[Route("auth")]
public class AuthController(IOAuthService oauthService) : ControllerBase
{
    [HttpGet("spotify/start")]
    public async Task<IActionResult> StartSpotify([FromQuery] string? userId, CancellationToken cancellationToken)
    {
        var redirectUrl = await oauthService.BuildAuthorizationUrlAsync(OAuthProvider.Spotify, ResolveUserId(userId), cancellationToken);
        return Redirect(redirectUrl);
    }

    [HttpGet("spotify/callback")]
    public async Task<IActionResult> SpotifyCallback([FromQuery] string code, [FromQuery] string state, [FromQuery] string? userId, CancellationToken cancellationToken)
    {
        var result = await oauthService.HandleCallbackAsync(OAuthProvider.Spotify, code, state, ResolveUserId(userId), cancellationToken);
        return Ok(ToResponse(result));
    }

    [HttpGet("soundcloud/start")]
    public async Task<IActionResult> StartSoundCloud([FromQuery] string? userId, CancellationToken cancellationToken)
    {
        var redirectUrl = await oauthService.BuildAuthorizationUrlAsync(OAuthProvider.SoundCloud, ResolveUserId(userId), cancellationToken);
        return Redirect(redirectUrl);
    }

    [HttpGet("soundcloud/callback")]
    public async Task<IActionResult> SoundCloudCallback([FromQuery] string code, [FromQuery] string state, [FromQuery] string? userId, CancellationToken cancellationToken)
    {
        var result = await oauthService.HandleCallbackAsync(OAuthProvider.SoundCloud, code, state, ResolveUserId(userId), cancellationToken);
        return Ok(ToResponse(result));
    }

    private string ResolveUserId(string? userId)
        => string.IsNullOrWhiteSpace(userId)
            ? Request.Headers["X-User-Id"].FirstOrDefault() ?? "demo-user"
            : userId;

    private static object ToResponse(AuthCallbackResult result) => new
    {
        connected = result.Connected,
        expiresAt = result.ExpiresAt,
        lastRefreshResult = result.LastRefreshResult
    };
}
