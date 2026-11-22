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
        private static readonly DateTime StopCondition = new(2021, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public async Task CollectHistoricalPostsAsync(
            string channelTag, CancellationToken cancellationToken)
        {
            channelTag = channelTag.ToLowerInvariant();
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var state = await db.TelegramChannels.AsNoTracking()
                    .SingleAsync(channel => channel.ChannelTag == channelTag, cancellationToken);
                if (state.IsHistoricalCollectionCompleted)
                    return;

                var before = state.LastProcessedMessageId;
                var page = await FetchPageAsync(channelTag, before, cancellationToken);
                var completed = page.OlderBefore == null || IsBeforeStopCondition(page);
                var nextBefore = page.OlderBefore ?? page.OldestMessageId;
                var filteredPage = FilterEligiblePosts(page);
                await store.SaveHistoricalPageAsync(filteredPage, before, nextBefore, completed, cancellationToken);
                logger.LogInformation("{Channel}: committed {Count} posts, next cursor {Cursor}, completed {Completed}",
                    channelTag, filteredPage.Messages.Count, nextBefore, completed);
                if (completed)
                    return;
            }
        }

        public async Task CollectNewPostsAsync(string channelTag, CancellationToken cancellationToken)
        {
            channelTag = channelTag.ToLowerInvariant();
            var checkpoint = await db.TelegramChannels.AsNoTracking()
                .SingleAsync(channel => channel.ChannelTag == channelTag, cancellationToken);

            var previousNewestId = checkpoint.NewestCollectedMessageId;
            var newestMsgId = previousNewestId;
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

                var reachedCheckpoint = previousNewestId.HasValue && page.OldestMessageId <= previousNewestId;
                var completed = reachedCheckpoint || page.OlderBefore == null || IsBeforeStopCondition(page);
                await store.SaveNewPostsPageAsync(eligiblePage, checkpoint, newestMsgId, completed, cancellationToken);
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

            if (!page.HasValidHistory)
                throw new InvalidOperationException($"Uncertain page coverage for {channelTag}. Checkpoint retained.");

            if (before.HasValue && (page.NewestMessageId >= before || page.OlderBefore >= before))
                throw new InvalidOperationException($"Repeated or non-decreasing page for {channelTag}. Checkpoint retained.");

            return page;
        }

        private static bool IsBeforeStopCondition(ParsedPage page) =>
            page.Diagnostics.Count == 0 && page.Messages.Count > 0 &&
            page.Messages.All(post => post.PostedAt < StopCondition);

        private static ParsedPage FilterEligiblePosts(ParsedPage page) => new()
        {
            ChannelUsername = page.ChannelUsername,
            ChannelTitle = page.ChannelTitle,
            Messages = page.Messages.Where(post => post.PostedAt >= StopCondition).ToList()
        };
    }

}