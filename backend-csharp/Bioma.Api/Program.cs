using Bioma.Application.Interfaces;
using Bioma.Infrastructure.Data;
using Bioma.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddDbContext<BiomaDbContext>(options =>
{
    BiomaDbConfiguration.Configure(options, builder.Configuration, builder.Environment.ContentRootPath);
});

builder.Services.AddScoped<IUserProfileService, UserProfileService>();

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

app.MapGet("/health", async (BiomaDbContext db) => Results.Ok(new
{
    status = "healthy",
    service = "Bioma.Api (Back-Office C# & Base de données)",
    databaseProvider = db.Database.ProviderName,
    playersCount = await db.Players.CountAsync(),
    speciesCardsCount = await db.Cards.CountAsync(),
    playerCardsCount = await db.PlayerCards.CountAsync(),
    hotspotsCount = await db.Hotspots.CountAsync(),
    quizQuestionsCount = await db.QuizQuestions.CountAsync(),
    connectors = new[] { "GBIF API v1", "iNaturalist API v1" }
}));

app.MapGet("/api/database/status", async (BiomaDbContext db) =>
{
    var players = await db.Players
        .Include(p => p.InventoryCards)
            .ThenInclude(pc => pc.SpeciesCard)
        .Select(p => new
        {
            p.Id,
            p.Username,
            p.Email,
            p.Level,
            p.ExperiencePoints,
            p.VirtualCurrency,
            p.Energy,
            p.EloRating,
            p.TotalVictories,
            p.TotalDefeats,
            p.PushNotificationsEnabled,
            Cards = p.InventoryCards.Select(pc => new
            {
                pc.Id,
                Species = pc.SpeciesCard != null ? pc.SpeciesCard.CommonName : "",
                ScientificName = pc.SpeciesCard != null ? pc.SpeciesCard.ScientificName : "",
                pc.CustomNickname,
                pc.Level,
                pc.ExperiencePoints,
                Stage = pc.Stage.ToString(),
                State = pc.State.ToString(),
                pc.IsInActiveDeck,
                pc.CurrentAttack,
                pc.CurrentDefense,
                pc.CurrentSpeed,
                pc.PolaroidFinish,
                pc.ObtainedVia
            })
        })
        .ToListAsync();

    return Results.Ok(new
    {
        provider = db.Database.ProviderName,
        tables = new[]
        {
            "Players", "SpeciesCards", "PlayerCards", "CaptureAttempts",
            "BiodiversityHotspots", "ExpeditionMissions", "BiologyQuizQuestions",
            "QuizAttempts", "GachaPulls", "LaboratoryFusions", "AuctionListings",
            "AuctionBids", "Friendships", "TradeOffers", "BattleMatches", "ShowcasePedestals"
        },
        players
    });
});

app.MapControllers();

app.Run();
