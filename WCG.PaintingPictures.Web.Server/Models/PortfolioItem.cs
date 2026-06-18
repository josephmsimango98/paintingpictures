namespace WCG.PaintingPictures.Web.Server.Models;

public class PortfolioItem
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Type { get; set; } = "photograph";
    public string? Image { get; set; }
    public string? Description { get; set; }
    public string? Poem { get; set; }
    public string? Thoughts { get; set; }
    public string TypeLabel => Type.Length > 0 ? char.ToUpper(Type[0]) + Type[1..] : Type;
}
