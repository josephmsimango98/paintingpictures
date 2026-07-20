using WCG.PaintingPictures.Web.Server.Models;

namespace WCG.PaintingPictures.Web.Server.Services;

public sealed class SiteIdentityService
{
    private readonly SupabaseDataService _supabase;
    private SiteIdentity _cache = SiteIdentity.Defaults;
    private DateTimeOffset _cachedAt = DateTimeOffset.MinValue;
    private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(30);

    public SiteIdentityService(SupabaseDataService supabase)
    {
        _supabase = supabase;
    }

    public async Task<SiteIdentity> GetAsync(CancellationToken cancellationToken = default)
    {
        if (DateTimeOffset.UtcNow - _cachedAt < CacheFor)
            return Clone(_cache);

        if (!_supabase.IsConfigured)
            return Clone(SiteIdentity.Defaults);

        try
        {
            var remote = await _supabase.GetSiteIdentityAsync(cancellationToken);
            _cache = remote ?? SiteIdentity.Defaults;
            _cachedAt = DateTimeOffset.UtcNow;
        }
        catch
        {
            // Keep last known / defaults if Supabase is temporarily unavailable.
            if (_cachedAt == DateTimeOffset.MinValue)
                _cache = SiteIdentity.Defaults;
        }

        return Clone(_cache);
    }

    public async Task SaveAsync(SiteIdentity identity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);

        identity.SiteTitle = string.IsNullOrWhiteSpace(identity.SiteTitle)
            ? "The Irony"
            : identity.SiteTitle.Trim();
        identity.ArtistName = string.IsNullOrWhiteSpace(identity.ArtistName)
            ? "Joseph Msimango"
            : identity.ArtistName.Trim();
        identity.Statement = string.IsNullOrWhiteSpace(identity.Statement)
            ? SiteIdentity.Defaults.Statement
            : identity.Statement.Trim();
        identity.Id = 1;

        if (_supabase.IsConfigured)
            await _supabase.SaveSiteIdentityAsync(identity, cancellationToken);

        _cache = Clone(identity);
        _cachedAt = DateTimeOffset.UtcNow;
    }

    private static SiteIdentity Clone(SiteIdentity source) => new()
    {
        Id = source.Id,
        SiteTitle = source.SiteTitle,
        ArtistName = source.ArtistName,
        Statement = source.Statement,
        UpdatedAt = source.UpdatedAt
    };
}
