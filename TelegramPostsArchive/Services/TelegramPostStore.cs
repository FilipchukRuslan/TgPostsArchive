using Microsoft.EntityFrameworkCore;
using TelegramPostsArchive.Data;
using TelegramPostsArchive.Data.Entities;
using TelegramPostsArchive.Models.Parser;

namespace TelegramPostsArchive.Services
{
    public sealed class TelegramPostStore(DataContext db)
    {
        public async Task SaveHistoricalPageAsync(
            ParsedPage page, int? expectedBefore, int? nextBefore, bool completed,
            CancellationToken cancellationToken = default)
        {
            if (nextBefore is <= 0 || nextBefore >= expectedBefore)
                throw new ArgumentOutOfRangeException(nameof(nextBefore), "Historical collection cursors must decrease.");

            var channelTag = page.ChannelUsername.ToLowerInvariant();
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var channel = await LockChannelAsync(channelTag, cancellationToken);
            if (channel.IsHistoricalCollectionCompleted || channel.LastProcessedMessageId != expectedBefore)
                throw new InvalidOperationException("Historical collection progress changed during the fetch. Resume from the saved checkpoint.");

            await WritePageAsync(page, cancellationToken);

            // Save the latest eligible ID from the first historical page as the initial polling anchor.
            var newest = channel.NewestCollectedMessageId;
            if (expectedBefore == null && newest == null)
                newest = page.Messages.Max(post => (int?)post.TgMsgId);

            await db.TelegramChannels.Where(row => row.ChannelTag == channelTag)
                .ExecuteUpdateAsync(update => update
                    .SetProperty(row => row.LastProcessedMessageId, nextBefore ?? expectedBefore)
                    .SetProperty(row => row.NewestCollectedMessageId, newest)
                    .SetProperty(row => row.IsHistoricalCollectionCompleted, completed), cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        public async Task SaveNewPostsPageAsync(
            ParsedPage page, TelegramChannel checkpoint, int? newest, bool completed, CancellationToken cancellationToken)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var channel = await LockChannelAsync(checkpoint.ChannelTag, cancellationToken);
            if (!channel.IsHistoricalCollectionCompleted)
                throw new InvalidOperationException("Historical collection must complete before collecting new posts.");

            if (channel.NewestCollectedMessageId != checkpoint.NewestCollectedMessageId ||
                channel.LastSuccessfulSyncAt != checkpoint.LastSuccessfulSyncAt)
            {
                throw new InvalidOperationException("New post collection progress changed during the fetch. Retry from the saved checkpoint.");
            }

            await WritePageAsync(page, cancellationToken);
            if (completed)
            {
                var succeededAt = DateTime.UtcNow;
                await db.TelegramChannels.Where(row => row.ChannelTag == checkpoint.ChannelTag)
                    .ExecuteUpdateAsync(update => update
                        .SetProperty(row => row.NewestCollectedMessageId, newest)
                        .SetProperty(row => row.LastSuccessfulSyncAt, succeededAt), cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }

        private async Task<TelegramChannel> LockChannelAsync(string channelTag, CancellationToken cancellationToken)
        {
            var channels = await db.TelegramChannels.FromSqlInterpolated(
                $"""SELECT * FROM "TelegramChannels" WHERE "ChannelTag" = {channelTag} FOR NO KEY UPDATE""")
                .AsNoTracking().ToListAsync(cancellationToken);
            return channels.Single();
        }

        private async Task WritePageAsync(ParsedPage page, CancellationToken cancellationToken)
        {
            var channelTag = page.ChannelUsername.ToLowerInvariant();

            if (!string.IsNullOrWhiteSpace(page.ChannelTitle))
            {
                await db.TelegramChannels
                    .Where(channel => channel.ChannelTag == channelTag && channel.Title != page.ChannelTitle)
                    .ExecuteUpdateAsync(update => update.SetProperty(channel => channel.Title, page.ChannelTitle),
                        cancellationToken);
            }

            foreach (var post in page.Messages)
            {
                if (!string.Equals(post.ChannelUsername, channelTag, StringComparison.OrdinalIgnoreCase) ||
                    post.TgMsgId <= 0 || post.PostedAt is not { } postedAt)
                {
                    throw new ArgumentException("The page contains a post with an invalid identity or publication time.",
                        nameof(page));
                }

                var text = string.IsNullOrWhiteSpace(post.Text) ? null : post.Text;
                var forwardFrom = string.IsNullOrWhiteSpace(post.ForwardFrom) ? null : post.ForwardFrom;
                var mediaUrls = post.MediaUrls.ToArray();

                await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "TelegramPosts"
                    ("ChannelTag", "MessageId", "PostedAt", "Text", "Views", "MediaUrls", "ForwardFrom", "IsEdited")
                VALUES
                    ({channelTag}, {post.TgMsgId}, {postedAt.ToUniversalTime()}, {text}, {post.Views},
                     {mediaUrls}, {forwardFrom}, {post.IsEdited})
                ON CONFLICT ("ChannelTag", "MessageId") DO NOTHING
                """, cancellationToken);
            }
        }
    }

}
