using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlaylistSync.Core;
using PlaylistSync.Infrastructure.Persistence;

namespace PlaylistSync.Api.Controllers;

[ApiController]
[Route("auth")]
public class AuthController(IOAuthService oauthService, PlaylistSyncDbContext dbContext) : ControllerBase
{
    [HttpGet("connections")]
    public async Task<IActionResult> GetConnections(CancellationToken cancellationToken)
    {
        var user = await GetOrCreateUserAsync(cancellationToken);
        var connected = await dbContext.ConnectedAccounts
            .AsNoTracking()
            .Where(x => x.UserAccountId == user.Id)
            .ToListAsync(cancellationToken);

        var spotify = connected.FirstOrDefault(x => x.Provider.Equals("spotify", StringComparison.OrdinalIgnoreCase));
        var soundCloud = connected.FirstOrDefault(x => x.Provider.Equals("soundcloud", StringComparison.OrdinalIgnoreCase));

        return Ok(new
        {
            spotify = ToConnectionResponse(spotify),
            soundcloud = ToConnectionResponse(soundCloud)
        });
    }

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

    private async Task<UserAccount> GetOrCreateUserAsync(CancellationToken cancellationToken)
    {
        var userId = ResolveUserId(null);
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

    private string ResolveUserId(string? userId)
    {
        if (!string.IsNullOrWhiteSpace(userId))
        {
            WriteUserCookie(userId);
            return userId;
        }

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

    private static object ToConnectionResponse(ConnectedAccount? account) => new
    {
        connected = account is not null,
        expiresAt = account?.ExpiresAt,
        lastRefreshResult = account?.LastRefreshResult
    };

    private static object ToResponse(AuthCallbackResult result) => new
    {
        connected = result.Connected,
        expiresAt = result.ExpiresAt,
        lastRefreshResult = result.LastRefreshResult
    };
}
