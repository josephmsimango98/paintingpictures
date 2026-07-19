using ImageMagick;
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

    public IReadOnlyList<PortfolioItem> GetPublished() =>
        _items.Where(x => x.IsPublished).ToList();

    public IReadOnlyList<PortfolioItem> GetByType(string type, bool publishedOnly = false) =>
        _items
            .Where(x => string.Equals(x.Type, type, StringComparison.OrdinalIgnoreCase))
            .Where(x => !publishedOnly || x.IsPublished)
            .ToList();

    public async Task<IReadOnlyList<PortfolioItem>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!_supabase.IsConfigured)
            return GetAll();

        try
        {
            var remoteItems = await _supabase.GetItemsAsync(cancellationToken);
            _items.Clear();
            _items.AddRange(remoteItems.Where(x => x is not null));
        }
        catch
        {
            // Keep the last known in-memory list if Supabase is temporarily unavailable.
        }

        return GetAll();
    }

    public async Task<UploadedPortfolioImage> UploadImageAsync(
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        await using var source = new MemoryStream();
        await content.CopyToAsync(source, cancellationToken);
        var sourceBytes = source.ToArray();
        if (sourceBytes.Length == 0)
            throw new InvalidOperationException("The uploaded file was empty.");

        var extension = Path.GetExtension(fileName);
        var looksLikeHeic = IsHeicFile(extension, contentType, sourceBytes);

        using var image = LoadImage(sourceBytes, looksLikeHeic);
        image.AutoOrient();

        if (image.ColorSpace != ColorSpace.sRGB)
            image.ColorSpace = ColorSpace.sRGB;

        var width = checked((int)image.Width);
        var height = checked((int)image.Height);
        if (width <= 0 || height <= 0)
            throw new InvalidOperationException("Could not read image dimensions.");

        var isHeic = looksLikeHeic || IsHeicFormat(image.Format);
        byte[] uploadBytes;
        string uploadFileName;
        string uploadContentType;

        if (isHeic)
        {
            // Browsers cannot display HEIC; always store a JPEG copy.
            image.Quality = 90;
            uploadBytes = image.ToByteArray(MagickFormat.Jpeg);
            uploadFileName = Path.ChangeExtension(
                string.IsNullOrWhiteSpace(fileName) ? "upload.jpg" : fileName,
                ".jpg");
            uploadContentType = "image/jpeg";
        }
        else
        {
            uploadBytes = sourceBytes;
            uploadFileName = fileName;
            uploadContentType = string.IsNullOrWhiteSpace(contentType)
                ? "application/octet-stream"
                : contentType;
        }

        await using var upload = new MemoryStream(uploadBytes, writable: false);
        var url = await _supabase.UploadImageAsync(
            upload,
            uploadFileName,
            uploadContentType,
            cancellationToken);

        return new UploadedPortfolioImage(url, width, height, isHeic);
    }

    private static MagickImage LoadImage(byte[] sourceBytes, bool preferHeic)
    {
        try
        {
            if (preferHeic)
            {
                var settings = new MagickReadSettings { Format = MagickFormat.Heic };
                return new MagickImage(sourceBytes, settings);
            }

            return new MagickImage(sourceBytes);
        }
        catch (MagickException) when (preferHeic)
        {
            // Some phones label HEIF with a HEIC extension (or the reverse).
            var settings = new MagickReadSettings { Format = MagickFormat.Heif };
            return new MagickImage(sourceBytes, settings);
        }
        catch (MagickException ex)
        {
            throw new InvalidOperationException(
                "Could not read this image. If it is HEIC/HEIF, try exporting as JPEG, or re-upload after restarting the app so ImageMagick codecs are loaded.",
                ex);
        }
    }

    private static bool IsHeicFile(string? extension, string? contentType, byte[] bytes)
    {
        if (!string.IsNullOrWhiteSpace(extension)
            && (extension.Equals(".heic", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".heif", StringComparison.OrdinalIgnoreCase)))
            return true;

        if (!string.IsNullOrWhiteSpace(contentType)
            && (contentType.Equals("image/heic", StringComparison.OrdinalIgnoreCase)
                || contentType.Equals("image/heif", StringComparison.OrdinalIgnoreCase)
                || contentType.Equals("image/heic-sequence", StringComparison.OrdinalIgnoreCase)
                || contentType.Equals("image/heif-sequence", StringComparison.OrdinalIgnoreCase)))
            return true;

        // HEIF brand is often in bytes 4..11 as "ftypheic" / "ftypheif" / "ftypmif1".
        if (bytes.Length >= 12)
        {
            var brand = System.Text.Encoding.ASCII.GetString(bytes, 4, 8);
            if (brand.StartsWith("ftyp", StringComparison.OrdinalIgnoreCase)
                && (brand.Contains("heic", StringComparison.OrdinalIgnoreCase)
                    || brand.Contains("heif", StringComparison.OrdinalIgnoreCase)
                    || brand.Contains("mif1", StringComparison.OrdinalIgnoreCase)
                    || brand.Contains("msf1", StringComparison.OrdinalIgnoreCase)))
                return true;
        }

        return false;
    }

    private static bool IsHeicFormat(MagickFormat format)
    {
        var name = format.ToString();
        return name.Contains("Heic", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Heif", StringComparison.OrdinalIgnoreCase);
    }

    public PortfolioItem? GetById(int id, bool publishedOnly = false)
    {
        var item = _items.FirstOrDefault(x => x.Id == id);
        if (item is null)
            return null;
        if (publishedOnly && !item.IsPublished)
            return null;
        return item;
    }

    public PortfolioItem? GetNext(int id, bool publishedOnly = false)
    {
        var items = publishedOnly ? _items.Where(x => x.IsPublished).ToList() : _items;
        var index = items.FindIndex(x => x.Id == id);
        return index >= 0 && index < items.Count - 1 ? items[index + 1] : null;
    }

    public PortfolioItem? GetPrevious(int id, bool publishedOnly = false)
    {
        var items = publishedOnly ? _items.Where(x => x.IsPublished).ToList() : _items;
        var index = items.FindIndex(x => x.Id == id);
        return index > 0 ? items[index - 1] : null;
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

public sealed record UploadedPortfolioImage(
    string Url,
    int Width,
    int Height,
    bool WasConvertedFromHeic);
