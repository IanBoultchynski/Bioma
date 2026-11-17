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

app.UseStaticFiles();
app.UseAntiforgery();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
