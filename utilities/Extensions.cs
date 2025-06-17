using Humanizer;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;
using Spectre.Console;

static partial class CosmosExtensions
{
    internal static async Task<string> GetResultJsonAsync(this Container container, string query)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(query);

        using FeedIterator<dynamic> iterator = container.GetItemQueryIterator<dynamic>(query);

        List<dynamic> results = [];
        while (iterator.HasMoreResults)
        {
            FeedResponse<dynamic> response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }

        return JsonConvert.SerializeObject(
            results,
            Formatting.Indented,
            new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
            }
        );
    }
}

static partial class StringExtensions
{
    internal static string GetHeader(this string title) =>
        $"{title.Humanize().Transform(To.TitleCase)} functions";

    internal static string[] GetDisplayNames(this (string Title, string Group) item)
    {
        List<string> values = [
            .. new string[] {
                item.Group,
                item.Group.Humanize().Transform(To.TitleCase),
                item.Group.Dehumanize().Kebaberize(),
                item.Group.Dehumanize().Pascalize(),
                item.Title.Transform(To.LowerCase).Humanize().Transform(To.TitleCase),
                item.Title.Transform(To.LowerCase).Kebaberize(),
                item.Title.Transform(To.LowerCase).Pascalize()
            },
            .. item.Group.Humanize().Transform(To.TitleCase).Split(" "),
            .. item.Title.Transform(To.LowerCase).Humanize().Transform(To.TitleCase).Split(" ")
        ];

        return [.. values.Select(v => v.Transform(To.LowerCase)).Distinct().Order()];
    }
}