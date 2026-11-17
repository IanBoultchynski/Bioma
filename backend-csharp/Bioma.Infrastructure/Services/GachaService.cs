using System.Collections.Concurrent;
using Bioma.Application.DTOs;
using Bioma.Application.Interfaces;
using Bioma.Domain.Entities;
using Bioma.Domain.Enums;
using Bioma.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Bioma.Infrastructure.Services;

/// <summary>
/// Épique 5 : Tirage / Gacha (US-27 à US-31) + Tirages Gratuits Temporels (Max 10, +1 toutes les 10 min).
/// - US-27 : dépense de monnaie virtuelle ou utilisation du stock de tirages gratuits
/// - US-28 : les espèces rares dans la vraie nature (occurrences API) sont les plus difficiles à obtenir
/// - US-29 : renvoie les cartes dans l'ordre pour l'animation de révélation Polaroïd
/// - US-30 : chaque carte indique clairement sa rareté (Commune → Légendaire) et sa couleur associée
/// - US-31 : multi-tirage ×10 (avec une carte Rare minimum garantie)
/// </summary>
public class GachaService : IGachaService
{
    public const int SinglePullCost = 100;
    public const int MultiPullCount = 10;
    public const int MultiPullCost = 900; // ×10 = 10 % de réduction

    public const int MaxFreePullsStock = 10;
    public const int FreePullRegenMinutes = 10;
    public const int FreePullRegenSeconds = FreePullRegenMinutes * 60;

    /// <summary>
    /// Table de probabilités par carte (poids relatifs, total = 100),
    /// inversement corrélée à l'abondance réelle dans la nature (API GBIF / iNaturalist).
    /// </summary>
    private static readonly (CardRarity Rarity, int Weight)[] DropTable =
    [
        (CardRarity.Commune, 60),
        (CardRarity.PeuCommune, 28),
        (CardRarity.Rare, 10),
        (CardRarity.Legendaire, 2)
    ];

    /// <summary>
    /// Cache dynamique alimenté en direct par les APIs GBIF &amp; iNaturalist (nombre réel d'observations dans la nature).
    /// </summary>
    private static readonly ConcurrentDictionary<string, long> LiveApiOccurrenceCounts = new(StringComparer.OrdinalIgnoreCase);

    private readonly BiomaDbContext _db;
    private readonly Random _rng;
    private readonly SemaphoreSlim _gate = new(1, 1);

    // Constructeur utilisé par l'injection de dépendances
    public GachaService(BiomaDbContext db) : this(db, Random.Shared)
    {
    }

    // Constructeur utilisé par les tests (Random avec graine fixe => résultats reproductibles)
    public GachaService(BiomaDbContext db, Random rng)
    {
        _db = db;
        _rng = rng;
    }

    public static void RegisterLiveOccurrenceCount(string scientificName, long count)
    {
        if (!string.IsNullOrWhiteSpace(scientificName) && count > 0)
        {
            LiveApiOccurrenceCounts[scientificName.Trim()] = count;
        }
    }

    /// <summary>
    /// US-28 : Convertit le nombre d'observations réelles dans la nature (issu de l'API) en niveau de rareté Bioma.
    /// </summary>
    public static CardRarity ComputeRarityFromOccurrences(long occurrenceCount) => occurrenceCount switch
    {
        >= 100_000 => CardRarity.Commune,
        >= 20_000  => CardRarity.PeuCommune,
        >= 3_500   => CardRarity.Rare,
        _          => CardRarity.Legendaire
    };

    public static (int Atk, int Def, int Spd, double WeightKg) ComputeStatsFromRarity(CardRarity rarity, string scientificName)
    {
        int hash = Math.Abs(scientificName.GetHashCode());
        int v1 = hash % 9;
        int v2 = (hash / 11) % 9;
        int v3 = (hash / 23) % 9;

        return rarity switch
        {
            CardRarity.Legendaire => (88 + v1, 82 + v2, 84 + v3, 45.0),
            CardRarity.Rare       => (72 + v1, 64 + v2, 68 + v3, 35.0),
            CardRarity.PeuCommune => (54 + v1, 48 + v2, 58 + v3, 12.0),
            _                     => (36 + v1, 34 + v2, 48 + v3, 5.0)
        };
    }

    public static long GetEstimatedNaturalOccurrences(string? scientificName, CardRarity rarity)
    {
        if (!string.IsNullOrWhiteSpace(scientificName) &&
            LiveApiOccurrenceCounts.TryGetValue(scientificName.Trim(), out var liveCount))
        {
            return liveCount;
        }

        return rarity switch
        {
            CardRarity.Commune    => 180_000,
            CardRarity.PeuCommune => 45_000,
            CardRarity.Rare       => 9_500,
            CardRarity.Legendaire => 1_200,
            _                     => 180_000
        };
    }

    public static double DropRateForRarity(CardRarity rarity)
    {
        int total = DropTable.Sum(d => d.Weight);
        var match = DropTable.FirstOrDefault(d => d.Rarity == rarity);
        return match.Weight > 0 ? Math.Round(match.Weight * 100.0 / total, 1) : 60.0;
    }

    public static string PolaroidFinishForRarity(CardRarity rarity) => rarity switch
    {
        CardRarity.Legendaire => "Holographique",
        CardRarity.Rare       => "Or",
        CardRarity.PeuCommune => "Bronze",
        _                     => "Standard"
    };

    /// <summary>
    /// Calcule en temps réel le stock de tirages gratuits (max 10, +1 toutes les 10 minutes)
    /// à partir de l'historique des tirages gratuits enregistrés dans Supabase PostgreSQL.
    /// </summary>
    public static (int AvailableStock, int SecondsUntilNext, DateTimeOffset? FullAtUtc) CalculateFreePullsFromTimestamps(
        IEnumerable<DateTimeOffset> recentFreePullTimes,
        DateTimeOffset nowUtc)
    {
        DateTimeOffset fullAt = DateTimeOffset.MinValue;

        foreach (var pulledAt in recentFreePullTimes.OrderBy(t => t))
        {
            var baseTime = fullAt > pulledAt ? fullAt : pulledAt;
            fullAt = baseTime.AddMinutes(FreePullRegenMinutes);

            var maxCeiling = pulledAt.AddMinutes(MaxFreePullsStock * FreePullRegenMinutes);
            if (fullAt > maxCeiling)
            {
                fullAt = maxCeiling;
            }
        }

        if (fullAt <= nowUtc)
        {
            return (MaxFreePullsStock, 0, null);
        }

        double remainingSecondsToFull = (fullAt - nowUtc).TotalSeconds;
        int missingPulls = Math.Clamp((int)Math.Ceiling(remainingSecondsToFull / FreePullRegenSeconds), 1, MaxFreePullsStock);
        int availableStock = Math.Clamp(MaxFreePullsStock - missingPulls, 0, MaxFreePullsStock);

        double secondsInCurrentCycle = remainingSecondsToFull - ((missingPulls - 1) * FreePullRegenSeconds);
        int secondsUntilNext = Math.Clamp((int)Math.Ceiling(secondsInCurrentCycle), 1, FreePullRegenSeconds);

        return (availableStock, secondsUntilNext, fullAt);
    }

    private async Task<(int AvailableStock, int SecondsUntilNext, DateTimeOffset? FullAtUtc)> GetFreePullsStateCoreAsync(
        Guid playerId,
        DateTimeOffset nowUtc,
        CancellationToken ct)
    {
        var windowStart = nowUtc.AddMinutes(-(MaxFreePullsStock * FreePullRegenMinutes + 5));
        var recentFreeTimes = await _db.GachaPulls
            .AsNoTracking()
            .Where(g => g.PlayerId == playerId && g.CurrencySpent == 0 && g.PulledAt >= windowStart)
            .Select(g => g.PulledAt)
            .ToListAsync(ct);

        return CalculateFreePullsFromTimestamps(recentFreeTimes, nowUtc);
    }

    public async Task<GachaBannerDto> GetBannerAsync(Guid playerId, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var currency = await _db.Players
                .AsNoTracking()
                .Where(p => p.Id == playerId)
                .Select(p => p.VirtualCurrency)
                .FirstOrDefaultAsync(ct);

            var catalogueSize = await DrawablePool()
                .AsNoTracking()
                .Select(c => c.ScientificName)
                .Distinct()
                .CountAsync(ct);

            var (freeAvailable, secondsUntilNext, fullAtUtc) = await GetFreePullsStateCoreAsync(playerId, DateTimeOffset.UtcNow, ct);

            int totalWeight = DropTable.Sum(d => d.Weight);
            var odds = DropTable
                .Select(d => new GachaOddsDto(d.Rarity, RarityLabel(d.Rarity), Math.Round(d.Weight * 100.0 / totalWeight, 1)))
                .ToList();

            return new GachaBannerDto(
                SinglePullCost,
                MultiPullCost,
                MultiPullCount,
                currency,
                catalogueSize,
                odds,
                freeAvailable,
                MaxFreePullsStock,
                FreePullRegenMinutes,
                secondsUntilNext,
                fullAtUtc);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<int> GrantExplorationCreditsAsync(Guid playerId, int amount = 500, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var player = await _db.Players.FirstOrDefaultAsync(p => p.Id == playerId, ct);
            if (player is null) return 0;

            player.VirtualCurrency += Math.Max(50, amount);
            player.LastCloudSyncAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
            return player.VirtualCurrency;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<GachaPullResultDto> PullFreeAsync(Guid playerId, int count = 1, CancellationToken ct = default)
    {
        if (count < 1 || count > MaxFreePullsStock)
        {
            return Fail($"Nombre de tirages gratuits invalide ({count}).", 0);
        }

        await _gate.WaitAsync(ct);
        try
        {
            var player = await _db.Players.FirstOrDefaultAsync(p => p.Id == playerId, ct);
            if (player is null)
            {
                return Fail("Joueur introuvable. Connectez-vous dans l'onglet Mon Deck & Profil.", 0);
            }

            var nowUtc = DateTimeOffset.UtcNow;
            var (freeAvailable, secondsUntilNext, fullAtUtc) = await GetFreePullsStateCoreAsync(playerId, nowUtc, ct);

            if (freeAvailable < count)
            {
                int mins = secondsUntilNext / 60;
                int secs = secondsUntilNext % 60;
                return new GachaPullResultDto(
                    false,
                    $"Stock de tirages gratuits insuffisant ({freeAvailable}/{MaxFreePullsStock}). Prochaine carte gratuite dans {mins:D2}:{secs:D2}.",
                    0,
                    player.VirtualCurrency,
                    new List<GachaDrawnCardDto>(),
                    freeAvailable,
                    secondsUntilNext,
                    fullAtUtc);
            }

            return await ExecutePullCoreAsync(player, count, isFreePull: true, nowUtc, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<GachaPullResultDto> PullAsync(Guid playerId, int count, CancellationToken ct = default)
    {
        if (count != 1 && count != MultiPullCount)
        {
            return Fail($"Tirage ×{count} impossible : choisissez ×1 ou ×{MultiPullCount}.", 0);
        }

        await _gate.WaitAsync(ct);
        try
        {
            var player = await _db.Players.FirstOrDefaultAsync(p => p.Id == playerId, ct);
            if (player is null)
            {
                return Fail("Joueur introuvable. Connectez-vous dans l'onglet Mon Deck & Profil.", 0);
            }

            int cost = count == 1 ? SinglePullCost : MultiPullCost;
            if (player.VirtualCurrency < cost)
            {
                return Fail($"Solde insuffisant : il faut {cost} crédits, vous avez {player.VirtualCurrency}.", player.VirtualCurrency);
            }

            return await ExecutePullCoreAsync(player, count, isFreePull: false, DateTimeOffset.UtcNow, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<GachaPullResultDto> ExecutePullCoreAsync(
        PlayerProfile player,
        int count,
        bool isFreePull,
        DateTimeOffset nowUtc,
        CancellationToken ct)
    {
        var pool = await DrawablePool().AsNoTracking().ToListAsync(ct);
        if (pool.Count == 0)
        {
            return Fail("Le catalogue d'espèces est vide : ajoutez des espèces depuis l'explorateur.", player.VirtualCurrency);
        }

        var uniquePool = pool
            .GroupBy(s => s.ScientificName, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        var poolByRarity = uniquePool
            .GroupBy(s => s.Rarity)
            .ToDictionary(g => g.Key, g => g.ToList());

        var ownedSpeciesIds = (await _db.PlayerCards
                .AsNoTracking()
                .Where(pc => pc.PlayerId == player.Id)
                .Select(pc => pc.SpeciesCardId)
                .ToListAsync(ct))
            .ToHashSet();

        var rolledRarities = Enumerable.Range(0, count).Select(_ => RollRarity()).ToList();

        // Garantie du ×10 : au moins une carte Rare ou Légendaire
        if (count == MultiPullCount && rolledRarities.All(r => r < CardRarity.Rare))
        {
            rolledRarities[_rng.Next(count)] = CardRarity.Rare;
        }

        int totalCost = isFreePull ? 0 : (count == 1 ? SinglePullCost : MultiPullCost);
        int costPerCard = isFreePull ? 0 : (totalCost / count);

        if (!isFreePull)
        {
            player.VirtualCurrency -= totalCost;
        }
        player.LastCloudSyncAt = nowUtc;

        var drawnCards = new List<GachaDrawnCardDto>();
        for (int i = 0; i < rolledRarities.Count; i++)
        {
            var rarity = rolledRarities[i];
            var species = PickSpecies(poolByRarity, rarity);
            long naturalOccurrences = GetEstimatedNaturalOccurrences(species.ScientificName, species.Rarity);
            double dropRate = DropRateForRarity(species.Rarity);

            var playerCard = new PlayerCard
            {
                PlayerId = player.Id,
                SpeciesCardId = species.Id,
                CustomNickname = species.CommonName,
                Level = 1,
                ExperiencePoints = 0,
                Stage = LifeStage.Juvenile,
                State = CardState.Disponible,
                IsInActiveDeck = false,
                CurrentAttack = species.BaseAttack,
                CurrentDefense = species.BaseDefense,
                CurrentSpeed = species.BaseSpeed,
                WeightKg = species.AverageWeightKg,
                PolaroidFinish = PolaroidFinishForRarity(species.Rarity),
                ObtainedVia = "Gacha",
                AcquiredAt = nowUtc.AddMilliseconds(i)
            };
            _db.PlayerCards.Add(playerCard);

            _db.GachaPulls.Add(new GachaPullRecord
            {
                PlayerId = player.Id,
                PlayerCardId = playerCard.Id,
                Rarity = species.Rarity,
                CurrencySpent = costPerCard,
                IsMultiPull = count > 1,
                PulledAt = nowUtc.AddMilliseconds(i)
            });

            bool isNew = ownedSpeciesIds.Add(species.Id);
            drawnCards.Add(new GachaDrawnCardDto(
                playerCard.Id,
                species.Id,
                species.CommonName,
                species.ScientificName,
                species.Family,
                species.ImageUrl,
                species.Rarity,
                RarityLabel(species.Rarity),
                isNew,
                naturalOccurrences,
                dropRate,
                playerCard.CurrentAttack,
                playerCard.CurrentDefense,
                playerCard.CurrentSpeed,
                playerCard.IsInActiveDeck));
        }

        await _db.SaveChangesAsync(ct);

        var (freeAvailable, secondsUntilNext, fullAtUtc) = await GetFreePullsStateCoreAsync(player.Id, nowUtc, ct);

        return new GachaPullResultDto(
            true,
            $"{count} carte(s) ajoutée(s) à l'inventaire de {player.Username}.",
            totalCost,
            player.VirtualCurrency,
            drawnCards,
            freeAvailable,
            secondsUntilNext,
            fullAtUtc);
    }

    public static string RarityLabel(CardRarity rarity) => rarity switch
    {
        CardRarity.Commune => "Commune",
        CardRarity.PeuCommune => "Peu commune",
        CardRarity.Rare => "Rare",
        CardRarity.Legendaire => "Légendaire",
        _ => rarity.ToString()
    };

    private IQueryable<SpeciesCard> DrawablePool() =>
        _db.Cards.Where(c => !c.IsHybrid && c.SourceApi != "Laboratoire");

    private CardRarity RollRarity()
    {
        int totalWeight = DropTable.Sum(d => d.Weight);
        int roll = _rng.Next(totalWeight);
        int cumulative = 0;

        foreach (var (rarity, weight) in DropTable)
        {
            cumulative += weight;
            if (roll < cumulative)
            {
                return rarity;
            }
        }

        return CardRarity.Commune;
    }

    private SpeciesCard PickSpecies(Dictionary<CardRarity, List<SpeciesCard>> poolByRarity, CardRarity wanted)
    {
        var fallbackOrder = Enum.GetValues<CardRarity>()
            .OrderBy(r => r > wanted)
            .ThenBy(r => Math.Abs((int)r - (int)wanted));

        foreach (var rarity in fallbackOrder)
        {
            if (poolByRarity.TryGetValue(rarity, out var candidates) && candidates.Count > 0)
            {
                return candidates[_rng.Next(candidates.Count)];
            }
        }

        throw new InvalidOperationException("Catalogue vide.");
    }

    private static GachaPullResultDto Fail(string message, int currency) =>
        new(false, message, 0, currency, new List<GachaDrawnCardDto>());
}
