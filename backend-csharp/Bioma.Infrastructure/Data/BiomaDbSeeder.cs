using Bioma.Domain.Entities;
using Bioma.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Bioma.Infrastructure.Data;

/// <summary>
/// Création automatique du schéma et initialisation des données de base (Espèces, Utilisateurs, Inventaire & Progression, Hotspots, Quiz).
/// </summary>
public static class BiomaDbSeeder
{
    public static async Task SeedAsync(BiomaDbContext db)
    {
        await db.Database.EnsureCreatedAsync();

        var foxCardId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var kingfisherCardId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        if (!await db.Cards.AnyAsync())
        {
            db.Cards.AddRange(
                new SpeciesCard
                {
                    Id = foxCardId,
                    CommonName = "Renard roux",
                    ScientificName = "Vulpes vulpes",
                    Family = "Canidae",
                    Kingdom = "Animalia",
                    SourceApi = "GBIF",
                    ExternalId = "gbif-2434559",
                    Latitude = 47.4684,
                    Longitude = -0.5562,
                    CountryOrPlace = "France (Vallée de la Loire)",
                    ObservedDate = "2025-04-12",
                    Rarity = CardRarity.PeuCommune,
                    Biome = BiomeType.ForetTemperee,
                    AverageWeightKg = 7.5,
                    DangerScore = 35,
                    HabitatDescription = "Forêts tempérées, lisières, bocages et plaines.",
                    BiologicalDescription = "Mammifère carnivore opportuniste de la famille des Canidés, doté d'une ouïe fine capable de détecter les rongeurs sous la neige.",
                    BaseAttack = 48,
                    BaseDefense = 38,
                    BaseSpeed = 62,
                    ImageUrl = "https://images.unsplash.com/photo-1474511320723-9a56873867b5?auto=format&fit=crop&w=600&q=80"
                },
                new SpeciesCard
                {
                    Id = kingfisherCardId,
                    CommonName = "Martin-pêcheur d'Europe",
                    ScientificName = "Alcedo atthis",
                    Family = "Alcedinidae",
                    Kingdom = "Animalia",
                    SourceApi = "iNaturalist",
                    ExternalId = "inat-4829104",
                    Latitude = 43.5512,
                    Longitude = 4.6214,
                    CountryOrPlace = "France (Camargue)",
                    ObservedDate = "2025-05-03",
                    Rarity = CardRarity.Rare,
                    Biome = BiomeType.ZoneHumide,
                    AverageWeightKg = 0.04,
                    DangerScore = 10,
                    HabitatDescription = "Rivières calmes, étangs poissonneux et roselières.",
                    BiologicalDescription = "Oiseau plongeur au plumage bleu-vert iridescent qui capture de petits poissons en piqué rapide depuis un perchoir.",
                    BaseAttack = 36,
                    BaseDefense = 25,
                    BaseSpeed = 78,
                    ImageUrl = "https://images.unsplash.com/photo-1444464666168-49d633b86797?auto=format&fit=crop&w=600&q=80"
                }
            );
            await db.SaveChangesAsync();
        }

        if (!await db.Players.AnyAsync())
        {
            var playerAlphaId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
            var playerBetaId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

            db.Players.AddRange(
                new PlayerProfile
                {
                    Id = playerAlphaId,
                    Username = "ExplorateurAlpha",
                    Email = "alpha@bioma.local",
                    PasswordHash = "DEMO_HASH_ALPHA",
                    AvatarUrl = "https://images.unsplash.com/photo-1534528741775-53994a69daeb?auto=format&fit=crop&w=200&q=80",
                    VirtualCurrency = 1250,
                    ExperiencePoints = 640,
                    Level = 4,
                    Energy = 100,
                    EloRating = 1120,
                    TotalVictories = 7,
                    TotalDefeats = 3,
                    PushNotificationsEnabled = true
                },
                new PlayerProfile
                {
                    Id = playerBetaId,
                    Username = "NaturalisteBeta",
                    Email = "beta@bioma.local",
                    PasswordHash = "DEMO_HASH_BETA",
                    AvatarUrl = "https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?auto=format&fit=crop&w=200&q=80",
                    VirtualCurrency = 800,
                    ExperiencePoints = 320,
                    Level = 2,
                    Energy = 90,
                    EloRating = 1040,
                    TotalVictories = 4,
                    TotalDefeats = 2,
                    PushNotificationsEnabled = true
                }
            );

            var alphaFoxInstanceId = Guid.Parse("cccccccc-1111-1111-1111-111111111111");
            var alphaKingfisherInstanceId = Guid.Parse("cccccccc-2222-2222-2222-222222222222");

            db.PlayerCards.AddRange(
                new PlayerCard
                {
                    Id = alphaFoxInstanceId,
                    PlayerId = playerAlphaId,
                    SpeciesCardId = foxCardId,
                    CustomNickname = "Goupil de Loire",
                    ExperiencePoints = 420,
                    Level = 5,
                    Stage = LifeStage.Adulte,
                    State = CardState.Disponible,
                    IsInActiveDeck = true,
                    CurrentAttack = 58,
                    CurrentDefense = 46,
                    CurrentSpeed = 70,
                    WeightKg = 7.8,
                    PolaroidFinish = "Or",
                    ObtainedVia = "Capture"
                },
                new PlayerCard
                {
                    Id = alphaKingfisherInstanceId,
                    PlayerId = playerAlphaId,
                    SpeciesCardId = kingfisherCardId,
                    CustomNickname = "Flèche d'Azur",
                    ExperiencePoints = 150,
                    Level = 2,
                    Stage = LifeStage.Juvenile,
                    State = CardState.Disponible,
                    IsInActiveDeck = true,
                    CurrentAttack = 38,
                    CurrentDefense = 28,
                    CurrentSpeed = 80,
                    WeightKg = 0.042,
                    PolaroidFinish = "Standard",
                    ObtainedVia = "Gacha"
                }
            );

            db.ShowcasePedestals.Add(
                new ShowcasePedestal
                {
                    OwnerPlayerId = playerAlphaId,
                    DisplayedPlayerCardId = alphaFoxInstanceId,
                    SlotIndex = 1,
                    PositionX = 0.0,
                    PositionY = 0.5,
                    PositionZ = -1.5
                }
            );

            await db.SaveChangesAsync();
        }

        if (!await db.Hotspots.AnyAsync())
        {
            db.Hotspots.AddRange(
                new BiodiversityHotspot
                {
                    Name = "Delta de la Camargue",
                    CountryOrRegion = "France",
                    Latitude = 43.5512,
                    Longitude = 4.6214,
                    Biome = BiomeType.ZoneHumide,
                    ActiveSeason = Season.Printemps,
                    SpeciesDensityScore = 88
                },
                new BiodiversityHotspot
                {
                    Name = "Bassin Amazonien (Manaus)",
                    CountryOrRegion = "Brésil",
                    Latitude = -3.1190,
                    Longitude = -60.0217,
                    Biome = BiomeType.ForetTropicale,
                    ActiveSeason = Season.Ete,
                    SpeciesDensityScore = 98
                },
                new BiodiversityHotspot
                {
                    Name = "Plaines du Serengeti",
                    CountryOrRegion = "Tanzanie",
                    Latitude = -2.3333,
                    Longitude = 34.8333,
                    Biome = BiomeType.Savane,
                    ActiveSeason = Season.Automne,
                    SpeciesDensityScore = 94
                }
            );
            await db.SaveChangesAsync();
        }

        if (!await db.QuizQuestions.AnyAsync())
        {
            db.QuizQuestions.AddRange(
                new BiologyQuizQuestion
                {
                    QuestionText = "Quel sens le Renard roux (Vulpes vulpes) utilise-t-il principalement pour localiser ses proies sous une épaisse couche de neige ?",
                    ChoiceA = "L'ouïe ultra-directionnelle",
                    ChoiceB = "L'écholocation par ultrasons",
                    ChoiceC = "La vision thermique infrarouge",
                    ChoiceD = "L'électroréception",
                    CorrectChoiceIndex = 0,
                    ScientificExplanation = "Le renard roux possède des pavillons auriculaires mobiles capables de localiser le bruissement d'un campagnol sous 50 cm de neige.",
                    TimeLimitSeconds = 15,
                    RewardCurrency = 60,
                    RewardXp = 30
                },
                new BiologyQuizQuestion
                {
                    QuestionText = "Dans quel biome principal le Martin-pêcheur d'Europe (Alcedo atthis) chasse-t-il ses proies ?",
                    ChoiceA = "La toundra arctique",
                    ChoiceB = "Les zones humides et cours d'eau douce",
                    ChoiceC = "Le désert aride",
                    ChoiceD = "La haute montagne rocheuse",
                    CorrectChoiceIndex = 1,
                    ScientificExplanation = "Le martin-pêcheur dépend d'eaux douces claires et calmes où il repère les petits poissons depuis une branche surplombant l'eau.",
                    TimeLimitSeconds = 15,
                    RewardCurrency = 50,
                    RewardXp = 25
                }
            );
            await db.SaveChangesAsync();
        }
    }
}
