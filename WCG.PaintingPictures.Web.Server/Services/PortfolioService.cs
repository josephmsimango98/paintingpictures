using WCG.PaintingPictures.Web.Server.Models;

namespace WCG.PaintingPictures.Web.Server.Services;

public class PortfolioService
{
    private readonly List<PortfolioItem> _items = new()
    {
        new()
        {
            Id = 1,
            Title = "Joseph — Study in Light",
            Type = "photograph",
            Image = "https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?w=800&h=800&fit=crop",
            Description = "A portrait capturing Joseph in natural morning light. There's something about the way light falls on his face — honest, unguarded. This was taken during our conversation about memory.",
            Poem = "Light finds the edges,\nsilent and persistent,\nrevealing what was always there.",
            Thoughts = "Sometimes the best portraits are about being present with another person."
        },
        new()
        {
            Id = 2,
            Title = "Untitled Drawing",
            Type = "drawing",
            Image = "https://images.unsplash.com/photo-1578925078519-cf21a4eae3f7?w=800&h=800&fit=crop",
            Description = "Ink on paper. A study of form and negative space. These marks were made without intention — just the movement of hand and breath.",
            Thoughts = "I'm drawn to imperfection. The wavering line, the unexpected smudge — these are the most honest parts."
        },
        new()
        {
            Id = 3,
            Title = "Absence",
            Type = "poem",
            Poem = "I am learning to sit with silence.\nNot the silence of absence,\nbut the silence of listening.\n\nThe world is speaking\nin colors I'm only now beginning to see.",
            Description = "A meditation on presence and what we notice when we stop looking."
        },
        new()
        {
            Id = 4,
            Title = "Joseph — Hands",
            Type = "photograph",
            Image = "https://images.unsplash.com/photo-1506794778202-cad84cf45f1d?w=800&h=800&fit=crop",
            Description = "A detail study. Hands say more than faces sometimes. In these hands I see the work of a lifetime.",
            Thoughts = "Details are where intimacy lives."
        },
        new()
        {
            Id = 5,
            Title = "Studies in Ink",
            Type = "drawing",
            Image = "https://images.unsplash.com/photo-1460661419201-fd4cecdf8a8b?w=800&h=800&fit=crop",
            Description = "Quick sketches exploring movement and gesture. Speed allows for honesty."
        },
        new()
        {
            Id = 6,
            Title = "Before Autumn",
            Type = "photograph",
            Image = "https://images.unsplash.com/photo-1502920917128-1aa500764cbd?w=800&h=800&fit=crop",
            Description = "Taken just before everything changes. A quality of light that feels like goodbye and beginning at once.",
            Poem = "Everything is leaving.\nEverything is arriving.\nI am standing in between,\nwatchful."
        }
    };

    public IReadOnlyList<PortfolioItem> GetAll() => _items.AsReadOnly();

    public PortfolioItem? GetById(int id) => _items.FirstOrDefault(x => x.Id == id);

    public void Add(PortfolioItem item)
    {
        item.Id = _items.Count > 0 ? _items.Max(x => x.Id) + 1 : 1;
        _items.Add(item);
    }

    public void Update(PortfolioItem item)
    {
        var index = _items.FindIndex(x => x.Id == item.Id);
        if (index >= 0) _items[index] = item;
    }

    public void Delete(int id) => _items.RemoveAll(x => x.Id == id);
}
