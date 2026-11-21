using System.Net;
using System.Text.RegularExpressions;

namespace TelegramPostsArchive.Services;

public sealed class TelegramPageClient
{
    private readonly HttpClient _httpClient;
    private readonly TelegramRequestBridge _requestBridge;

    public TelegramPageClient(HttpClient httpClient, TelegramRequestBridge? requestBridge = null)
    {
        _httpClient = httpClient;
        _requestBridge = requestBridge ?? new TelegramRequestBridge();
    }

    public async Task<string> GetPageHtmlAsync(
        string channelTag,
        int? before = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(channelTag) ||
            !Regex.IsMatch(channelTag, @"^[A-Za-z0-9_]{5,32}$"))
        {
            throw new ArgumentException("Invalid Telegram channel tag.", nameof(channelTag));
        }

        if (before is <= 0)
            throw new ArgumentOutOfRangeException(nameof(before));

        var path = before.HasValue
            ? $"s/{channelTag}?before={before.Value}"
            : $"s/{channelTag}";

        const int maxAttempts = 3;
        for (var attempt = 1; ; attempt++)
        {
            HttpResponseMessage response;
            try
            {
                response = await _requestBridge.SendAsync(_httpClient, path, cancellationToken);
            }
            catch (HttpRequestException) when (attempt < maxAttempts)
            {
                await Task.Delay(TimeSpan.FromSeconds(attempt), cancellationToken);
                continue;
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested && attempt < maxAttempts)
            {
                await Task.Delay(TimeSpan.FromSeconds(attempt), cancellationToken);
                continue;
            }

            using (response)
            {
                var retryable = response.StatusCode == HttpStatusCode.TooManyRequests ||
                    (int)response.StatusCode >= 500 && (int)response.StatusCode <= 599;

                if (!retryable || attempt == maxAttempts)
                {
                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadAsStringAsync(cancellationToken);
                }

                var retryAfter = TelegramRequestBridge.GetRetryAfter(response);
                if (retryAfter > TimeSpan.FromSeconds(30))
                {
                    response.EnsureSuccessStatusCode();
                }

                await Task.Delay(
                    retryAfter > TimeSpan.FromSeconds(attempt) ? retryAfter : TimeSpan.FromSeconds(attempt),
                    cancellationToken);
            }
        }
    }
}

public sealed class TelegramRequestBridge
{
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private DateTimeOffset _nextRequest = DateTimeOffset.MinValue;

    public async Task<HttpResponseMessage> SendAsync(
        HttpClient client, string path, CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var delay = _nextRequest - DateTimeOffset.UtcNow;
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay, cancellationToken);

            _nextRequest = DateTimeOffset.UtcNow.AddSeconds(1);
            var response = await client.GetAsync(path, cancellationToken);
            if (response.StatusCode == HttpStatusCode.TooManyRequests ||
                (int)response.StatusCode >= 500 && (int)response.StatusCode <= 599)
            {
                var retryAt = DateTimeOffset.UtcNow + GetRetryAfter(response);
                if (retryAt > _nextRequest)
                    _nextRequest = retryAt;
            }

            return response;
        }
        finally
        {
            _mutex.Release();
        }
    }

    public static TimeSpan GetRetryAfter(HttpResponseMessage response)
    {
        var header = response.Headers.RetryAfter;
        var delay = header?.Delta ?? (header?.Date - DateTimeOffset.UtcNow) ?? TimeSpan.Zero;
        return delay > TimeSpan.Zero ? delay : TimeSpan.Zero;
    }
}
