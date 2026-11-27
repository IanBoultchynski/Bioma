using Bioma.Application.Interfaces;
using Bioma.Client;
using Bioma.Infrastructure.Data;
using Bioma.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options => options.DetailedErrors = true);

builder.Services.AddScoped<UserSessionState>();

builder.Services.AddDbContext<BiomaDbContext>(options =>
{
    BiomaDbConfiguration.Configure(options, builder.Configuration, builder.Environment.ContentRootPath);
}, contextLifetime: ServiceLifetime.Transient, optionsLifetime: ServiceLifetime.Transient);

builder.Services.AddTransient<IUserProfileService, UserProfileService>();
builder.Services.AddTransient<IGachaService, GachaService>();
builder.Services.AddTransient<IQuizService, QuizService>();
builder.Services.AddTransient<IShowcase3DService, Showcase3DService>();
builder.Services.AddTransient<ISocialTradeService, SocialTradeService>();

builder.Services.AddHttpClient("ImageProxy", client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("BiomaVitrine3D/1.0");
});

builder.Services.AddHttpClient<IBiomaGameService, BiomaGameService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(8);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("BiomaSimplifiedApp/1.0");
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BiomaDbContext>();
    await BiomaDbSeeder.SeedAsync(db);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.MapGet("/css/{file}", async (string file, IWebHostEnvironment env) =>
{
    var safeFile = Path.GetFileName(file);
    var candidates = new[]
    {
        Path.Combine(env.ContentRootPath, "wwwroot", "css", safeFile),
        Path.Combine(AppContext.BaseDirectory, "wwwroot", "css", safeFile)
    };

    var path = candidates.FirstOrDefault(File.Exists);
    if (path is null)
    {
        return Results.NotFound();
    }

    var css = await File.ReadAllTextAsync(path);
    return Results.Content(css, "text/css; charset=utf-8");
});

app.MapGet("/media/{file}", (string file, IWebHostEnvironment env) =>
{
    var safeFile = Path.GetFileName(file);
    var candidates = new[]
    {
        Path.Combine(env.ContentRootPath, "wwwroot", "media", safeFile),
        Path.Combine(AppContext.BaseDirectory, "wwwroot", "media", safeFile)
    };

    var path = candidates.FirstOrDefault(File.Exists);
    if (path is null)
    {
        return Results.NotFound();
    }

    string contentType = Path.GetExtension(safeFile).ToLowerInvariant() switch
    {
        ".webm" => "video/webm",
        ".png"  => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        _ => "application/octet-stream"
    };

    return Results.File(path, contentType, enableRangeProcessing: true);
});

app.MapGet("/models/biomes/{file}", (string file, IWebHostEnvironment env) =>
{
    var safeFile = Path.GetFileName(file);
    var candidates = new[]
    {
        Path.Combine(env.ContentRootPath, "wwwroot", "models", "biomes", safeFile),
        Path.Combine(AppContext.BaseDirectory, "wwwroot", "models", "biomes", safeFile),
        Path.Combine(Showcase3DService.ResolveBiomesDirectory(), safeFile)
    };

    var path = candidates.FirstOrDefault(File.Exists);
    if (path is null)
    {
        return Results.NotFound();
    }

    string contentType = Path.GetExtension(safeFile).ToLowerInvariant() switch
    {
        ".glb"   => "model/gltf-binary",
        ".gltf"  => "model/gltf+json",
        ".blend" => "application/octet-stream",
        ".png"   => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".webp"  => "image/webp",
        ".svg"   => "image/svg+xml",
        _        => "application/octet-stream"
    };

    return Results.File(path, contentType, enableRangeProcessing: true);
});

app.MapGet("/js/{file}", async (string file, IWebHostEnvironment env) =>
{
    var safeFile = Path.GetFileName(file);
    var candidates = new[]
    {
        Path.Combine(env.ContentRootPath, "wwwroot", "js", safeFile),
        Path.Combine(AppContext.BaseDirectory, "wwwroot", "js", safeFile),
        Path.Combine(@"w:\bioma\frontend-blazor\Bioma.Client\wwwroot\js", safeFile)
    };

    var path = candidates.FirstOrDefault(File.Exists);
    if (path is null)
    {
        return Results.NotFound();
    }

    var js = await File.ReadAllTextAsync(path);
    return Results.Content(js, "application/javascript; charset=utf-8");
});

app.MapGet("/api/showcase/image-proxy", async (string url, IHttpClientFactory httpFactory) =>
{
    if (string.IsNullOrWhiteSpace(url) ||
        !Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
        (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
    {
        return Results.BadRequest();
    }

    if (uri.IsLoopback || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
    {
        return Results.BadRequest();
    }

    var cached = ShowcaseImageCache.GetOrNull(url);
    if (cached is not null)
    {
        return Results.File(cached.Value.Bytes, cached.Value.ContentType);
    }

    try
    {
        var client = httpFactory.CreateClient("ImageProxy");
        using var resp = await client.GetAsync(uri);
        if (!resp.IsSuccessStatusCode)
        {
            return Results.NotFound();
        }

        var contentType = resp.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
        if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest();
        }

        var bytes = await resp.Content.ReadAsByteArrayAsync();
        if (bytes.Length == 0 || bytes.Length > 8 * 1024 * 1024)
        {
            return Results.BadRequest();
        }

        ShowcaseImageCache.Set(url, bytes, contentType);
        return Results.File(bytes, contentType);
    }
    catch
    {
        return Results.NotFound();
    }
});

app.UseStaticFiles();
app.UseAntiforgery();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

file static class ShowcaseImageCache
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (byte[] Bytes, string ContentType)> Cache = new();

    public static (byte[] Bytes, string ContentType)? GetOrNull(string url) =>
        Cache.TryGetValue(url, out var entry) ? entry : null;

    public static void Set(string url, byte[] bytes, string contentType)
    {
        if (Cache.Count > 250)
        {
            Cache.Clear();
        }
        Cache[url] = (bytes, contentType);
    }
}
