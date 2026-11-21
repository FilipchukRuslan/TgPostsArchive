namespace TelegramPostsArchive.Models
{
    public class PostsPageResponse
    {
        public PostsPageResponse(int page, int pageSize, bool hasNextPage, List<PostResponse> items)
        {
            Page = page;
            PageSize = pageSize;
            HasNextPage = hasNextPage;
            Items = items;
        }

        public int Page { get; set; }
        public int PageSize { get; set; }
        public bool HasNextPage { get; set; }
        public List<PostResponse> Items { get; set; } = [];
    }
}
