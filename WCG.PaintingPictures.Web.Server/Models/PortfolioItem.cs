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

    [JsonPropertyName("image")]
    public string? Image { get; set; }

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

    [JsonIgnore]
    public string TypeLabel =>
        string.IsNullOrEmpty(Type) ? "" : char.ToUpper(Type[0]) + Type[1..];

    [JsonIgnore]
    public bool HasImage => !string.IsNullOrEmpty(Image);
}
