using System.Text.Json.Serialization;

namespace WCG.PaintingPictures.Web.Server.Models;

public class PortfolioItem
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "photograph";

    /// <summary>Original uploaded file URL (may be HEIC/HEIF).</summary>
    [JsonPropertyName("image")]
    public string? Image { get; set; }

    /// <summary>Browser-safe JPEG derivative, created when first viewed.</summary>
    [JsonPropertyName("image_display")]
    public string? ImageDisplay { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("poem")]
    public string? Poem { get; set; }

    [JsonPropertyName("thoughts")]
    public string? Thoughts { get; set; }

    [JsonPropertyName("image_width")]
    public int ImageWidth { get; set; } = 4;

    [JsonPropertyName("image_height")]
    public int ImageHeight { get; set; } = 3;

    [JsonPropertyName("latitude")]
    public double? Latitude { get; set; }

    [JsonPropertyName("longitude")]
    public double? Longitude { get; set; }

    [JsonPropertyName("taken_at")]
    public DateTimeOffset? TakenAt { get; set; }

    [JsonPropertyName("camera_make")]
    public string? CameraMake { get; set; }

    [JsonPropertyName("camera_model")]
    public string? CameraModel { get; set; }

    [JsonPropertyName("is_published")]
    public bool IsPublished { get; set; } = true;

    [JsonIgnore]
    public string TypeLabel =>
        string.IsNullOrEmpty(Type) ? "" : char.ToUpper(Type[0]) + Type[1..];

    [JsonIgnore]
    public bool HasImage => !string.IsNullOrEmpty(Image);

    [JsonIgnore]
    public string VisibilityLabel => IsPublished ? "Published" : "Draft";

    [JsonIgnore]
    public bool HasLocation => Latitude is not null && Longitude is not null;

    [JsonIgnore]
    public bool NeedsDisplayConversion =>
        HasImage && string.IsNullOrWhiteSpace(ImageDisplay) && IsHeicOrHeifUrl(Image);

    /// <summary>URL safe for &lt;img&gt; tags in browsers.</summary>
    [JsonIgnore]
    public string? DisplayImageUrl
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(ImageDisplay))
                return ImageDisplay;
            if (NeedsDisplayConversion && Id > 0)
                return $"/api/media/{Id}";
            return Image;
        }
    }

    [JsonIgnore]
    public string? MapsUrl =>
        HasLocation
            ? $"https://www.google.com/maps?q={Latitude!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)},{Longitude!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
            : null;

    public static bool IsHeicOrHeifUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return url.Contains(".heic", StringComparison.OrdinalIgnoreCase)
                || url.Contains(".heif", StringComparison.OrdinalIgnoreCase);

        var path = uri.AbsolutePath;
        return path.EndsWith(".heic", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".heif", StringComparison.OrdinalIgnoreCase);
    }
}
