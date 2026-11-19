
using System.Text.RegularExpressions;

namespace TelegramPostsArchive.Services;

public sealed class TelegramPageClient
{
    private readonly HttpClient _httpClient;

    public TelegramPageClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<string> GetPageHtmlAsync(
        string channelTag,
        int? before = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(channelTag) ||
            !Regex.IsMatch(channelTag, @"^[A-Za-z0-9_]{5,32}$"))
        {
            throw new ArgumentException(
                "Invalid Telegram channel tag.",
                nameof(channelTag));
        }

        if (before is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(before));
        }

        var path = before.HasValue
            ? $"s/{channelTag}?before={before.Value}"
            : $"s/{channelTag}";

        using var response = await _httpClient.GetAsync(
            path,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync(
            cancellationToken);
    }
}
