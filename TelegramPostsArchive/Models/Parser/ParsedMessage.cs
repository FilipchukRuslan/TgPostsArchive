namespace TelegramPostsArchive.Models.Parser
{
    public class ParsedMessage
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
        public List<string> MediaUrls { get; set; } = new();
        public bool? IsEdited { get; set; }
        public string ContentHash { get; set; } = "";
    }
}
