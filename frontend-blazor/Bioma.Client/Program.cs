using Bioma.Application.Interfaces;
using Bioma.Client;
using Bioma.Infrastructure.Data;
using Bioma.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddDbContext<BiomaDbContext>(options =>
{
    BiomaDbConfiguration.Configure(options, builder.Configuration, builder.Environment.ContentRootPath);
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

app.MapGet("/css/bioma.css", async (IWebHostEnvironment env) =>
{
    var candidates = new[]
    {
        Path.Combine(env.ContentRootPath, "wwwroot", "css", "bioma.css"),
        Path.Combine(AppContext.BaseDirectory, "wwwroot", "css", "bioma.css")
    };

    var path = candidates.FirstOrDefault(File.Exists);
    if (path is null)
    {
        return Results.NotFound();
    }

    var css = await File.ReadAllTextAsync(path);
    return Results.Content(css, "text/css; charset=utf-8");
});

app.UseStaticFiles();
app.UseAntiforgery();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
