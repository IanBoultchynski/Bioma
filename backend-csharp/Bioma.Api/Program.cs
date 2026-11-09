using Bioma.Application.Interfaces;
using Bioma.Infrastructure.Data;
using Bioma.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var postgresConn = builder.Configuration.GetConnectionString("Postgres");
builder.Services.AddDbContext<BiomaDbContext>(options =>
{
    if (!string.IsNullOrWhiteSpace(postgresConn))
    {
        options.UseNpgsql(postgresConn);
    }
    else
    {
        options.UseInMemoryDatabase("BiomaSimplifiedBackOfficeDb");
    }
});

builder.Services.AddHttpClient<IBiomaGameService, BiomaGameService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(8);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("BiomaSimplifiedApp/1.0");
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BiomaDbContext>();
    await BiomaDbSeeder.SeedAsync(db);
}

app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    service = "Bioma.Api (Début Back-Office C#)",
    connectors = new[] { "GBIF API v1", "iNaturalist API v1" }
}));

app.MapControllers();

app.Run();
