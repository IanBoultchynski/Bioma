using System.Collections.Concurrent;
using Bioma.Application.DTOs;
using Bioma.Application.Interfaces;
using Bioma.Domain.Entities;
using Bioma.Domain.Enums;
using Bioma.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Bioma.Infrastructure.Services;

/// <summary>
/// Épique 5 : Tirage / Gacha (US-27 à US-31).
/// - US-27 : dépense de monnaie virtuelle pour tirer une carte
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
    /// Plus une espèce est rare dans la vraie nature, plus son palier est élevé et difficile à obtenir.
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
        // Légère variation déterministe basée sur le nom scientifique pour que chaque espèce ait un profil propre
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

            var catalogueSize = await DrawablePool().AsNoTracking().CountAsync(ct);

            int totalWeight = DropTable.Sum(d => d.Weight);
            var odds = DropTable
                .Select(d => new GachaOddsDto(d.Rarity, RarityLabel(d.Rarity), Math.Round(d.Weight * 100.0 / totalWeight, 1)))
                .ToList();

            return new GachaBannerDto(SinglePullCost, MultiPullCost, MultiPullCount, currency, catalogueSize, odds);
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

            var pool = await DrawablePool().AsNoTracking().ToListAsync(ct);
            if (pool.Count == 0)
            {
                return Fail("Le catalogue d'espèces est vide : ajoutez des espèces depuis l'explorateur.", player.VirtualCurrency);
            }

            // Déduplication par nom scientifique pour que chaque espèce du catalogue ait sa place propre
            var uniquePool = pool
                .GroupBy(s => s.ScientificName, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            var poolByRarity = uniquePool
                .GroupBy(s => s.Rarity)
                .ToDictionary(g => g.Key, g => g.ToList());

            var ownedSpeciesIds = (await _db.PlayerCards
                    .AsNoTracking()
                    .Where(pc => pc.PlayerId == playerId)
                    .Select(pc => pc.SpeciesCardId)
                    .ToListAsync(ct))
                .ToHashSet();

            var rolledRarities = Enumerable.Range(0, count).Select(_ => RollRarity()).ToList();

            if (count == MultiPullCount && rolledRarities.All(r => r < CardRarity.Rare))
            {
                rolledRarities[_rng.Next(count)] = CardRarity.Rare;
            }

            player.VirtualCurrency -= cost;
            player.LastCloudSyncAt = DateTimeOffset.UtcNow;
            int costPerCard = cost / count;

            var drawnCards = new List<GachaDrawnCardDto>();
            foreach (var rarity in rolledRarities)
            {
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
                    AcquiredAt = DateTimeOffset.UtcNow
                };
                _db.PlayerCards.Add(playerCard);

                _db.GachaPulls.Add(new GachaPullRecord
                {
                    PlayerId = player.Id,
                    PlayerCardId = playerCard.Id,
                    Rarity = species.Rarity,
                    CurrencySpent = costPerCard,
                    IsMultiPull = count > 1,
                    PulledAt = DateTimeOffset.UtcNow
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

            return new GachaPullResultDto(
                true,
                $"{count} carte(s) ajoutée(s) à l'inventaire de {player.Username}.",
                cost,
                player.VirtualCurrency,
                drawnCards);
        }
        finally
        {
            _gate.Release();
        }
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
