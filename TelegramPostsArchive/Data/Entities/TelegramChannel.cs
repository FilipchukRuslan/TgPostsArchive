namespace TelegramPostsArchive.Data.Entities
{
    public class TelegramChannel
    {
        public static List<string> SeedTags { get; } = ["kpszsu", "dsns_telegram", "ukrainenow"];

        public required string ChannelTag { get; init; }
        public string? Title { get; set; }
        public int? LastProcessedMessageId { get; set; }
        public bool IsHistoricalCollectionCompleted { get; set; }
        public int? NewestCollectedMessageId { get; set; }
        public DateTime? LastSuccessfulSyncAt { get; set; }
    }
}
