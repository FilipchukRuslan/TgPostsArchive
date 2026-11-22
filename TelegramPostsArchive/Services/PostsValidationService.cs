using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace TelegramPostsArchive.Services
{
    public class PostsValidationService
    {
        public const int PostsPageSize = 20;
        private const int MaxPage = int.MaxValue / PostsPageSize;

        public (DateOnly? FromDate, DateOnly? ToDate, int PageNumber) Validate(
            string channelTag, string? from, string? to, string? page,
            IQueryCollection query, ModelStateDictionary modelState)
        {
            if (!Regex.IsMatch(channelTag, @"\A[A-Za-z0-9_]{5,32}\z"))
                modelState.AddModelError("channel_tag", "Use a channel tag containing 5 to 32 letters, digits, or underscores.");

            if (query.ContainsKey("from") && from == null)
                from = "";
            if (query.ContainsKey("to") && to == null)
                to = "";
            if (query.ContainsKey("page") && page == null)
                page = "";

            var fromDate = ParseDate(from, "from", modelState);
            var toDate = ParseDate(to, "to", modelState);
            if (fromDate > toDate)
                modelState.AddModelError("to", "The to date must be on or after the from date.");

            var pageNumber = 1;
            if (page != null && (!int.TryParse(page, NumberStyles.None, CultureInfo.InvariantCulture, out pageNumber) ||
                pageNumber < 1 || pageNumber > MaxPage))
            {
                modelState.AddModelError("page", $"Page must be an integer between 1 and {MaxPage}.");
            }

            return (fromDate, toDate, pageNumber);
        }

        private static DateOnly? ParseDate(string? value, string key, ModelStateDictionary modelState)
        {
            if (value == null)
                return null;

            if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date))
                return date;

            modelState.AddModelError(key, "Use a valid date in YYYY-MM-DD format.");
            return null;
        }
    }
}