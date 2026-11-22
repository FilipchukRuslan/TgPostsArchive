using Microsoft.AspNetCore.Mvc;
using TelegramPostsArchive.Models;
using TelegramPostsArchive.Services;

namespace TelegramPostsArchive.Controllers
{
    [ApiController]
    [Route("channels/{channel_tag}/posts")]
    public sealed class PostsController(PostsService postsService, PostsValidationService validationService) : ControllerBase
    {
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

            var result = await postsService.GetAsync(channelTag, fromDate, toDate, q, pageNumber, cancellationToken);
            if (result == null)
                return Problem(statusCode: StatusCodes.Status404NotFound, title: "Channel is not in the archive.");

            return result;
        }
    }
}
