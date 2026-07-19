using WCG.PaintingPictures.Web.Server.Models;

namespace WCG.PaintingPictures.Web.Server.Services;

public sealed class HeroGalleryService
{
    private readonly SupabaseDataService _supabase;
    private readonly PortfolioService _portfolio;
    private HeroGalleryConfig? _cache;

    public HeroGalleryService(SupabaseDataService supabase, PortfolioService portfolio)
    {
        _supabase = supabase;
        _portfolio = portfolio;
    }

    public async Task<HeroGalleryConfig> GetConfigAsync(CancellationToken cancellationToken = default)
    {
        var items = await _portfolio.LoadAsync(cancellationToken) ?? [];
        if (!_supabase.IsConfigured)
            return BuildFallback(items);

        try
        {
            var settings = await _supabase.GetHeroSettingsAsync(cancellationToken);
            var slots = await _supabase.GetHeroSlotsAsync(cancellationToken);
            if (settings is null || slots.Count == 0)
                return BuildFallback(items);

            var config = MapConfig(settings, slots, items);
            if (config.Slots.All(x => x.Item is null && x.PortfolioItemId is null))
                return BuildFallback(items);

            _cache = config;
            return config;
        }
        catch
        {
            return _cache ?? BuildFallback(items);
        }
    }

    public async Task SaveConfigAsync(HeroGalleryConfig config, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);

        var layoutKey = HeroLayoutKeys.Normalize(config.LayoutKey);
        var slots = NormalizeSlots(config.Slots);

        ValidateAssignments(slots);

        if (!_supabase.IsConfigured)
        {
            _cache = new HeroGalleryConfig
            {
                LayoutKey = layoutKey,
                Slots = slots
            };
            return;
        }

        await _supabase.SaveHeroConfigAsync(layoutKey, slots, cancellationToken);
        var items = await _portfolio.LoadAsync(cancellationToken);
        _cache = new HeroGalleryConfig
        {
            LayoutKey = layoutKey,
            Slots = slots.Select(slot =>
            {
                slot.Item = slot.PortfolioItemId is int id
                    ? items.FirstOrDefault(x => x.Id == id)
                    : null;
                return slot;
            }).ToList()
        };
    }

    public static HeroGalleryConfig BuildFallback(IReadOnlyList<PortfolioItem>? items)
    {
        var safe = (items ?? [])
            .Where(i => i is not null)
            .ToList();

        var photographs = safe
            .Where(i => string.Equals(i.Type, "photograph", StringComparison.OrdinalIgnoreCase) && i.HasImage)
            .ToList();
        var drawings = safe
            .Where(i => string.Equals(i.Type, "drawing", StringComparison.OrdinalIgnoreCase) && i.HasImage)
            .ToList();
        var poem = safe.FirstOrDefault(i => string.Equals(i.Type, "poem", StringComparison.OrdinalIgnoreCase));
        var featured = photographs.ElementAtOrDefault(0) ?? safe.FirstOrDefault(i => i.HasImage);
        var drawing = drawings.ElementAtOrDefault(0);
        var photography = photographs.ElementAtOrDefault(1) ?? photographs.ElementAtOrDefault(0);

        var config = HeroGalleryConfig.Empty(HeroLayoutKeys.Editorial);
        Assign(config, HeroSlotKeys.Featured, featured);
        Assign(config, HeroSlotKeys.Secondary, poem);
        Assign(config, HeroSlotKeys.Tertiary, drawing);
        Assign(config, HeroSlotKeys.Quaternary, photography);
        return config;
    }

    private static void Assign(HeroGalleryConfig config, string slotKey, PortfolioItem? item)
    {
        var slot = config.Slots.First(x => x.SlotKey == slotKey);
        slot.PortfolioItemId = item?.Id;
        slot.Item = item;
    }

    private static HeroGalleryConfig MapConfig(
        HeroGallerySettingsRow settings,
        IReadOnlyList<HeroGallerySlotRow> slots,
        IReadOnlyList<PortfolioItem> items)
    {
        var config = HeroGalleryConfig.Empty(settings.LayoutKey);
        foreach (var row in slots)
        {
            var slot = config.Slots.FirstOrDefault(x => x.SlotKey == row.SlotKey);
            if (slot is null)
                continue;

            slot.PortfolioItemId = row.PortfolioItemId;
            slot.SortOrder = row.SortOrder;
            slot.Item = row.PortfolioItemId is int id
                ? items.FirstOrDefault(x => x.Id == id)
                : null;
        }

        return config;
    }

    private static List<HeroGallerySlotAssignment> NormalizeSlots(IEnumerable<HeroGallerySlotAssignment> slots)
    {
        var byKey = slots
            .Where(x => HeroSlotKeys.All.Contains(x.SlotKey))
            .GroupBy(x => x.SlotKey)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        return HeroSlotKeys.All
            .Select((key, index) =>
            {
                byKey.TryGetValue(key, out var existing);
                return new HeroGallerySlotAssignment
                {
                    SlotKey = key,
                    PortfolioItemId = existing?.PortfolioItemId,
                    SortOrder = index + 1,
                    Item = existing?.Item
                };
            })
            .ToList();
    }

    private static void ValidateAssignments(IReadOnlyList<HeroGallerySlotAssignment> slots)
    {
        var assigned = slots
            .Where(x => x.PortfolioItemId is > 0)
            .Select(x => x.PortfolioItemId!.Value)
            .ToList();

        if (assigned.Count != assigned.Distinct().Count())
            throw new InvalidOperationException("Each portfolio item can only be assigned to one hero slot.");
    }
}
