using Bioma.Domain.Entities;
using Bioma.Domain.Enums;
using Bioma.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Bioma.Infrastructure.Data;

/// <summary>
/// Initialise la structure relationnelle de la base de données (PostgreSQL ou SQLite fallback)
/// et injecte automatiquement le socle de jeu indispensable (espèces réelles, comptes fondateurs,
/// hotspots d'expéditions et questions scientifiques).
/// </summary>
public static class BiomaDbSeeder
{
    public const string DefaultPassword = "Bioma2026!";

    public static async Task SeedAsync(BiomaDbContext db)
    {
        // 1. Génération automatique du schéma relationnel
        await db.Database.EnsureCreatedAsync();

        // 2. Starter-pack d'espèces scientifiques réelles (GBIF / iNaturalist)
        var fox = await db.Cards.FirstOrDefaultAsync(c => c.ScientificName == "Vulpes vulpes");
        if (fox is null)
        {
            fox = new SpeciesCard
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                CommonName = "Renard roux",
                ScientificName = "Vulpes vulpes",
                Family = "Canidae",
                Kingdom = "Animalia",
                SourceApi = "GBIF",
                ExternalId = "gbif-vulpes-01",
                ImageUrl = "https://images.unsplash.com/photo-1516934024742-b461fba47600?auto=format&fit=crop&w=800&q=80",
                Latitude = 46.52,
                Longitude = 2.45,
                CountryOrPlace = "France, Forêts tempérées",
                Rarity = CardRarity.Commune,
                Biome = BiomeType.ForetTemperee,
                AverageWeightKg = 7.5,
                DangerScore = 25,
                BaseAttack = 40,
                BaseDefense = 35,
                BaseSpeed = 55,
                HabitatDescription = "Forêts boréales, campagnes bocagères et lisières tempérées.",
                BiologicalDescription = "Canidé omnivore très adaptable doté d'une ouïe fine capable de repérer des rongeurs sous la neige."
            };
            db.Cards.Add(fox);
        }

        var lion = await db.Cards.FirstOrDefaultAsync(c => c.ScientificName == "Panthera leo");
        if (lion is null)
        {
            lion = new SpeciesCard
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                CommonName = "Lion d'Afrique",
                ScientificName = "Panthera leo",
                Family = "Felidae",
                Kingdom = "Animalia",
                SourceApi = "iNaturalist",
                ExternalId = "inat-lion-01",
                ImageUrl = "https://images.unsplash.com/photo-1546182990-dffeafbe841d?auto=format&fit=crop&w=800&q=80",
                Latitude = -2.33,
                Longitude = 34.83,
                CountryOrPlace = "Tanzanie, Parc National du Serengeti",
                Rarity = CardRarity.Rare,
                Biome = BiomeType.Savane,
                AverageWeightKg = 190.0,
                DangerScore = 85,
                BaseAttack = 80,
                BaseDefense = 65,
                BaseSpeed = 60,
                HabitatDescription = "Savanes herbeuses et plaines boisées ouvertes d'Afrique subsaharienne.",
                BiologicalDescription = "Grand prédateur grégaire chassant en harde organisée, doté d'une mâchoire puissante et d'un rugissement audible à 8 km."
            };
            db.Cards.Add(lion);
        }

        var eagle = await db.Cards.FirstOrDefaultAsync(c => c.ScientificName == "Haliaeetus leucocephalus");
        if (eagle is null)
        {
            eagle = new SpeciesCard
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                CommonName = "Pygargue à tête blanche",
                ScientificName = "Haliaeetus leucocephalus",
                Family = "Accipitridae",
                Kingdom = "Animalia",
                SourceApi = "GBIF",
                ExternalId = "gbif-eagle-01",
                ImageUrl = "https://images.unsplash.com/photo-1522069169874-c58ec4b76be5?auto=format&fit=crop&w=800&q=80",
                Latitude = 57.05,
                Longitude = -135.33,
                CountryOrPlace = "Alaska, États-Unis",
                Rarity = CardRarity.Rare,
                Biome = BiomeType.ForetTemperee,
                AverageWeightKg = 5.2,
                DangerScore = 50,
                BaseAttack = 65,
                BaseDefense = 45,
                BaseSpeed = 75,
                HabitatDescription = "Zones côtières, grands fleuves et lacs riches en poissons d'Amérique du Nord.",
                BiologicalDescription = "Rapace majestueux muni de deux fovéas par œil pour une acuité visuelle exceptionnelle en piqué aérien."
            };
            db.Cards.Add(eagle);
        }

        await db.SaveChangesAsync();

        // 3. Comptes de test de l'équipe (Ian & Mohamed Arfaoui)
        var hashedPwd = UserProfileService.HashPassword(DefaultPassword);

        var ianUser = await db.Players
            .Include(p => p.InventoryCards)
            .FirstOrDefaultAsync(p => p.Username == "Ian" || p.Email == "ian@bioma.fr");

        if (ianUser is null)
        {
            ianUser = new PlayerProfile
            {
                Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                Username = "Ian",
                Email = "ian@bioma.fr",
                PasswordHash = hashedPwd,
                AvatarUrl = UserProfileService.BuildDefaultAvatarSvg("Ian"),
                VirtualCurrency = 1000,
                ExperiencePoints = 250,
                Level = 2,
                Energy = 100,
                EloRating = 1100,
                TotalVictories = 5,
                TotalDefeats = 1,
                PushNotificationsEnabled = true,
                CreatedAt = DateTimeOffset.UtcNow,
                LastCloudSyncAt = DateTimeOffset.UtcNow
            };
            db.Players.Add(ianUser);
            await db.SaveChangesAsync();

            // Deck de départ pour Ian
            db.PlayerCards.AddRange(
                new PlayerCard
                {
                    PlayerId = ianUser.Id,
                    SpeciesCardId = fox.Id,
                    CustomNickname = "Goupil",
                    Level = 2,
                    ExperiencePoints = 120,
                    Stage = LifeStage.Adulte,
                    State = CardState.Disponible,
                    IsInActiveDeck = true,
                    CurrentAttack = 44,
                    CurrentDefense = 38,
                    CurrentSpeed = 58,
                    WeightKg = 7.8,
                    PolaroidFinish = "Standard",
                    ObtainedVia = "Starter"
                },
                new PlayerCard
                {
                    PlayerId = ianUser.Id,
                    SpeciesCardId = lion.Id,
                    CustomNickname = "Simba",
                    Level = 1,
                    ExperiencePoints = 40,
                    Stage = LifeStage.Juvenile,
                    State = CardState.Disponible,
                    IsInActiveDeck = true,
                    CurrentAttack = 80,
                    CurrentDefense = 65,
                    CurrentSpeed = 60,
                    WeightKg = 185.0,
                    PolaroidFinish = "Bronze",
                    ObtainedVia = "Starter"
                },
                new PlayerCard
                {
                    PlayerId = ianUser.Id,
                    SpeciesCardId = eagle.Id,
                    CustomNickname = "Aquila",
                    Level = 1,
                    ExperiencePoints = 20,
                    Stage = LifeStage.Juvenile,
                    State = CardState.Disponible,
                    IsInActiveDeck = true,
                    CurrentAttack = 65,
                    CurrentDefense = 45,
                    CurrentSpeed = 75,
                    WeightKg = 5.2,
                    PolaroidFinish = "Standard",
                    ObtainedVia = "Starter"
                }
            );
            await db.SaveChangesAsync();
        }

        var arfaoumoUser = await db.Players
            .Include(p => p.InventoryCards)
            .FirstOrDefaultAsync(p => p.Username == "Arfaoumo" || p.Email == "arfaoumo@bioma.fr");

        if (arfaoumoUser is null)
        {
            arfaoumoUser = new PlayerProfile
            {
                Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                Username = "Arfaoumo",
                Email = "arfaoumo@bioma.fr",
                PasswordHash = hashedPwd,
                AvatarUrl = UserProfileService.BuildDefaultAvatarSvg("Arfaoumo"),
                VirtualCurrency = 1000,
                ExperiencePoints = 250,
                Level = 2,
                Energy = 100,
                EloRating = 1100,
                TotalVictories = 5,
                TotalDefeats = 1,
                PushNotificationsEnabled = true,
                CreatedAt = DateTimeOffset.UtcNow,
                LastCloudSyncAt = DateTimeOffset.UtcNow
            };
            db.Players.Add(arfaoumoUser);
            await db.SaveChangesAsync();

            // Deck de départ pour Mohamed
            db.PlayerCards.AddRange(
                new PlayerCard
                {
                    PlayerId = arfaoumoUser.Id,
                    SpeciesCardId = lion.Id,
                    CustomNickname = "Aslan",
                    Level = 2,
                    ExperiencePoints = 140,
                    Stage = LifeStage.Adulte,
                    State = CardState.Disponible,
                    IsInActiveDeck = true,
                    CurrentAttack = 88,
                    CurrentDefense = 72,
                    CurrentSpeed = 64,
                    WeightKg = 195.0,
                    PolaroidFinish = "Or",
                    ObtainedVia = "Starter"
                },
                new PlayerCard
                {
                    PlayerId = arfaoumoUser.Id,
                    SpeciesCardId = eagle.Id,
                    CustomNickname = "Faucon",
                    Level = 2,
                    ExperiencePoints = 110,
                    Stage = LifeStage.Adulte,
                    State = CardState.Disponible,
                    IsInActiveDeck = true,
                    CurrentAttack = 70,
                    CurrentDefense = 50,
                    CurrentSpeed = 80,
                    WeightKg = 5.4,
                    PolaroidFinish = "Standard",
                    ObtainedVia = "Starter"
                },
                new PlayerCard
                {
                    PlayerId = arfaoumoUser.Id,
                    SpeciesCardId = fox.Id,
                    CustomNickname = "Renart",
                    Level = 1,
                    ExperiencePoints = 30,
                    Stage = LifeStage.Juvenile,
                    State = CardState.Disponible,
                    IsInActiveDeck = true,
                    CurrentAttack = 40,
                    CurrentDefense = 35,
                    CurrentSpeed = 55,
                    WeightKg = 7.5,
                    PolaroidFinish = "Standard",
                    ObtainedVia = "Starter"
                }
            );
            await db.SaveChangesAsync();
        }

        // 4. Hotspots d'expéditions (Épique 3 : Globe 3D)
        if (!await db.Hotspots.AnyAsync())
        {
            db.Hotspots.AddRange(
                new BiodiversityHotspot
                {
                    Name = "Parc National de Yellowstone",
                    CountryOrRegion = "États-Unis",
                    Latitude = 44.428,
                    Longitude = -110.588,
                    Biome = BiomeType.ForetTemperee,
                    ActiveSeason = Season.Ete,
                    SpeciesDensityScore = 88
                },
                new BiodiversityHotspot
                {
                    Name = "Plaines du Serengeti",
                    CountryOrRegion = "Tanzanie",
                    Latitude = -2.333,
                    Longitude = 34.833,
                    Biome = BiomeType.Savane,
                    ActiveSeason = Season.Printemps,
                    SpeciesDensityScore = 96
                },
                new BiodiversityHotspot
                {
                    Name = "Bassin de l'Amazone",
                    CountryOrRegion = "Brésil",
                    Latitude = -3.465,
                    Longitude = -62.215,
                    Biome = BiomeType.ForetTropicale,
                    ActiveSeason = Season.Automne,
                    SpeciesDensityScore = 99
                },
                new BiodiversityHotspot
                {
                    Name = "Grande Barrière de Corail",
                    CountryOrRegion = "Australie",
                    Latitude = -18.287,
                    Longitude = 147.699,
                    Biome = BiomeType.ZoneHumide,
                    ActiveSeason = Season.Ete,
                    SpeciesDensityScore = 94
                }
            );
            await db.SaveChangesAsync();
        }

        // 5. Questions de Quiz Chronométré (Épique 4)
        if (!await db.QuizQuestions.AnyAsync())
        {
            db.QuizQuestions.AddRange(
                new BiologyQuizQuestion
                {
                    QuestionText = "Quel mammifère marin détient le record absolu de profondeur de plongée en apnée (plus de 2 000 m) ?",
                    ChoiceA = "Grand requin blanc",
                    ChoiceB = "Grand cachalot",
                    ChoiceC = "Baleine bleue",
                    ChoiceD = "Orque",
                    CorrectChoiceIndex = 1,
                    ScientificExplanation = "Le Grand cachalot (Physeter macrocephalus) plonge couramment au-delà de 2 000 mètres de profondeur pour chasser les calmars géants.",
                    TimeLimitSeconds = 15,
                    RewardCurrency = 50,
                    RewardXp = 25
                },
                new BiologyQuizQuestion
                {
                    QuestionText = "À quelle famille scientifique appartient le Renard roux (Vulpes vulpes) ?",
                    ChoiceA = "Felidae",
                    ChoiceB = "Ursidae",
                    ChoiceC = "Canidae",
                    ChoiceD = "Mustelidae",
                    CorrectChoiceIndex = 2,
                    ScientificExplanation = "Le renard roux appartient à la famille des Canidés, aux côtés du loup gris et du dingo.",
                    TimeLimitSeconds = 15,
                    RewardCurrency = 50,
                    RewardXp = 25
                },
                new BiologyQuizQuestion
                {
                    QuestionText = "Quelle adaptation visuelle remarquable permet au Pygargue d'avoir une vision si perçante en vol ?",
                    ChoiceA = "Deux fovéas distinctes par œil",
                    ChoiceB = "Vision thermique infrarouge",
                    ChoiceC = "Un œil dorsal supplémentaire",
                    ChoiceD = "Écholocalisation par ultrasons",
                    CorrectChoiceIndex = 0,
                    ScientificExplanation = "La plupart des grands rapaces ont deux fovéas par œil, ce qui leur permet une vue panoramique tout en zoomant sur des détails au sol.",
                    TimeLimitSeconds = 15,
                    RewardCurrency = 50,
                    RewardXp = 25
                }
            );
            await db.SaveChangesAsync();
        }
    }
}
