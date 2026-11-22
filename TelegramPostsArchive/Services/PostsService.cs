using Microsoft.EntityFrameworkCore;
using TelegramPostsArchive.Data;
using TelegramPostsArchive.Models;

namespace TelegramPostsArchive.Services
{
    public sealed class PostsService(DataContext db)
    {
        private const int PageSize = PostsValidationService.PostsPageSize;

        public async Task<PostsPageResponse?> GetAsync(
            string channelTag, DateOnly? fromDate, DateOnly? toDate, string? q, int pageNumber,
            CancellationToken cancellationToken)
        {
            channelTag = channelTag.ToLowerInvariant();
            if (!await db.TelegramChannels.AnyAsync(channel => channel.ChannelTag == channelTag, cancellationToken))
                return null;

            var posts = db.TelegramPosts.AsNoTracking().Where(post => post.ChannelTag == channelTag);
            if (fromDate.HasValue)
            {
                var lowerBound = fromDate.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                posts = posts.Where(post => post.PostedAt >= lowerBound);
            }
            if (toDate.HasValue)
            {
                var upperBound = toDate.Value.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
                posts = posts.Where(post => post.PostedAt <= upperBound);
            }
            if (!string.IsNullOrWhiteSpace(q))
            {
                var pattern = "%" + q.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
                posts = posts.Where(post => post.Text != null && EF.Functions.ILike(post.Text, pattern, "\\"));
            }

            var items = await posts.OrderByDescending(post => post.PostedAt)
                .ThenByDescending(post => post.MessageId)
                .Skip((pageNumber - 1) * PageSize)
                .Take(PageSize + 1)
                .Select(post => new PostResponse(post.ChannelTag, post.MessageId, post.PostedAt, post.Text,
                    post.Views, post.MediaUrls, post.ForwardFrom, post.IsEdited,
                    "https://t.me/" + post.ChannelTag + "/" + post.MessageId))
                .ToListAsync(cancellationToken);
            var hasNextPage = items.Count > PageSize;
            if (hasNextPage)
                items.RemoveAt(PageSize);

            return new PostsPageResponse(pageNumber, PageSize, hasNextPage, items);
        }
    }
}
