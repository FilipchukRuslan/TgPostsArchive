using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using TelegramPostsArchive.Models.Parser;

namespace TelegramPostsArchive.Services;

public class TelegramHtmlParser
{
    private readonly ParserOptions _options;

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
        var diagnostics = new List<string>();
        int? oldestMessageId = null;
        int? newestMessageId = null;
        var hasInvalidPostReference = false;

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

                if (parsedPost == null || parsedPost.Value.MessageId <= 0 ||
                    !string.Equals(parsedPost.Value.Username, channelUsername, StringComparison.OrdinalIgnoreCase))
                {
                    diagnostics.Add($"Invalid post reference: {post}");
                    hasInvalidPostReference = true;
                    continue;
                }

                var (username, messageId) = parsedPost.Value;
                // A skipped post can still identify the boundary of this page.
                oldestMessageId = Math.Min(oldestMessageId ?? messageId, messageId);
                newestMessageId = Math.Max(newestMessageId ?? messageId, messageId);

                // Extract message text.
                var textNode = node.SelectSingleNode(
                    ".//*[contains(concat(' ', normalize-space(@class), ' '), " +
                    "' tgme_widget_message_text ')]");

                string text = textNode != null
                    ? ExtractText(textNode)
                    : "";

                // Extract publication datetime.
                // Select only time elements with a datetime attribute.
                var timeNode = node.SelectSingleNode(
                    ".//time[@datetime]");

                DateTimeOffset? postedAt = ParseDateTime(
                    timeNode?.GetAttributeValue("datetime", null));

                if (postedAt == null)
                {
                    diagnostics.Add($"Missing or invalid publication time: {post}");
                    continue;
                }

                // Extract views.
                var viewNode = node.SelectSingleNode(
                    ".//*[contains(concat(' ', normalize-space(@class), ' '), " +
                    "' tgme_widget_message_views ')]");

                int? views = ParseViews(
                    viewNode?.InnerText);

                var mediaUrls = new List<string>();
                foreach (var element in node.Descendants())
                {
                    string? mediaUrl = null;
                    var classes = element.GetAttributeValue("class", "").Split(' ');
                    if (classes.Contains("tgme_widget_message_photo_wrap"))
                    {
                        var style = element.GetAttributeValue("style", "");
                        var match = Regex.Match(style, @"background-image\s*:\s*url\(\s*['""]?(?<url>[^'"")]+)", RegexOptions.IgnoreCase);
                        if (match.Success)
                            mediaUrl = match.Groups["url"].Value.Trim();
                    }
                    else if (element.Name == "video" || (element.Name == "source" && element.ParentNode?.Name == "video"))
                    {
                        mediaUrl = element.GetAttributeValue("src", "");
                    }
                    else if (classes.Contains("tgme_widget_message_video_player"))
                    {
                        var video = element.SelectSingleNode(".//video[@src]");

                        if (video == null || string.IsNullOrWhiteSpace(
                            video.GetAttributeValue("src", "")))
                        {
                            mediaUrl = element.GetAttributeValue("href", "");
                        }
                    }

                    var normalized = NormalizeLink(mediaUrl);
                    if (normalized != null)
                        mediaUrls.Add(normalized);
                }

                var metadata = node.SelectSingleNode(
                    ".//*[contains(concat(' ', normalize-space(@class), ' '), ' tgme_widget_message_meta ')]");
                bool? edited = metadata != null && Regex.IsMatch(
                    metadata.InnerText, @"\bedited\b", RegexOptions.IgnoreCase) ? true : null;

                // Extract forwarding information.
                var forwardNode = node.SelectSingleNode(
                    ".//*[contains(concat(' ', normalize-space(@class), ' '), " +
                    "' tgme_widget_message_forwarded_from_name ')]");

                string? forwardFrom = forwardNode != null
                    ? NormalizeText(forwardNode.InnerText)
                    : null;

                // Build structured message.
                messages.Add(new ParsedMessage
                {
                    ChannelUsername = username,
                    TgMsgId = messageId,
                    PostedAt = postedAt,
                    Text = text,
                    Views = views,
                    ForwardFrom = forwardFrom,
                    MediaUrls = mediaUrls.Distinct(StringComparer.Ordinal).ToList(),
                    IsEdited = edited
                });
            }
        }

        int? olderBefore = null;
        var olderLinks = document.DocumentNode.SelectNodes(
            "//a[contains(concat(' ', normalize-space(@class), ' '), ' tme_messages_more ') and not(@data-after)]");
        var hasValidPagination = olderLinks == null && (!hasInvalidPostReference || oldestMessageId.HasValue);
        if (olderLinks is { Count: 1 })
        {
            var href = HtmlEntity.DeEntitize(olderLinks[0].GetAttributeValue("href", ""));
            if (Uri.TryCreate(new Uri("https://t.me/"), href, out var olderUri) &&
                olderUri.Host == "t.me" &&
                string.Equals(olderUri.AbsolutePath, $"/s/{channelUsername}", StringComparison.OrdinalIgnoreCase) &&
                olderUri.Query.StartsWith("?before=", StringComparison.Ordinal) &&
                int.TryParse(olderUri.Query["?before=".Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var cursor) &&
                cursor > 0 &&
                (cursor == oldestMessageId || hasInvalidPostReference && (oldestMessageId == null || cursor < oldestMessageId)))
            {
                olderBefore = cursor;
                hasValidPagination = true;
            }
        }

        if (!hasValidPagination)
            diagnostics.Add("Could not verify the older-page cursor.");

        var historyNode = document.DocumentNode.SelectSingleNode(
            "//*[contains(concat(' ', normalize-space(@class), ' '), ' tgme_channel_history ')]");

        return new ParsedPage
        {
            ChannelUsername = channelUsername,
            ChannelTitle = channelTitle,
            Messages = messages,
            Diagnostics = diagnostics,
            OldestMessageId = oldestMessageId,
            NewestMessageId = newestMessageId,
            OlderBefore = olderBefore,
            HasValidHistory = hasValidPagination && historyNode != null && !string.IsNullOrWhiteSpace(channelTitle)
        };
    }

    private static string ExtractText(HtmlNode node)
    {
        var text = new StringBuilder();
        AppendText(node, text);
        var decoded = HtmlEntity.DeEntitize(text.ToString()).Replace("\r", "");
        return string.Join("\n", decoded.Split('\n')
            .Select(line => Regex.Replace(line, @"[^\S\n]+", " ").Trim())).Trim();
    }

    private static void AppendText(HtmlNode node, StringBuilder text)
    {
        if (node.NodeType == HtmlNodeType.Text)
        {
            text.Append(node.InnerText);
            return;
        }

        if (node.Name == "br")
        {
            text.Append('\n');
            return;
        }

        var isBlock = node.Name is "p" or "blockquote";
        if (isBlock && text.Length > 0 && text[^1] != '\n')
            text.Append('\n');

        foreach (var child in node.ChildNodes)
            AppendText(child, text);

        if (isBlock && text.Length > 0 && text[^1] != '\n')
            text.Append('\n');
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
        if (link.StartsWith("//", StringComparison.Ordinal))
            link = "https:" + link;

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
}
