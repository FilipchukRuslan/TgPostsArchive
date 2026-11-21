namespace TelegramPostsArchive.Models
{
    public class PostResponse
    {
        public PostResponse(string channelTag, int messageId, DateTime postedAt, string? text, int? views, string[] mediaUrls, string? forwardFrom, bool? isEdited, string url)
        {
            ChannelTag = channelTag;
            MessageId = messageId;
            PostedAt = postedAt;
            Text = text;
            Views = views;
            MediaUrls = mediaUrls;
            ForwardFrom = forwardFrom;
            IsEdited = isEdited;
            Url = url;
        }

        public string ChannelTag { get; set; } = string.Empty;
        public int MessageId { get; set; }
        public DateTime PostedAt { get; set; }
        public string? Text { get; set; }
        public int? Views { get; set; }
        public string[] MediaUrls { get; set; } = [];
        public string? ForwardFrom { get; set; }
        public bool? IsEdited { get; set; }
        public string Url { get; set; } = string.Empty;
    }
}
