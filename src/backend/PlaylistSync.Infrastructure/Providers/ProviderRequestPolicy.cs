using System.Net;

namespace PlaylistSync.Infrastructure.Providers;

internal static class ProviderRequestPolicy
{
    public static async Task<HttpResponseMessage> SendWithRetryAsync(Func<CancellationToken, Task<HttpResponseMessage>> send, CancellationToken cancellationToken)
    {
        const int maxAttempts = 4;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            HttpResponseMessage? response = null;
            try
            {
                response = await send(cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return response;
                }

                var statusCode = (int)response.StatusCode;
                var transient = IsTransientStatus(response.StatusCode);
                var retryAfter = GetRetryAfter(response);

                if (transient && attempt < maxAttempts)
                {
                    response.Dispose();
                    await Task.Delay(retryAfter ?? GetExponentialBackoff(attempt), cancellationToken);
                    continue;
                }

                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                response.Dispose();
                throw new ProviderApiException($"Provider request failed with status {statusCode}: {error}", statusCode, transient, retryAfter);
            }
            catch (Exception ex) when (attempt < maxAttempts && IsTransientException(ex))
            {
                response?.Dispose();
                await Task.Delay(GetExponentialBackoff(attempt), cancellationToken);
            }
        }

        throw new ProviderApiException("Provider request failed after retries.", 0, true);
    }

    private static bool IsTransientException(Exception ex)
        => ex is HttpRequestException || ex is TimeoutException || ex is TaskCanceledException;

    private static bool IsTransientStatus(HttpStatusCode code)
        => code == HttpStatusCode.TooManyRequests || (int)code >= 500;

    private static TimeSpan? GetRetryAfter(HttpResponseMessage response)
    {
        if (response.Headers.RetryAfter?.Delta is not null)
        {
            return response.Headers.RetryAfter.Delta.Value;
        }

        if (response.Headers.RetryAfter?.Date is not null)
        {
            var delay = response.Headers.RetryAfter.Date.Value - DateTimeOffset.UtcNow;
            return delay > TimeSpan.Zero ? delay : TimeSpan.FromMilliseconds(500);
        }

        return null;
    }

    private static TimeSpan GetExponentialBackoff(int attempt)
    {
        var jitter = Random.Shared.Next(200, 1000);
        return TimeSpan.FromMilliseconds((400 * Math.Pow(2, attempt - 1)) + jitter);
    }
}
