using System.Text.RegularExpressions;
using WCG.PaintingPictures.Web.Server.Models;

namespace WCG.PaintingPictures.Web.Server.Services;

public sealed partial class CollectionService
{
    private readonly SupabaseDataService _supabase;
    private readonly PortfolioService _portfolio;
    private readonly List<PortfolioCollection> _collections = [];

    public CollectionService(SupabaseDataService supabase, PortfolioService portfolio)
    {
        _supabase = supabase;
        _portfolio = portfolio;
    }

    public async Task<IReadOnlyList<PortfolioCollection>> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        var items = await _portfolio.LoadAsync(cancellationToken);

        if (_supabase.IsConfigured)
        {
            try
            {
                var collections = await _supabase.GetCollectionsAsync(cancellationToken);
                var memberships = await _supabase.GetCollectionItemsAsync(cancellationToken);
                _collections.Clear();
                _collections.AddRange(collections.Where(x => x is not null));

                foreach (var collection in _collections)
                {
                    collection.ItemIds = memberships
                        .Where(x => x.CollectionId == collection.Id)
                        .OrderBy(x => x.SortOrder)
                        .Select(x => x.PortfolioItemId)
                        .Distinct()
                        .ToList();
                }
            }
            catch
            {
                // Keep the last known collection list when Supabase is unavailable.
            }
        }

        AttachCovers(_collections, items);
        return _collections.AsReadOnly();
    }

    public async Task<IReadOnlyList<PortfolioCollection>> GetByMediumAsync(
        string medium,
        CancellationToken cancellationToken = default)
    {
        var normalized = PortfolioMedia.Normalize(medium);
        var all = await LoadAsync(cancellationToken);
        return all
            .Where(x => x.Medium.Equals(normalized, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Title)
            .ToList();
    }

    public async Task<PortfolioCollection?> GetBySlugAsync(
        string medium,
        string slug,
        CancellationToken cancellationToken = default)
    {
        var normalized = PortfolioMedia.Normalize(medium);
        var all = await LoadAsync(cancellationToken);
        return all.FirstOrDefault(x =>
            x.Medium.Equals(normalized, StringComparison.OrdinalIgnoreCase) &&
            x.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<IReadOnlyList<PortfolioItem>> GetItemsAsync(
        PortfolioCollection collection,
        CancellationToken cancellationToken = default)
    {
        var allItems = await _portfolio.LoadAsync(cancellationToken);
        var byId = allItems.ToDictionary(x => x.Id);
        return collection.ItemIds
            .Where(byId.ContainsKey)
            .Select(id => byId[id])
            .Where(item => item.Type.Equals(collection.Medium, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public async Task SaveAsync(
        PortfolioCollection collection,
        IReadOnlyList<int> selectedItemIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(collection);

        collection.Title = collection.Title.Trim();
        if (collection.Title.Length == 0)
            throw new InvalidOperationException("Collection title is required.");

        collection.Medium = PortfolioMedia.Normalize(collection.Medium);
        collection.Slug = Slugify(string.IsNullOrWhiteSpace(collection.Slug)
            ? collection.Title
            : collection.Slug);

        var allItems = await _portfolio.LoadAsync(cancellationToken);
        var validIds = allItems
            .Where(x => x.Type.Equals(collection.Medium, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Id)
            .ToHashSet();
        var itemIds = selectedItemIds
            .Where(validIds.Contains)
            .Distinct()
            .ToList();

        if (collection.CoverItemId is not null && !itemIds.Contains(collection.CoverItemId.Value))
            collection.CoverItemId = itemIds.FirstOrDefault() is var first && first > 0 ? first : null;

        var duplicate = _collections.Any(x =>
            x.Id != collection.Id &&
            x.Medium.Equals(collection.Medium, StringComparison.OrdinalIgnoreCase) &&
            x.Slug.Equals(collection.Slug, StringComparison.OrdinalIgnoreCase));
        if (duplicate)
            throw new InvalidOperationException("A collection with this name already exists for that medium.");

        if (_supabase.IsConfigured)
        {
            if (collection.Id == 0)
            {
                var created = await _supabase.CreateCollectionAsync(collection, cancellationToken)
                    ?? throw new InvalidOperationException("Supabase did not return the created collection.");
                collection.Id = created.Id;
                collection.CreatedAt = created.CreatedAt;
            }
            else
            {
                await _supabase.UpdateCollectionAsync(collection, cancellationToken);
            }

            await _supabase.ReplaceCollectionItemsAsync(collection.Id, itemIds, cancellationToken);
        }
        else if (collection.Id == 0)
        {
            collection.Id = _collections.Count == 0 ? 1 : _collections.Max(x => x.Id) + 1;
            collection.CreatedAt = DateTimeOffset.UtcNow;
        }

        collection.ItemIds = itemIds;
        var index = _collections.FindIndex(x => x.Id == collection.Id);
        if (index >= 0)
            _collections[index] = collection;
        else
            _collections.Add(collection);

        AttachCovers([collection], allItems);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        if (_supabase.IsConfigured)
            await _supabase.DeleteCollectionAsync(id, cancellationToken);
        _collections.RemoveAll(x => x.Id == id);
    }

    public static string Slugify(string value)
    {
        var slug = NonSlugCharacters().Replace(value.Trim().ToLowerInvariant(), "-");
        return slug.Trim('-');
    }

    private static void AttachCovers(
        IEnumerable<PortfolioCollection> collections,
        IReadOnlyList<PortfolioItem> items)
    {
        foreach (var collection in collections)
        {
            var coverId = collection.CoverItemId
                ?? collection.ItemIds.FirstOrDefault();
            collection.CoverItem = coverId > 0
                ? items.FirstOrDefault(x => x.Id == coverId)
                : null;
        }
    }

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex NonSlugCharacters();
}
