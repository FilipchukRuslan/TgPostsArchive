using System.Text.Json;

namespace TelegramPostsArchive.Models.Parser
{
    public class ParserOptions
    {
        public Dictionary<string, double> ViewMultipliers { get; set; } =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["k"] = 1_000,
            ["m"] = 1_000_000,
            ["b"] = 1_000_000_000
        };

        public static ParserOptions LoadRules(string path)
        {
            if (!File.Exists(path))
                return new ParserOptions();

            using var document = JsonDocument.Parse(File.ReadAllText(path));

            var options = new ParserOptions();

            if (document.RootElement.TryGetProperty(
                    "view_multipliers", out var multipliers))
            {
                options.ViewMultipliers.Clear();

                foreach (var property in multipliers.EnumerateObject())
                {
                    options.ViewMultipliers[property.Name] =
                        property.Value.GetDouble();
                }
            }

            return options;
        }
    }
}
