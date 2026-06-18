using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.FluentUI.AspNetCore.Components;
using WCG.PaintingPictures.Web.Server.Components;
using WCG.PaintingPictures.Web.Server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();
builder.Services.AddFluentUIComponents();
builder.Services.AddSingleton<PortfolioService>();

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
app.MapPost("/api/auth/login", async (HttpContext ctx, IConfiguration config) =>
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

        var role = string.Equals(session.User.Email, adminEmail,
            StringComparison.OrdinalIgnoreCase) ? "admin" : "user";

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, session.User.Email!),
            new(ClaimTypes.NameIdentifier, session.User.Id!),
            new(ClaimTypes.Role, role),
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));

        return Results.Redirect(role == "admin" ? "/admin" : "/");
    }
    catch
    {
        return Results.Redirect("/login?error=invalid");
    }
});

// Logout
app.MapGet("/api/auth/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/");
});

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
