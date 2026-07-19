using System.Text.Json.Serialization;

namespace WCG.PaintingPictures.Web.Server.Models;

public static class PortfolioMedia
{
    public const string Photograph = "photograph";
    public const string Drawing = "drawing";
    public const string Poem = "poem";

    public static readonly IReadOnlyList<string> All = [Photograph, Drawing, Poem];

    public static string Normalize(string? value) =>
        All.FirstOrDefault(x => x.Equals(value, StringComparison.OrdinalIgnoreCase))
        ?? Photograph;

    public static string PluralLabel(string? value) => Normalize(value) switch
    {
        Drawing => "Drawings",
        Poem => "Poems",
        _ => "Photographs"
    };
}

public sealed class PortfolioCollection
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("slug")]
    public string Slug { get; set; } = "";

    [JsonPropertyName("medium")]
    public string Medium { get; set; } = PortfolioMedia.Photograph;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("cover_item_id")]
    public int? CoverItemId { get; set; }

    [JsonPropertyName("sort_order")]
    public int SortOrder { get; set; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonIgnore]
    public IReadOnlyList<int> ItemIds { get; set; } = [];

    [JsonIgnore]
    public PortfolioItem? CoverItem { get; set; }

    [JsonIgnore]
    public int ItemCount => ItemIds.Count;
}

public sealed class CollectionItemRow
{
    [JsonPropertyName("collection_id")]
    public long CollectionId { get; set; }

    [JsonPropertyName("portfolio_item_id")]
    public int PortfolioItemId { get; set; }

    [JsonPropertyName("sort_order")]
    public int SortOrder { get; set; }
}
