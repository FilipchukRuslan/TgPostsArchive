using Microsoft.EntityFrameworkCore;
using TelegramPostsArchive.Data;
using TelegramPostsArchive.Data.Entities;
using TelegramPostsArchive.Models.Parser;
using TelegramPostsArchive.Services;

namespace TelegramPostsArchive.Services
{
    public sealed class TelegramPostStore(DataContext db)
    {
        public async Task SavePageAsync(ParsedPage page, CancellationToken cancellationToken = default)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await WritePageAsync(page, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        public async Task SaveBackfillPageAsync(
            ParsedPage page, int? expectedBefore, int nextBefore, bool completed,
            CancellationToken cancellationToken = default)
        {
            if (nextBefore <= 0 || expectedBefore.HasValue && nextBefore >= expectedBefore.Value)
                throw new ArgumentOutOfRangeException(nameof(nextBefore), "Backfill cursors must decrease.");

            var channelTag = page.ChannelUsername.ToLowerInvariant();
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var channel = await LockChannelAsync(channelTag, cancellationToken);
            if (channel.IsHistoricalCollectionCompleted || channel.LastProcessedMessageId != expectedBefore)
                throw new InvalidOperationException("Backfill progress changed during the fetch. Resume from the saved checkpoint.");

            await WritePageAsync(page, cancellationToken);
            await db.TelegramChannels.Where(row => row.ChannelTag == channelTag)
                .ExecuteUpdateAsync(update => update
                    .SetProperty(row => row.LastProcessedMessageId, nextBefore)
                    .SetProperty(row => row.IsHistoricalCollectionCompleted, completed), cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        public async Task<TelegramChannel> UpdateNewestCollectedMessageIdAsync(string channelTag, CancellationToken cancellationToken)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var channel = await LockChannelAsync(channelTag, cancellationToken);
            if (channel.NewestCollectedMessageId == null && channel.LastSuccessfulSyncAt == null)
            {
                // Persist the initial anchor before a catch-up can save any partial pages.
                var newest = await db.TelegramPosts.Where(post => post.ChannelTag == channelTag)
                    .MaxAsync(post => (int?)post.MessageId, cancellationToken);
                if (newest.HasValue)
                {
                    await db.TelegramChannels.Where(row => row.ChannelTag == channelTag)
                        .ExecuteUpdateAsync(update => update.SetProperty(row => row.NewestCollectedMessageId, newest),
                            cancellationToken);
                    channel.NewestCollectedMessageId = newest;
                }
            }

            await transaction.CommitAsync(cancellationToken);
            return channel;
        }

        public async Task SavePollPageAsync(
            ParsedPage page, TelegramChannel checkpoint, int? newest, bool completed, CancellationToken cancellationToken)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var channel = await LockChannelAsync(checkpoint.ChannelTag, cancellationToken);
            if (channel.NewestCollectedMessageId != checkpoint.NewestCollectedMessageId ||
                channel.LastSuccessfulSyncAt != checkpoint.LastSuccessfulSyncAt)
            {
                throw new InvalidOperationException("Polling progress changed during the fetch. Retry from the saved checkpoint.");
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

            // Acquire post locks in the same order when concurrent imports overlap.
            foreach (var post in page.Messages.OrderBy(post => post.TgMsgId))
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
                INSERT INTO "TelegramPosts" AS stored
                    ("ChannelTag", "MessageId", "PostedAt", "Text", "Views", "MediaUrls", "ForwardFrom", "IsEdited")
                VALUES
                    ({channelTag}, {post.TgMsgId}, {postedAt.ToUniversalTime()}, {text}, {post.Views},
                     {mediaUrls}, {forwardFrom}, {post.IsEdited})
                ON CONFLICT ("ChannelTag", "MessageId") DO UPDATE SET
                    "PostedAt" = EXCLUDED."PostedAt",
                    "Text" = COALESCE(EXCLUDED."Text", stored."Text"),
                    "Views" = COALESCE(EXCLUDED."Views", stored."Views"),
                    "MediaUrls" = CASE WHEN cardinality(EXCLUDED."MediaUrls") > 0
                                       THEN EXCLUDED."MediaUrls" ELSE stored."MediaUrls" END,
                    "ForwardFrom" = COALESCE(EXCLUDED."ForwardFrom", stored."ForwardFrom"),
                    "IsEdited" = COALESCE(EXCLUDED."IsEdited", stored."IsEdited")
                WHERE EXCLUDED."PostedAt" IS DISTINCT FROM stored."PostedAt"
                   OR (EXCLUDED."Text" IS NOT NULL AND EXCLUDED."Text" IS DISTINCT FROM stored."Text")
                   OR (EXCLUDED."Views" IS NOT NULL AND EXCLUDED."Views" IS DISTINCT FROM stored."Views")
                   OR (cardinality(EXCLUDED."MediaUrls") > 0 AND EXCLUDED."MediaUrls" IS DISTINCT FROM stored."MediaUrls")
                   OR (EXCLUDED."ForwardFrom" IS NOT NULL AND EXCLUDED."ForwardFrom" IS DISTINCT FROM stored."ForwardFrom")
                   OR (EXCLUDED."IsEdited" IS NOT NULL AND EXCLUDED."IsEdited" IS DISTINCT FROM stored."IsEdited")
                """, cancellationToken);
            }
        }
    }

}
