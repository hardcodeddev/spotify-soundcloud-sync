using System.Security.Cryptography;
using System.Text;

namespace PlaylistSync.Infrastructure.Auth;

internal static class PkceGenerator
{
    public static (string state, string codeVerifier, string codeChallenge) Generate()
    {
        var state = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var verifier = Base64UrlEncode(RandomNumberGenerator.GetBytes(64));
        var challengeBytes = SHA256.HashData(Encoding.UTF8.GetBytes(verifier));
        var challenge = Base64UrlEncode(challengeBytes);
        return (state, verifier, challenge);
    }

    private static string Base64UrlEncode(byte[] bytes) => Convert.ToBase64String(bytes)
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');
}
