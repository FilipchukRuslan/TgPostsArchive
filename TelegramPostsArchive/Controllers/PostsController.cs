using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace TelegramPostsArchive.Controllers
{
    [Route("channels/{channel_tag}/posts")]
    [ApiController]
    public class PostsController : ControllerBase
    {
    }
}
