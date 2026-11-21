using TelegramPostsArchive.Services;

namespace TelegramPostsArchive.Models.Parser
{
    public class ParsedPage
    {
        public string ChannelUsername { get; set; } = "";
        public string? ChannelTitle { get; set; }
        public List<ParsedMessage> Messages { get; set; } = new();
        public List<string> Diagnostics { get; set; } = new();
        public int? NextBefore { get; set; }
        public int? OlderBefore { get; set; }
        public bool HasValidHistory { get; set; }
    }
}
