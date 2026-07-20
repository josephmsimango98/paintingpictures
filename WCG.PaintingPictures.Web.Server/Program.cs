using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.FluentUI.AspNetCore.Components;
using WCG.PaintingPictures.Web.Server.Components;
using WCG.PaintingPictures.Web.Server.Models;
using WCG.PaintingPictures.Web.Server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();
builder.Services.AddFluentUIComponents();
builder.Services.AddSingleton<SupabaseDataService>();
builder.Services.AddSingleton<PortfolioService>();
builder.Services.AddSingleton<HeroGalleryService>();
builder.Services.AddSingleton<CollectionService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.Name = "pp.auth";
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Allow large portfolio uploads (HEIC phone photos) over HTTP multipart.
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 30 * 1024 * 1024;
});
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 30 * 1024 * 1024;
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();

// Login — real HTTP POST so HttpContext.SignInAsync is available
app.MapPost("/api/auth/login", async (
    HttpContext ctx,
    IConfiguration config,
    SupabaseDataService dataService) =>
{
    var form = await ctx.Request.ReadFormAsync();
    var email = form["email"].ToString();
    var password = form["password"].ToString();

    var url = config["Supabase:Url"];
    var key = config["Supabase:AnonKey"];
    var adminEmail = config["Supabase:AdminEmail"];

    if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(key))
        return Results.Redirect("/login?error=notconfigured");

    try
    {
        var client = new Supabase.Client(url, key,
            new Supabase.SupabaseOptions { AutoRefreshToken = false });
        var session = await client.Auth.SignIn(email, password);

        if (session?.User is null)
            return Results.Redirect("/login?error=invalid");

        UserProfile? profile = null;
        if (dataService.IsConfigured)
        {
            await dataService.EnsureViewerProfileAsync(
                session.User.Id!,
                session.User.Email!,
                session.User.Email?.Split('@')[0]);
            profile = await dataService.GetProfileAsync(session.User.Id!);
        }
        var role = profile?.Role is "admin" or "viewer"
            ? profile.Role
            : string.Equals(session.User.Email, adminEmail, StringComparison.OrdinalIgnoreCase)
                ? "admin"
                : "viewer";

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, session.User.Email!),
            new(ClaimTypes.NameIdentifier, session.User.Id!),
            new(ClaimTypes.Role, role),
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));

        return Results.Redirect("/");
    }
    catch
    {
        return Results.Redirect("/login?error=invalid");
    }
});

// Registration — new accounts always start as viewers.
app.MapPost("/api/auth/register", async (
    HttpContext ctx,
    IConfiguration config,
    SupabaseDataService dataService) =>
{
    var form = await ctx.Request.ReadFormAsync();
    var email = form["email"].ToString().Trim();
    var password = form["password"].ToString();
    var displayName = form["displayName"].ToString().Trim();

    var url = config["Supabase:Url"];
    var key = config["Supabase:AnonKey"];
    if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(key))
        return Results.Redirect("/register?error=notconfigured");

    try
    {
        var client = new Supabase.Client(url, key,
            new Supabase.SupabaseOptions { AutoRefreshToken = false });
        var session = await client.Auth.SignUp(email, password);
        if (session?.User is null)
            return Results.Redirect("/register?error=invalid");

        if (dataService.IsConfigured)
        {
            await dataService.EnsureViewerProfileAsync(
                session.User.Id!,
                email,
                displayName);
        }

        return Results.Redirect("/login?registered=true");
    }
    catch
    {
        return Results.Redirect("/register?error=invalid");
    }
});

// Logout
app.MapGet("/api/auth/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/");
});

// Admin image upload over HTTP (bypasses Blazor SignalR size limits).
app.MapPost("/api/admin/portfolio/upload", async (
    HttpRequest request,
    PortfolioService portfolio,
    CancellationToken cancellationToken) =>
{
    const long maxBytes = 25 * 1024 * 1024;
    if (!request.HasFormContentType)
        return Results.BadRequest(new { detail = "Expected a multipart file upload." });

    var form = await request.ReadFormAsync(cancellationToken);
    var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
    if (file is null || file.Length == 0)
        return Results.BadRequest(new { detail = "No file uploaded." });
    if (file.Length > maxBytes)
        return Results.BadRequest(new { detail = "Image must be 25 MB or smaller." });

    var extension = Path.GetExtension(file.FileName);
    var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif", ".heic", ".heif"
    };
    if (!allowed.Contains(extension))
        return Results.BadRequest(new { detail = "Please choose a JPEG, PNG, WebP, GIF, HEIC, or HEIF image." });

    try
    {
        await using var stream = file.OpenReadStream();
        var uploaded = await portfolio.UploadImageAsync(
            stream,
            file.FileName,
            file.ContentType ?? "",
            cancellationToken);

        return Results.Ok(new
        {
            url = uploaded.Url,
            displayUrl = uploaded.DisplayUrl,
            previewDataUrl = uploaded.PreviewDataUrl,
            width = uploaded.Width,
            height = uploaded.Height,
            isHeic = uploaded.IsHeic,
            latitude = uploaded.Latitude,
            longitude = uploaded.Longitude,
            takenAt = uploaded.TakenAt,
            cameraMake = uploaded.CameraMake,
            cameraModel = uploaded.CameraModel,
            inspectError = uploaded.InspectError,
            convertError = uploaded.ConvertError
        });
    }
    catch (Exception ex)
    {
        return Results.Problem(
            detail: ex.InnerException?.Message ?? ex.Message,
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Upload failed");
    }
})
.RequireAuthorization(policy => policy.RequireRole("admin"))
.DisableAntiforgery();

// Convert HEIC (and similar) originals to JPEG on demand for browser display.
app.MapGet("/api/media/{id:int}", async (
    int id,
    PortfolioService portfolio,
    CancellationToken cancellationToken) =>
{
    await portfolio.LoadAsync(cancellationToken);
    var item = portfolio.GetById(id);
    if (item is null || !item.HasImage)
        return Results.NotFound();

    try
    {
        var displayUrl = await portfolio.EnsureDisplayImageAsync(item, cancellationToken);
        return Results.Redirect(displayUrl);
    }
    catch (Exception ex)
    {
        return Results.Problem(
            detail: ex.Message,
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Could not prepare display image");
    }
});

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
