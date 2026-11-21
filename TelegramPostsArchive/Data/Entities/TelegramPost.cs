namespace TelegramPostsArchive.Data.Entities
{
    public class TelegramPost
    {
        public required string ChannelTag { get; init; }
        public int MessageId { get; init; }
        public DateTime PostedAt { get; set; }
        public string? Text { get; set; }
        public int? Views { get; set; }
        public string[] MediaUrls { get; set; } = [];
        public string? ForwardFrom { get; set; }
        public bool? IsEdited { get; set; }
    }
}
