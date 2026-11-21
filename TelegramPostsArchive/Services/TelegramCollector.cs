using Microsoft.EntityFrameworkCore;
using TelegramPostsArchive.Data;
using TelegramPostsArchive.Models.Parser;

namespace TelegramPostsArchive.Services
{
    public sealed class TelegramCollector(
    TelegramPageClient client,
    TelegramHtmlParser parser,
    DataContext db,
    TelegramPostStore store,
    ILogger<TelegramCollector> logger)
    {
        private static readonly DateTime Cutoff = new(2021, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public async Task<bool> BackfillAsync(
            string channelTag, int? maxPages = null, CancellationToken cancellationToken = default)
        {
            if (maxPages is <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxPages));

            channelTag = channelTag.ToLowerInvariant();
            var pages = 0;
            while (maxPages == null || pages < maxPages)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var state = await db.TelegramChannels.AsNoTracking()
                    .SingleAsync(channel => channel.ChannelTag == channelTag, cancellationToken);
                if (state.IsHistoricalCollectionCompleted)
                    return true;

                var before = state.LastProcessedMessageId;
                var page = await FetchPageAsync(channelTag, before, cancellationToken);
                var completed = page.Messages.All(post => post.PostedAt < Cutoff) || page.OlderBefore == null;
                var nextBefore = page.OlderBefore ?? page.Messages.Min(post => post.TgMsgId);
                var filteredPage = FilterEligiblePosts(page);
                await store.SaveBackfillPageAsync(filteredPage, before, nextBefore, completed, cancellationToken);
                pages++;
                logger.LogInformation("{Channel}: committed {Count} posts, next cursor {Cursor}, completed {Completed}",
                    channelTag, filteredPage.Messages.Count, nextBefore, completed);
                if (completed)
                    return true;
            }

            return false;
        }

        public async Task GetNewMessageAsync(string channelTag, CancellationToken cancellationToken = default)
        {
            channelTag = channelTag.ToLowerInvariant();
            var channelWithNewestMsg = await store.UpdateNewestCollectedMessageIdAsync(channelTag, cancellationToken);
            
            var newestMsgId = channelWithNewestMsg.NewestCollectedMessageId;
            int? before = null;
            var pages = 0;

            while (true)
            {
                var page = await FetchPageAsync(channelTag, before, cancellationToken);
                var eligiblePage = FilterEligiblePosts(page);
                foreach (var post in eligiblePage.Messages)
                {
                    if (newestMsgId == null || post.TgMsgId > newestMsgId.Value)
                        newestMsgId = post.TgMsgId;
                }

                // Include the full overlap page so already archived posts receive updates.
                var completed = channelWithNewestMsg.NewestCollectedMessageId == null ||
                    page.Messages.Any(post => post.TgMsgId <= channelWithNewestMsg.NewestCollectedMessageId.Value) ||
                    eligiblePage.Messages.Count == 0 || page.OlderBefore == null;
                await store.SavePollPageAsync(eligiblePage, channelWithNewestMsg, newestMsgId, completed, cancellationToken);
                pages++;
                if (completed)
                {
                    logger.LogInformation("{Channel}: poll completed after {Pages} pages, newest message {MessageId}",
                        channelTag, pages, newestMsgId);
                    return;
                }

                before = page.OlderBefore;
            }
        }

        private async Task<ParsedPage> FetchPageAsync(string channelTag, int? before, CancellationToken cancellationToken)
        {
            var html = await client.GetPageHtmlAsync(channelTag, before, cancellationToken);
            var page = parser.ParseTmePage(html, channelTag);
            foreach (var diagnostic in page.Diagnostics)
                logger.LogWarning("{Channel}: {Diagnostic}", channelTag, diagnostic);

            if (!page.HasValidHistory || page.Messages.Count == 0 || page.Diagnostics.Count > 0)
                throw new InvalidOperationException($"Uncertain page coverage for {channelTag}. Checkpoint retained.");

            if (before.HasValue && page.Messages.Any(post => post.TgMsgId >= before.Value))
                throw new InvalidOperationException($"Repeated or non-decreasing page for {channelTag}. Checkpoint retained.");

            return page;
        }

        private static ParsedPage FilterEligiblePosts(ParsedPage page) => new()
        {
            ChannelUsername = page.ChannelUsername,
            ChannelTitle = page.ChannelTitle,
            Messages = page.Messages.Where(post => post.PostedAt >= Cutoff).ToList()
        };
    }

}