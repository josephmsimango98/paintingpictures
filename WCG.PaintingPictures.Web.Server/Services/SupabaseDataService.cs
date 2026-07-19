using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using WCG.PaintingPictures.Web.Server.Models;

namespace WCG.PaintingPictures.Web.Server.Services;

public sealed class SupabaseDataService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string? _url;
    private readonly string? _serviceRoleKey;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public SupabaseDataService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _url = configuration["Supabase:Url"]?.TrimEnd('/');
        _serviceRoleKey = configuration["Supabase:ServiceRoleKey"];
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_url) && !string.IsNullOrWhiteSpace(_serviceRoleKey);

    public async Task<IReadOnlyList<PortfolioItem>> GetItemsAsync(CancellationToken cancellationToken = default) =>
        await GetAsync<PortfolioItem>("portfolio_items?select=*&order=id.asc", cancellationToken);

    public async Task<PortfolioItem?> CreateItemAsync(
        PortfolioItem item,
        CancellationToken cancellationToken = default)
    {
        var payload = ToItemPayload(item, includeId: false);
        var created = await SendAsync<List<PortfolioItem>>(
            HttpMethod.Post,
            "portfolio_items",
            payload,
            "return=representation",
            cancellationToken);
        return created?.FirstOrDefault();
    }

    public async Task UpdateItemAsync(PortfolioItem item, CancellationToken cancellationToken = default) =>
        _ = await SendAsync<JsonElement>(
            HttpMethod.Patch,
            $"portfolio_items?id=eq.{item.Id}",
            ToItemPayload(item, includeId: false),
            "return=minimal",
            cancellationToken);

    public async Task DeleteItemAsync(int id, CancellationToken cancellationToken = default) =>
        _ = await SendAsync<JsonElement>(
            HttpMethod.Delete,
            $"portfolio_items?id=eq.{id}",
            null,
            "return=minimal",
            cancellationToken);

    public async Task<IReadOnlyList<PortfolioCollection>> GetCollectionsAsync(
        CancellationToken cancellationToken = default) =>
        await GetAsync<PortfolioCollection>(
            "collections?select=*&order=medium.asc,sort_order.asc,title.asc",
            cancellationToken);

    public async Task<IReadOnlyList<CollectionItemRow>> GetCollectionItemsAsync(
        CancellationToken cancellationToken = default) =>
        await GetAsync<CollectionItemRow>(
            "collection_items?select=*&order=sort_order.asc",
            cancellationToken);

    public async Task<PortfolioCollection?> CreateCollectionAsync(
        PortfolioCollection collection,
        CancellationToken cancellationToken = default)
    {
        var created = await SendAsync<List<PortfolioCollection>>(
            HttpMethod.Post,
            "collections",
            ToCollectionPayload(collection),
            "return=representation",
            cancellationToken);
        return created?.FirstOrDefault();
    }

    public async Task UpdateCollectionAsync(
        PortfolioCollection collection,
        CancellationToken cancellationToken = default) =>
        _ = await SendAsync<JsonElement>(
            HttpMethod.Patch,
            $"collections?id=eq.{collection.Id}",
            ToCollectionPayload(collection),
            "return=minimal",
            cancellationToken);

    public async Task DeleteCollectionAsync(long id, CancellationToken cancellationToken = default) =>
        _ = await SendAsync<JsonElement>(
            HttpMethod.Delete,
            $"collections?id=eq.{id}",
            null,
            "return=minimal",
            cancellationToken);

    public async Task ReplaceCollectionItemsAsync(
        long collectionId,
        IReadOnlyList<int> itemIds,
        CancellationToken cancellationToken = default)
    {
        _ = await SendAsync<JsonElement>(
            HttpMethod.Delete,
            $"collection_items?collection_id=eq.{collectionId}",
            null,
            "return=minimal",
            cancellationToken);

        if (itemIds.Count == 0)
            return;

        var payload = itemIds
            .Select((itemId, index) => new
            {
                collection_id = collectionId,
                portfolio_item_id = itemId,
                sort_order = index + 1
            })
            .ToArray();

        _ = await SendAsync<JsonElement>(
            HttpMethod.Post,
            "collection_items",
            payload,
            "return=minimal",
            cancellationToken);
    }

    public async Task<HeroGallerySettingsRow?> GetHeroSettingsAsync(CancellationToken cancellationToken = default)
    {
        var rows = await GetAsync<HeroGallerySettingsRow>(
            "hero_gallery_settings?id=eq.1&select=*&limit=1",
            cancellationToken);
        return rows.FirstOrDefault();
    }

    public async Task<IReadOnlyList<HeroGallerySlotRow>> GetHeroSlotsAsync(
        CancellationToken cancellationToken = default) =>
        await GetAsync<HeroGallerySlotRow>(
            "hero_gallery_slots?select=*&order=sort_order.asc",
            cancellationToken);

    public async Task SaveHeroConfigAsync(
        string layoutKey,
        IReadOnlyList<HeroGallerySlotAssignment> slots,
        CancellationToken cancellationToken = default)
    {
        var normalizedLayout = HeroLayoutKeys.Normalize(layoutKey);
        _ = await SendAsync<JsonElement>(
            HttpMethod.Post,
            "hero_gallery_settings?on_conflict=id",
            new
            {
                id = 1,
                layout_key = normalizedLayout,
                updated_at = DateTimeOffset.UtcNow
            },
            "resolution=merge-duplicates,return=minimal",
            cancellationToken);

        foreach (var slot in slots)
        {
            _ = await SendAsync<JsonElement>(
                HttpMethod.Post,
                "hero_gallery_slots?on_conflict=slot_key",
                new
                {
                    slot_key = slot.SlotKey,
                    portfolio_item_id = slot.PortfolioItemId,
                    sort_order = slot.SortOrder,
                    updated_at = DateTimeOffset.UtcNow
                },
                "resolution=merge-duplicates,return=minimal",
                cancellationToken);
        }
    }

    public async Task<UserProfile?> GetProfileAsync(string userId, CancellationToken cancellationToken = default)
    {
        var profiles = await GetAsync<UserProfile>(
            $"profiles?id=eq.{Uri.EscapeDataString(userId)}&select=*&limit=1",
            cancellationToken);
        return profiles.FirstOrDefault();
    }

    public async Task<IReadOnlyList<UserProfile>> GetProfilesAsync(CancellationToken cancellationToken = default) =>
        await GetAsync<UserProfile>("profiles?select=*&order=created_at.desc", cancellationToken);

    public async Task EnsureViewerProfileAsync(
        string userId,
        string email,
        string? displayName,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            id = userId,
            email,
            display_name = string.IsNullOrWhiteSpace(displayName) ? email.Split('@')[0] : displayName,
            role = "viewer"
        };
        _ = await SendAsync<JsonElement>(
            HttpMethod.Post,
            "profiles?on_conflict=id",
            payload,
            "resolution=ignore-duplicates,return=minimal",
            cancellationToken);
    }

    public async Task UpdateRoleAsync(
        string userId,
        string role,
        CancellationToken cancellationToken = default)
    {
        if (role is not ("admin" or "viewer"))
            throw new ArgumentOutOfRangeException(nameof(role));

        _ = await SendAsync<JsonElement>(
            HttpMethod.Patch,
            $"profiles?id=eq.{Uri.EscapeDataString(userId)}",
            new { role },
            "return=minimal",
            cancellationToken);
    }

    public async Task<EngagementSummary> GetEngagementAsync(
        int itemId,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            return new EngagementSummary(0, false, []);

        var likes = await GetAsync<LikeRow>(
            $"likes?item_id=eq.{itemId}&select=user_id",
            cancellationToken);
        var comments = await GetAsync<PortfolioComment>(
            $"comments?item_id=eq.{itemId}&select=*&order=created_at.desc",
            cancellationToken);

        return new EngagementSummary(
            likes.Count,
            userId is not null && likes.Any(x => x.UserId == userId),
            comments);
    }

    public async Task ToggleLikeAsync(
        int itemId,
        string userId,
        bool currentlyLiked,
        CancellationToken cancellationToken = default)
    {
        if (currentlyLiked)
        {
            _ = await SendAsync<JsonElement>(
                HttpMethod.Delete,
                $"likes?item_id=eq.{itemId}&user_id=eq.{Uri.EscapeDataString(userId)}",
                null,
                "return=minimal",
                cancellationToken);
        }
        else
        {
            _ = await SendAsync<JsonElement>(
                HttpMethod.Post,
                "likes",
                new { item_id = itemId, user_id = userId },
                "resolution=ignore-duplicates,return=minimal",
                cancellationToken);
        }
    }

    public async Task AddCommentAsync(
        int itemId,
        string userId,
        string authorName,
        string body,
        CancellationToken cancellationToken = default)
    {
        var cleanBody = body.Trim();
        if (cleanBody.Length is < 1 or > 1000)
            throw new ArgumentException("Comments must be between 1 and 1000 characters.", nameof(body));

        _ = await SendAsync<JsonElement>(
            HttpMethod.Post,
            "comments",
            new
            {
                item_id = itemId,
                user_id = userId,
                author_name = authorName,
                body = cleanBody
            },
            "return=minimal",
            cancellationToken);
    }

    public async Task<string> UploadImageAsync(
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("Supabase service role access is not configured.");

        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(extension))
            extension = GuessExtension(contentType);

        var objectPath = $"portfolio/{DateTime.UtcNow:yyyyMMdd}/{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{_url}/storage/v1/object/{Uri.EscapeDataString(PortfolioBucket)}/{objectPath}");
        request.Headers.TryAddWithoutValidation("apikey", _serviceRoleKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _serviceRoleKey);
        request.Headers.TryAddWithoutValidation("x-upsert", "true");
        request.Content = new StreamContent(content);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);

        using var response = await _httpClientFactory.CreateClient().SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return $"{_url}/storage/v1/object/public/{PortfolioBucket}/{objectPath}";
    }

    private const string PortfolioBucket = "portfolio";

    private static string GuessExtension(string contentType) => contentType.ToLowerInvariant() switch
    {
        "image/jpeg" or "image/jpg" => ".jpg",
        "image/png" => ".png",
        "image/webp" => ".webp",
        "image/gif" => ".gif",
        _ => ".bin"
    };

    private async Task<IReadOnlyList<T>> GetAsync<T>(
        string path,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured)
            return [];

        using var request = CreateRequest(HttpMethod.Get, path);
        using var response = await _httpClientFactory.CreateClient().SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<List<T>>(_json, cancellationToken) ?? [];
    }

    private async Task<T?> SendAsync<T>(
        HttpMethod method,
        string path,
        object? payload,
        string prefer,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("Supabase service role access is not configured.");

        using var request = CreateRequest(method, path);
        request.Headers.TryAddWithoutValidation("Prefer", prefer);
        if (payload is not null)
        {
            request.Content = new StringContent(
                JsonSerializer.Serialize(payload, _json),
                Encoding.UTF8,
                "application/json");
        }

        using var response = await _httpClientFactory.CreateClient().SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        if (response.Content.Headers.ContentLength is 0)
            return default;

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(content)
            ? default
            : JsonSerializer.Deserialize<T>(content, _json);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, $"{_url}/rest/v1/{path}");
        request.Headers.TryAddWithoutValidation("apikey", _serviceRoleKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _serviceRoleKey);
        return request;
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        var details = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException(
            $"Supabase request failed ({(int)response.StatusCode}): {details}",
            null,
            response.StatusCode);
    }

    private static object ToItemPayload(PortfolioItem item, bool includeId)
    {
        var values = new Dictionary<string, object?>
        {
            ["title"] = item.Title.Trim(),
            ["type"] = item.Type,
            ["image"] = item.Image,
            ["description"] = item.Description,
            ["poem"] = item.Poem,
            ["thoughts"] = item.Thoughts,
            ["image_width"] = Math.Max(1, item.ImageWidth),
            ["image_height"] = Math.Max(1, item.ImageHeight)
        };
        if (includeId)
            values["id"] = item.Id;
        return values;
    }

    private static object ToCollectionPayload(PortfolioCollection collection) => new
    {
        title = collection.Title.Trim(),
        slug = collection.Slug.Trim().ToLowerInvariant(),
        medium = PortfolioMedia.Normalize(collection.Medium),
        description = string.IsNullOrWhiteSpace(collection.Description)
            ? null
            : collection.Description.Trim(),
        cover_item_id = collection.CoverItemId,
        sort_order = Math.Max(0, collection.SortOrder),
        updated_at = DateTimeOffset.UtcNow
    };

    private sealed class LikeRow
    {
        [System.Text.Json.Serialization.JsonPropertyName("user_id")]
        public string UserId { get; set; } = "";
    }
}
