using System.Text.Json;
using Bioma.Domain.Entities;
using Bioma.Domain.Enums;
using Bioma.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace Bioma.Infrastructure.Data;

/// <summary>
/// Initialise la structure relationnelle de la base de données (PostgreSQL Supabase ou SQLite)
/// et alimente dynamiquement le catalogue d'espèces en interrogeant directement l'API publique iNaturalist / GBIF
/// pour récupérer les vraies photographies d'espèces, les vrais noms vernaculaires français et le vrai nombre
/// d'observations dans la nature (US-28, US-30).
/// </summary>
public static class BiomaDbSeeder
{
    public const string DefaultPassword = "Bioma2026!";

    /// <summary>
    /// Liste des taxons scientifiques à synchroniser dynamiquement depuis l'API iNaturalist / GBIF.
    /// Aucune photo ni statistique n'est figée en dur : tout est résolu depuis l'API.
    /// </summary>
    private static readonly string[] TargetSpeciesScientificNames =
    [
        "Vulpes vulpes",
        "Panthera leo",
        "Haliaeetus leucocephalus",
        "Sciurus vulgaris",
        "Anas platyrhynchos",
        "Erinaceus europaeus",
        "Erithacus rubecula",
        "Capreolus capreolus",
        "Meles meles",
        "Alcedo atthis",
        "Castor fiber",
        "Lutra lutra",
        "Salamandra salamandra",
        "Rupicapra rupicapra",
        "Bubo bubo",
        "Lynx lynx",
        "Canis lupus",
        "Ursus arctos",
        "Ursus maritimus",
        "Gulo gulo",
        "Panthera uncia",
        "Ailuropoda melanoleuca",
        "Lynx pardinus",
        "Pongo abelii"
    ];

    public static async Task SeedAsync(BiomaDbContext db)
    {
        // 1. Génération automatique du schéma relationnel (support Supabase & SQLite)
        var databaseCreator = db.Database.GetService<IDatabaseCreator>() as IRelationalDatabaseCreator;
        bool needCreate = false;

        try
        {
            var conn = db.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
            {
                await conn.OpenAsync();
            }
            using var cmd = conn.CreateCommand();
            bool isPg = db.Database.ProviderName?.Contains("Npgsql") == true;
            cmd.CommandText = isPg
                ? "SELECT EXISTS (SELECT FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'SpeciesCards');"
                : "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='SpeciesCards';";
            var result = await cmd.ExecuteScalarAsync();
            if (result is bool b)
            {
                needCreate = !b;
            }
            else if (result != null && long.TryParse(result.ToString(), out var count))
            {
                needCreate = count == 0;
            }
            else
            {
                needCreate = true;
            }
        }
        catch
        {
            needCreate = true;
        }

        if (needCreate)
        {
            if (databaseCreator != null)
            {
                try
                {
                    await databaseCreator.CreateTablesAsync();
                }
                catch
                {
                    await db.Database.EnsureCreatedAsync();
                }
            }
            else
            {
                await db.Database.EnsureCreatedAsync();
            }
        }

        // 2. Socle minimal hors-ligne (uniquement les 3 espèces de départ requises si l'environnement de test n'a pas d'accès réseau)
        var fox = await EnsureStarterSpeciesAsync(
            db,
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "Renard roux",
            "Vulpes vulpes",
            "Canidae",
            CardRarity.Commune,
            220_000);

        var lion = await EnsureStarterSpeciesAsync(
            db,
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            "Lion d'Afrique",
            "Panthera leo",
            "Felidae",
            CardRarity.Rare,
            11_500);

        var eagle = await EnsureStarterSpeciesAsync(
            db,
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            "Pygargue à tête blanche",
            "Haliaeetus leucocephalus",
            "Accipitridae",
            CardRarity.PeuCommune,
            65_000);

        await db.SaveChangesAsync();

        // 3. Synchronisation dynamique avec l'API publique iNaturalist / GBIF (uniquement sur base relationnelle réelle)
        bool isRelational = db.Database.IsRelational();
        if (isRelational)
        {
            await SyncCatalogueFromLiveApiAsync(db);
        }

        // 4. Comptes fondateurs (Ian & Arfaoumo)
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

            db.PlayerCards.AddRange(
                new PlayerCard
                {
                    PlayerId = ianUser.Id,
                    SpeciesCardId = fox.Id,
                    CustomNickname = fox.CommonName,
                    Level = 2,
                    ExperiencePoints = 120,
                    Stage = LifeStage.Adulte,
                    State = CardState.Disponible,
                    IsInActiveDeck = true,
                    CurrentAttack = fox.BaseAttack,
                    CurrentDefense = fox.BaseDefense,
                    CurrentSpeed = fox.BaseSpeed,
                    WeightKg = fox.AverageWeightKg,
                    PolaroidFinish = "Standard",
                    ObtainedVia = "Starter"
                },
                new PlayerCard
                {
                    PlayerId = ianUser.Id,
                    SpeciesCardId = lion.Id,
                    CustomNickname = lion.CommonName,
                    Level = 1,
                    ExperiencePoints = 40,
                    Stage = LifeStage.Juvenile,
                    State = CardState.Disponible,
                    IsInActiveDeck = true,
                    CurrentAttack = lion.BaseAttack,
                    CurrentDefense = lion.BaseDefense,
                    CurrentSpeed = lion.BaseSpeed,
                    WeightKg = lion.AverageWeightKg,
                    PolaroidFinish = "Or",
                    ObtainedVia = "Starter"
                },
                new PlayerCard
                {
                    PlayerId = ianUser.Id,
                    SpeciesCardId = eagle.Id,
                    CustomNickname = eagle.CommonName,
                    Level = 1,
                    ExperiencePoints = 20,
                    Stage = LifeStage.Juvenile,
                    State = CardState.Disponible,
                    IsInActiveDeck = true,
                    CurrentAttack = eagle.BaseAttack,
                    CurrentDefense = eagle.BaseDefense,
                    CurrentSpeed = eagle.BaseSpeed,
                    WeightKg = eagle.AverageWeightKg,
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

            db.PlayerCards.AddRange(
                new PlayerCard
                {
                    PlayerId = arfaoumoUser.Id,
                    SpeciesCardId = lion.Id,
                    CustomNickname = lion.CommonName,
                    Level = 2,
                    ExperiencePoints = 140,
                    Stage = LifeStage.Adulte,
                    State = CardState.Disponible,
                    IsInActiveDeck = true,
                    CurrentAttack = lion.BaseAttack,
                    CurrentDefense = lion.BaseDefense,
                    CurrentSpeed = lion.BaseSpeed,
                    WeightKg = lion.AverageWeightKg,
                    PolaroidFinish = "Or",
                    ObtainedVia = "Starter"
                },
                new PlayerCard
                {
                    PlayerId = arfaoumoUser.Id,
                    SpeciesCardId = eagle.Id,
                    CustomNickname = eagle.CommonName,
                    Level = 2,
                    ExperiencePoints = 110,
                    Stage = LifeStage.Adulte,
                    State = CardState.Disponible,
                    IsInActiveDeck = true,
                    CurrentAttack = eagle.BaseAttack,
                    CurrentDefense = eagle.BaseDefense,
                    CurrentSpeed = eagle.BaseSpeed,
                    WeightKg = eagle.AverageWeightKg,
                    PolaroidFinish = "Standard",
                    ObtainedVia = "Starter"
                },
                new PlayerCard
                {
                    PlayerId = arfaoumoUser.Id,
                    SpeciesCardId = fox.Id,
                    CustomNickname = fox.CommonName,
                    Level = 1,
                    ExperiencePoints = 30,
                    Stage = LifeStage.Juvenile,
                    State = CardState.Disponible,
                    IsInActiveDeck = true,
                    CurrentAttack = fox.BaseAttack,
                    CurrentDefense = fox.BaseDefense,
                    CurrentSpeed = fox.BaseSpeed,
                    WeightKg = fox.AverageWeightKg,
                    PolaroidFinish = "Standard",
                    ObtainedVia = "Starter"
                }
            );
            await db.SaveChangesAsync();
        }
    }

    private static async Task<SpeciesCard> EnsureStarterSpeciesAsync(
        BiomaDbContext db,
        Guid id,
        string commonName,
        string scientificName,
        string family,
        CardRarity rarity,
        long defaultOccurrences)
    {
        GachaService.RegisterLiveOccurrenceCount(scientificName, defaultOccurrences);
        var (atk, def, spd, weight) = GachaService.ComputeStatsFromRarity(rarity, scientificName);
        var biome = Showcase3DService.ResolveBiomeForSpecies(scientificName, commonName);

        var card = await db.Cards.FirstOrDefaultAsync(c => c.Id == id || c.ScientificName == scientificName);
        if (card is null)
        {
            card = new SpeciesCard
            {
                Id = id,
                CommonName = commonName,
                ScientificName = scientificName,
                Family = family,
                Kingdom = "Animalia",
                SourceApi = "iNaturalist",
                ExternalId = $"seed-{scientificName.ToLowerInvariant().Replace(' ', '-')}",
                ImageUrl = BuildCleanSvgPlaceholder(commonName, scientificName),
                CountryOrPlace = "Observation naturelle",
                ObservedDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                Rarity = rarity,
                Biome = biome,
                AverageWeightKg = weight,
                BaseAttack = atk,
                BaseDefense = def,
                BaseSpeed = spd
            };
            db.Cards.Add(card);
        }
        else if (card.Biome != biome)
        {
            card.Biome = biome;
        }
        return card;
    }

    /// <summary>
    /// Interroge en direct l'API publique iNaturalist (/v1/taxa) pour chaque espèce afin d'obtenir :
    /// - La vraie photographie taxonomique de l'animal (default_photo.medium_url)
    /// - Le vrai nom commun français (preferred_common_name)
    /// - Le vrai nombre d'observations mondiales dans la nature (observations_count)
    /// - Le niveau de rareté réel corrélé aux observations naturelles
    /// Met également à jour les cartes existantes en base Supabase pour corriger toute image erronée.
    /// </summary>
    private static async Task SyncCatalogueFromLiveApiAsync(BiomaDbContext db)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("BiomaNaturalistApp/1.0");

        var existingCards = await db.Cards.ToListAsync();

        foreach (var sciName in TargetSpeciesScientificNames)
        {
            try
            {
                var url = $"https://api.inaturalist.org/v1/taxa?q={Uri.EscapeDataString(sciName)}&rank=species&locale=fr&per_page=5";
                using var response = await http.GetAsync(url);
                if (!response.IsSuccessStatusCode) continue;

                using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
                if (!doc.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                JsonElement? matchedTaxon = null;
                foreach (var taxon in results.EnumerateArray())
                {
                    var name = taxon.TryGetProperty("name", out var n) ? n.GetString() : null;
                    if (string.Equals(name, sciName, StringComparison.OrdinalIgnoreCase))
                    {
                        matchedTaxon = taxon;
                        break;
                    }
                }
                matchedTaxon ??= results.GetArrayLength() > 0 ? results[0] : null;
                if (matchedTaxon is null) continue;

                var t = matchedTaxon.Value;
                string taxonId = t.TryGetProperty("id", out var idProp) ? idProp.ToString() : sciName;
                string commonFr = t.TryGetProperty("preferred_common_name", out var cn) && !string.IsNullOrWhiteSpace(cn.GetString())
                    ? CapitalizeFirst(cn.GetString()!)
                    : sciName;
                string family = t.TryGetProperty("iconic_taxon_name", out var fam) && !string.IsNullOrWhiteSpace(fam.GetString())
                    ? fam.GetString()!
                    : "Animalia";
                long obsCount = t.TryGetProperty("observations_count", out var oc) && oc.ValueKind == JsonValueKind.Number
                    ? oc.GetInt64()
                    : 25_000;

                string? photoUrl = null;
                if (t.TryGetProperty("default_photo", out var dp) && dp.ValueKind == JsonValueKind.Object)
                {
                    photoUrl = (dp.TryGetProperty("medium_url", out var mu) ? mu.GetString() : null)
                            ?? (dp.TryGetProperty("url", out var u) ? u.GetString()?.Replace("square", "medium") : null);
                }

                if (string.IsNullOrWhiteSpace(photoUrl)) continue;

                GachaService.RegisterLiveOccurrenceCount(sciName, obsCount);
                var rarity = GachaService.ComputeRarityFromOccurrences(obsCount);
                var (atk, def, spd, weight) = GachaService.ComputeStatsFromRarity(rarity, sciName);
                var biome = Showcase3DService.ResolveBiomeForSpecies(sciName, commonFr);

                var matchingCards = existingCards
                    .Where(c => c.ScientificName.Equals(sciName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (matchingCards.Count == 0)
                {
                    var newCard = new SpeciesCard
                    {
                        Id = Guid.NewGuid(),
                        CommonName = commonFr,
                        ScientificName = sciName,
                        Family = family,
                        Kingdom = "Animalia",
                        SourceApi = "iNaturalist",
                        ExternalId = $"inat-taxon-{taxonId}",
                        ImageUrl = photoUrl,
                        CountryOrPlace = "Référentiel mondial iNaturalist / GBIF",
                        ObservedDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                        Rarity = rarity,
                        Biome = biome,
                        AverageWeightKg = weight,
                        BaseAttack = atk,
                        BaseDefense = def,
                        BaseSpeed = spd,
                        CreatedAt = DateTimeOffset.UtcNow
                    };
                    db.Cards.Add(newCard);
                    existingCards.Add(newCard);
                }
                else
                {
                    // Met à jour les cartes existantes avec la vraie photo de l'espèce, le biome et la vraie rareté issue de l'API
                    foreach (var existing in matchingCards)
                    {
                        existing.CommonName = commonFr;
                        existing.ImageUrl = photoUrl;
                        existing.Family = family;
                        existing.Rarity = rarity;
                        existing.Biome = biome;
                        existing.BaseAttack = atk;
                        existing.BaseDefense = def;
                        existing.BaseSpeed = spd;
                    }
                }
            }
            catch
            {
                // Continue avec l'espèce suivante si un appel individuel expire
            }
        }

        await db.SaveChangesAsync();
    }

    private static string CapitalizeFirst(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s[1..];

    private static string BuildCleanSvgPlaceholder(string commonName, string scientificName)
    {
        string safeCommon = Uri.EscapeDataString(commonName);
        string safeSci = Uri.EscapeDataString(scientificName);
        return $"data:image/svg+xml;utf8,<svg xmlns='http://www.w3.org/2000/svg' width='600' height='420' viewBox='0 0 600 420'><rect width='600' height='420' fill='%23162D20'/><text x='300' y='200' text-anchor='middle' fill='%23F5F1E6' font-family='serif' font-weight='700' font-size='24'>{safeCommon}</text><text x='300' y='235' text-anchor='middle' fill='%238FA898' font-family='sans-serif' font-style='italic' font-size='16'>{safeSci}</text></svg>";
    }
}
