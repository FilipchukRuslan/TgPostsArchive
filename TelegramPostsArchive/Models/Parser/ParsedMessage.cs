namespace TelegramPostsArchive.Models.Parser
{
    public class ParsedMessage
    {
        public string ChannelUsername { get; set; } = "";
        public int TgMsgId { get; set; }
        public DateTimeOffset? PostedAt { get; set; }
        public string Text { get; set; } = "";
        public int? Views { get; set; }
        public string? ForwardFrom { get; set; }
        public List<string> MediaUrls { get; set; } = new();
        public bool? IsEdited { get; set; }
    }
}
