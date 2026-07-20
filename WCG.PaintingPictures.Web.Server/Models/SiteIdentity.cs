using System.Text.Json.Serialization;

namespace WCG.PaintingPictures.Web.Server.Models;

public sealed class SiteIdentity
{
    [JsonPropertyName("id")]
    public int Id { get; set; } = 1;

    [JsonPropertyName("site_title")]
    public string SiteTitle { get; set; } = "The Irony";

    [JsonPropertyName("artist_name")]
    public string ArtistName { get; set; } = "Joseph Msimango";

    [JsonPropertyName("statement")]
    public string Statement { get; set; } =
        "Painting Pictures is a visual expression of memory and light — photographs, drawings, and poems gathered as one way of seeing.";

    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }

    public static SiteIdentity Defaults { get; } = new();
}
