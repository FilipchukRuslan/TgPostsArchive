using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TelegramPostsArchive.Data;
using TelegramPostsArchive.Models;
using TelegramPostsArchive.Services;

namespace TelegramPostsArchive.Controllers
{
    [ApiController]
    [Route("channels/{channel_tag}/posts")]
    public sealed class PostsController(DataContext db, PostsValidationService validationService) : ControllerBase
    {
        private const int PageSize = PostsValidationService.PostsPageSize;

        [HttpGet]
        [ProducesResponseType<PostsPageResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<PostsPageResponse>> Get(
            [FromRoute(Name = "channel_tag")] string channelTag,
            [FromQuery] string? from,
            [FromQuery] string? to,
            [FromQuery] string? q,
            [FromQuery] string? page,
            CancellationToken cancellationToken)
        {
            var (fromDate, toDate, pageNumber) = validationService.Validate(
                channelTag, from, to, page, Request.Query, ModelState);

            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            channelTag = channelTag.ToLowerInvariant();
            if (!await db.TelegramChannels.AnyAsync(channel => channel.ChannelTag == channelTag, cancellationToken))
                return Problem(statusCode: StatusCodes.Status404NotFound, title: "Channel is not in the archive.");

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
