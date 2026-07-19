using WCG.PaintingPictures.Web.Server.Models;

namespace WCG.PaintingPictures.Web.Server.Services;

public class PortfolioService
{
    private readonly SupabaseDataService _supabase;

    public PortfolioService(SupabaseDataService supabase)
    {
        _supabase = supabase;
    }

    private readonly List<PortfolioItem> _items = new()
    {
        new()
        {
            Id = 1,
            Title = "Joseph — Study in Light",
            Type = "photograph",
            Image = "https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?w=800&h=1000&fit=crop",
            ImageWidth = 4,
            ImageHeight = 5,
            Description = "A portrait capturing Joseph in natural morning light. There's something about the way light falls on his face — honest, unguarded. This was taken during our conversation about memory.",
            Poem = "Light finds the edges,\nsilent and persistent,\nrevealing what was always there.",
            Thoughts = "Sometimes the best portraits are about being present with another person."
        },
        new()
        {
            Id = 2,
            Title = "Untitled Drawing",
            Type = "drawing",
            Image = "https://images.unsplash.com/photo-1578925078519-cf21a4eae3f7?w=1000&h=700&fit=crop",
            ImageWidth = 10,
            ImageHeight = 7,
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
            Image = "https://images.unsplash.com/photo-1506794778202-cad84cf45f1d?w=600&h=900&fit=crop",
            ImageWidth = 2,
            ImageHeight = 3,
            Description = "A detail study. Hands say more than faces sometimes. In these hands I see the work of a lifetime.",
            Thoughts = "Details are where intimacy lives."
        },
        new()
        {
            Id = 5,
            Title = "Studies in Ink",
            Type = "drawing",
            Image = "https://images.unsplash.com/photo-1460661419201-fd4cecdf8a8b?w=900&h=600&fit=crop",
            ImageWidth = 3,
            ImageHeight = 2,
            Description = "Quick sketches exploring movement and gesture. Speed allows for honesty."
        },
        new()
        {
            Id = 6,
            Title = "Before Autumn",
            Type = "photograph",
            Image = "https://images.unsplash.com/photo-1502920917128-1aa500764cbd?w=800&h=1200&fit=crop",
            ImageWidth = 2,
            ImageHeight = 3,
            Description = "Taken just before everything changes. A quality of light that feels like goodbye and beginning at once.",
            Poem = "Everything is leaving.\nEverything is arriving.\nI am standing in between,\nwatchful."
        }
    };

    public IReadOnlyList<PortfolioItem> GetAll() => _items.AsReadOnly();

    public IReadOnlyList<PortfolioItem> GetByType(string type) =>
        _items.Where(x => string.Equals(x.Type, type, StringComparison.OrdinalIgnoreCase)).ToList();

    public async Task<IReadOnlyList<PortfolioItem>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!_supabase.IsConfigured)
            return GetAll();

        try
        {
            var remoteItems = await _supabase.GetItemsAsync(cancellationToken);
            _items.Clear();
            _items.AddRange(remoteItems);
        }
        catch
        {
            // Keep the last known in-memory list if Supabase is temporarily unavailable.
        }

        return GetAll();
    }

    public Task<string> UploadImageAsync(
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default) =>
        _supabase.UploadImageAsync(content, fileName, contentType, cancellationToken);

    public PortfolioItem? GetById(int id) => _items.FirstOrDefault(x => x.Id == id);

    public PortfolioItem? GetNext(int id)
    {
        var index = _items.FindIndex(x => x.Id == id);
        return index >= 0 && index < _items.Count - 1 ? _items[index + 1] : null;
    }

    public PortfolioItem? GetPrevious(int id)
    {
        var index = _items.FindIndex(x => x.Id == id);
        return index > 0 ? _items[index - 1] : null;
    }

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

    public async Task AddAsync(PortfolioItem item, CancellationToken cancellationToken = default)
    {
        if (!_supabase.IsConfigured)
        {
            Add(item);
            return;
        }

        var created = await _supabase.CreateItemAsync(item, cancellationToken);
        if (created is not null)
            _items.Add(created);
    }

    public async Task UpdateAsync(PortfolioItem item, CancellationToken cancellationToken = default)
    {
        if (_supabase.IsConfigured)
            await _supabase.UpdateItemAsync(item, cancellationToken);
        Update(item);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        if (_supabase.IsConfigured)
            await _supabase.DeleteItemAsync(id, cancellationToken);
        Delete(id);
    }
}
