using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TelegramPostsArchive.Data;
using TelegramPostsArchive.Models;

namespace TelegramPostsArchive.Controllers
{
    [ApiController]
    [Route("channels/{channel_tag}/posts")]
    public sealed class PostsController(DataContext db) : ControllerBase
    {
        private const int PageSize = 20;
        private const int MaxPage = int.MaxValue / PageSize;

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
            if (!Regex.IsMatch(channelTag, @"\A[A-Za-z0-9_]{5,32}\z"))
                ModelState.AddModelError("channel_tag", "Use a channel tag containing 5 to 32 letters, digits, or underscores.");

            if (Request.Query.ContainsKey("from") && from == null)
                from = "";
            if (Request.Query.ContainsKey("to") && to == null)
                to = "";
            if (Request.Query.ContainsKey("page") && page == null)
                page = "";

            var fromDate = ParseDate(from, "from");
            var toDate = ParseDate(to, "to");
            if (fromDate > toDate)
                ModelState.AddModelError("to", "The to date must be on or after the from date.");

            var pageNumber = 1;
            if (page != null && (!int.TryParse(page, NumberStyles.None, CultureInfo.InvariantCulture, out pageNumber) ||
                pageNumber < 1 || pageNumber > MaxPage))
            {
                ModelState.AddModelError("page", $"Page must be an integer between 1 and {MaxPage}.");
            }

            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            channelTag = channelTag.ToLowerInvariant();
            if (!await db.TelegramChannels.AnyAsync(channel => channel.ChannelTag == channelTag, cancellationToken))
                return Problem(statusCode: StatusCodes.Status404NotFound, title: "Channel is not in the archive.");

            var posts = db.TelegramPosts.AsNoTracking().Where(post => post.ChannelTag == channelTag);
            if (fromDate.HasValue)
            {
                var lowerBound = new DateTimeOffset(fromDate.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
                posts = posts.Where(post => post.PostedAt >= lowerBound);
            }
            if (toDate.HasValue)
            {
                var upperBound = new DateTimeOffset(toDate.Value.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc));
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

        private DateOnly? ParseDate(string? value, string key)
        {
            if (value == null)
                return null;

            if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date))
                return date;

            ModelState.AddModelError(key, "Use a valid date in YYYY-MM-DD format.");
            return null;
        }
    }
}
