namespace PlaylistSync.Infrastructure.Providers;

public sealed class ProviderApiException(string message, int statusCode, bool isTransient, TimeSpan? retryAfter = null) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public bool IsTransient { get; } = isTransient;
    public TimeSpan? RetryAfter { get; } = retryAfter;
}
