using HtmlAgilityPack;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace TelegramPostsArchive.Services;

public class TelegramHtmlParser
{
    private readonly ParserOptions _options;

    private static readonly Regex MentionRegex = new(
        @"(?<![\w])@([A-Za-z0-9_]{5,})",
        RegexOptions.Compiled);

    public TelegramHtmlParser(ParserOptions? options = null)
    {
        _options = options ?? new ParserOptions();
    }

    public ParsedPage ParseTmePage(
        string html,
        string channelUsername)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);

        // Extract channel title.
        var titleNode = document.DocumentNode.SelectSingleNode(
            "//*[contains(concat(' ', normalize-space(@class), ' '), " +
            "' tgme_channel_info_header_title ')]//span | " +
            "//*[contains(concat(' ', normalize-space(@class), ' '), " +
            "' tgme_channel_info_header_title ')]");

        string? channelTitle = titleNode != null
            ? NormalizeText(titleNode.InnerText)
            : null;

        var messages = new List<ParsedMessage>();

        // Find every Telegram message.
        var messageNodes = document.DocumentNode.SelectNodes(
            "//*[contains(concat(' ', normalize-space(@class), ' '), " +
            "' tgme_widget_message ')]");

        if (messageNodes != null)
        {
            foreach (var node in messageNodes)
            {
                // Extract post reference.
                string post = node.GetAttributeValue(
                    "data-post", "");

                var parsedPost = ParsePostRef(post);

                if (parsedPost == null)
                    continue;

                var (username, messageId) = parsedPost.Value;

                // Extract message text.
                var textNode = node.SelectSingleNode(
                    ".//*[contains(concat(' ', normalize-space(@class), ' '), " +
                    "' tgme_widget_message_text ')]");

                string text = textNode != null
                    ? NormalizeText(textNode.InnerText)
                    : "";

                // Extract publication datetime.
                // Select only time elements with a datetime attribute.
                var timeNode = node.SelectSingleNode(
                    ".//time[@datetime]");

                DateTimeOffset? postedAt = ParseDateTime(
                    timeNode?.GetAttributeValue("datetime", null));

                // Extract views.
                var viewNode = node.SelectSingleNode(
                    ".//*[contains(concat(' ', normalize-space(@class), ' '), " +
                    "' tgme_widget_message_views ')]");

                int? views = ParseViews(
                    viewNode?.InnerText);

                // Extract links.
                var links = new List<string>();

                var anchorNodes = node.SelectNodes(".//a");

                if (anchorNodes != null)
                {
                    foreach (var anchor in anchorNodes)
                    {
                        string href = anchor.GetAttributeValue(
                            "href", "");

                        string? normalizedLink = NormalizeLink(href);

                        if (!string.IsNullOrEmpty(normalizedLink))
                            links.Add(normalizedLink);
                    }
                }

                // Extract forwarding information.
                var forwardNode = node.SelectSingleNode(
                    ".//*[contains(concat(' ', normalize-space(@class), ' '), " +
                    "' tgme_widget_message_forwarded_from_name ')]");

                string? forwardFrom = forwardNode != null
                    ? NormalizeText(forwardNode.InnerText)
                    : null;

                // Construct Telegram message URL.
                string url =
                    $"https://t.me/{username}/{messageId}";

                // Extract mentions from text and links.
                string mentionSource =
                    text + " " + string.Join(" ", links);

                var mentions = ExtractMentions(mentionSource);

                // Build structured message.
                messages.Add(new ParsedMessage
                {
                    ChannelUsername = username,
                    ChannelTitle = channelTitle,
                    TgMsgId = messageId,
                    Url = url,
                    PostedAt = postedAt,
                    Text = text,
                    Views = views,
                    Links = links
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(x => x, StringComparer.Ordinal)
                        .ToList(),
                    Mentions = mentions,
                    ForwardFrom = forwardFrom,
                    ContentHash = ContentHash(
                        username, messageId, text)
                });
            }
        }

        // Find oldest message ID for pagination.
        int? nextBefore = messages.Count > 0
            ? messages.Min(m => m.TgMsgId)
            : null;

        return new ParsedPage
        {
            ChannelUsername = channelUsername,
            ChannelTitle = channelTitle,
            Messages = messages,
            NextBefore = nextBefore
        };
    }

    // -----------------------------------------
    // Parse view counts
    // -----------------------------------------

    public int? ParseViews(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        string raw = value
            .Replace(',', '.')
            .Trim();

        var numberChars = new StringBuilder();
        string suffix = "";

        foreach (char c in raw)
        {
            if (char.IsDigit(c) || c == '.')
            {
                numberChars.Append(c);
                continue;
            }

            if (numberChars.Length > 0 &&
                !char.IsWhiteSpace(c))
            {
                suffix = char.ToLowerInvariant(c).ToString();
                break;
            }
        }

        if (numberChars.Length == 0)
            return null;

        if (!double.TryParse(
                numberChars.ToString(),
                NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out double number))
        {
            return null;
        }

        double multiplier =
            _options.ViewMultipliers.TryGetValue(
                suffix, out double result)
                ? result
                : 1;

        double total = number * multiplier;

        if (double.IsNaN(total) ||
            double.IsInfinity(total) ||
            total > int.MaxValue ||
            total < int.MinValue)
        {
            return null;
        }

        return (int)total;
    }

    // -----------------------------------------
    // Parse datetime
    // -----------------------------------------

    public static DateTimeOffset? ParseDateTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var result))
        {
            return result;
        }

        return null;
    }

    // -----------------------------------------
    // Parse Telegram post reference
    // -----------------------------------------

    private static (string Username, int MessageId)?
        ParsePostRef(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        string[] parts = value
            .Trim('/')
            .Split('/');

        if (parts.Length < 2)
            return null;

        string username = parts[^2];
        string messageIdRaw = parts[^1];

        if (string.IsNullOrWhiteSpace(username))
            return null;

        if (!int.TryParse(
                messageIdRaw,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int messageId))
        {
            return null;
        }

        return (username, messageId);
    }

    // -----------------------------------------
    // Normalize text
    // -----------------------------------------

    public static string NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        string decoded = HtmlEntity.DeEntitize(value);

        return Regex.Replace(
            decoded,
            @"\s+",
            " ").Trim();
    }

    // -----------------------------------------
    // Normalize links
    // -----------------------------------------

    public static string? NormalizeLink(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        string link = HtmlEntity.DeEntitize(value).Trim();

        if (!Uri.TryCreate(
                link,
                UriKind.Absolute,
                out var uri))
        {
            return null;
        }

        if (uri.Scheme != Uri.UriSchemeHttp &&
            uri.Scheme != Uri.UriSchemeHttps)
        {
            return null;
        }

        return uri.AbsoluteUri;
    }

    // -----------------------------------------
    // Extract Telegram mentions
    // -----------------------------------------

    public static List<string> ExtractMentions(string text)
    {
        return MentionRegex.Matches(text)
            .Select(m => "@" + m.Groups[1].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // -----------------------------------------
    // Generate content hash
    // -----------------------------------------

    public static string ContentHash(
        string username,
        int messageId,
        string text)
    {
        string input =
            $"{username}|{messageId}|{text}";

        byte[] bytes = Encoding.UTF8.GetBytes(input);

        byte[] hash = SHA256.HashData(bytes);

        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}


public sealed class ParsedMessage
{
    public string ChannelUsername { get; set; } = "";
    public string? ChannelTitle { get; set; }
    public int TgMsgId { get; set; }
    public string Url { get; set; } = "";
    public DateTimeOffset? PostedAt { get; set; }
    public string Text { get; set; } = "";
    public int? Views { get; set; }
    public List<string> Links { get; set; } = new();
    public List<string> Mentions { get; set; } = new();
    public string? ForwardFrom { get; set; }
    public string ContentHash { get; set; } = "";
}

public sealed class ParsedPage
{
    public string ChannelUsername { get; set; } = "";
    public string? ChannelTitle { get; set; }
    public List<ParsedMessage> Messages { get; set; } = new();
    public int? NextBefore { get; set; }
}

// ---------------------------------------------
// Parser configuration
// ---------------------------------------------

public sealed class ParserOptions
{
    public Dictionary<string, double> ViewMultipliers { get; set; } =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["k"] = 1_000,
            ["m"] = 1_000_000,
            ["b"] = 1_000_000_000
        };

    public static ParserOptions LoadRules(string path)
    {
        if (!File.Exists(path))
            return new ParserOptions();

        using var document = JsonDocument.Parse(File.ReadAllText(path));

        var options = new ParserOptions();

        if (document.RootElement.TryGetProperty(
                "view_multipliers", out var multipliers))
        {
            options.ViewMultipliers.Clear();

            foreach (var property in multipliers.EnumerateObject())
            {
                options.ViewMultipliers[property.Name] =
                    property.Value.GetDouble();
            }
        }

        return options;
    }
}
