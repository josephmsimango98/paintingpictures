using System.Text.Json.Serialization;

namespace WCG.PaintingPictures.Web.Server.Models;

public static class HeroLayoutKeys
{
    public const string Editorial = "editorial";
    public const string Balanced = "balanced";
    public const string FeaturedStrip = "featured-strip";

    public static readonly IReadOnlyList<string> All =
    [
        Editorial,
        Balanced,
        FeaturedStrip
    ];

    public static bool IsValid(string? key) =>
        !string.IsNullOrWhiteSpace(key) && All.Contains(key, StringComparer.OrdinalIgnoreCase);

    public static string Normalize(string? key) =>
        IsValid(key) ? All.First(x => x.Equals(key, StringComparison.OrdinalIgnoreCase)) : Editorial;
}

public static class HeroSlotKeys
{
    public const string Featured = "featured";
    public const string Secondary = "secondary";
    public const string Tertiary = "tertiary";
    public const string Quaternary = "quaternary";

    public static readonly IReadOnlyList<string> All =
    [
        Featured,
        Secondary,
        Tertiary,
        Quaternary
    ];

    public static string LabelFor(string slotKey, string layoutKey) =>
        (HeroLayoutKeys.Normalize(layoutKey), slotKey) switch
        {
            (HeroLayoutKeys.Editorial, Featured) => "Featured",
            (HeroLayoutKeys.Editorial, Secondary) => "Wide panel",
            (HeroLayoutKeys.Editorial, Tertiary) => "Lower left",
            (HeroLayoutKeys.Editorial, Quaternary) => "Lower right",
            (HeroLayoutKeys.Balanced, Featured) => "Top left",
            (HeroLayoutKeys.Balanced, Secondary) => "Top right",
            (HeroLayoutKeys.Balanced, Tertiary) => "Bottom left",
            (HeroLayoutKeys.Balanced, Quaternary) => "Bottom right",
            (HeroLayoutKeys.FeaturedStrip, Featured) => "Featured strip",
            (HeroLayoutKeys.FeaturedStrip, Secondary) => "Bottom left",
            (HeroLayoutKeys.FeaturedStrip, Tertiary) => "Bottom center",
            (HeroLayoutKeys.FeaturedStrip, Quaternary) => "Bottom right",
            _ => slotKey
        };

    public static string CssModifier(string slotKey) => slotKey switch
    {
        Featured => "pp-hero-card--featured",
        Secondary => "pp-hero-card--secondary",
        Tertiary => "pp-hero-card--tertiary",
        Quaternary => "pp-hero-card--quaternary",
        _ => "pp-hero-card--featured"
    };
}

public sealed class HeroGallerySettingsRow
{
    [JsonPropertyName("id")]
    public int Id { get; set; } = 1;

    [JsonPropertyName("layout_key")]
    public string LayoutKey { get; set; } = HeroLayoutKeys.Editorial;

    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed class HeroGallerySlotRow
{
    [JsonPropertyName("slot_key")]
    public string SlotKey { get; set; } = "";

    [JsonPropertyName("portfolio_item_id")]
    public int? PortfolioItemId { get; set; }

    [JsonPropertyName("sort_order")]
    public int SortOrder { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed class HeroGallerySlotAssignment
{
    public string SlotKey { get; set; } = "";
    public int? PortfolioItemId { get; set; }
    public int SortOrder { get; set; }
    public PortfolioItem? Item { get; set; }

    public string Label(string layoutKey) => HeroSlotKeys.LabelFor(SlotKey, layoutKey);
}

public sealed class HeroGalleryConfig
{
    public string LayoutKey { get; set; } = HeroLayoutKeys.Editorial;
    public List<HeroGallerySlotAssignment> Slots { get; set; } = [];

    public static HeroGalleryConfig Empty(string layoutKey = HeroLayoutKeys.Editorial) =>
        new()
        {
            LayoutKey = HeroLayoutKeys.Normalize(layoutKey),
            Slots = HeroSlotKeys.All
                .Select((key, index) => new HeroGallerySlotAssignment
                {
                    SlotKey = key,
                    SortOrder = index + 1
                })
                .ToList()
        };
}
