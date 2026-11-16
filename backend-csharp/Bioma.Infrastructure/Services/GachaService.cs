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
/// - US-28 : les espèces rares dans la nature sont les plus difficiles à obtenir
/// - US-29 : renvoie les cartes dans l'ordre pour l'animation de révélation
/// - US-30 : chaque carte indique sa rareté (Commune → Légendaire)
/// - US-31 : multi-tirage ×10 (avec une carte Rare minimum garantie)
/// </summary>
public class GachaService : IGachaService
{
    public const int SinglePullCost = 100;
    public const int MultiPullCount = 10;
    public const int MultiPullCost = 900; // ×10 = 10 % de réduction

    /// <summary>
    /// Table de probabilités par carte (poids relatifs, total = 100).
    /// Modifier ici pour équilibrer le jeu.
    /// </summary>
    private static readonly (CardRarity Rarity, int Weight)[] DropTable =
    {
        (CardRarity.Commune, 60),
        (CardRarity.PeuCommune, 28),
        (CardRarity.Rare, 10),
        (CardRarity.Legendaire, 2)
    };

    private readonly BiomaDbContext _db;
    private readonly Random _rng;

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

    public async Task<GachaBannerDto> GetBannerAsync(Guid playerId, CancellationToken ct = default)
    {
        var currency = await _db.Players
            .Where(p => p.Id == playerId)
            .Select(p => p.VirtualCurrency)
            .FirstOrDefaultAsync(ct);

        var catalogueSize = await DrawablePool().CountAsync(ct);

        int totalWeight = DropTable.Sum(d => d.Weight);
        var odds = DropTable
            .Select(d => new GachaOddsDto(d.Rarity, RarityLabel(d.Rarity), Math.Round(d.Weight * 100.0 / totalWeight, 1)))
            .ToList();

        return new GachaBannerDto(SinglePullCost, MultiPullCost, MultiPullCount, currency, catalogueSize, odds);
    }

    public async Task<GachaPullResultDto> PullAsync(Guid playerId, int count, CancellationToken ct = default)
    {
        if (count != 1 && count != MultiPullCount)
        {
            return Fail($"Tirage ×{count} impossible : choisissez ×1 ou ×{MultiPullCount}.", 0);
        }

        var player = await _db.Players.FirstOrDefaultAsync(p => p.Id == playerId, ct);
        if (player is null)
        {
            return Fail("Joueur introuvable.", 0);
        }

        int cost = count == 1 ? SinglePullCost : MultiPullCost;
        if (player.VirtualCurrency < cost)
        {
            return Fail($"Solde insuffisant : il faut {cost}, vous avez {player.VirtualCurrency}.", player.VirtualCurrency);
        }

        var pool = await DrawablePool().AsNoTracking().ToListAsync(ct);
        if (pool.Count == 0)
        {
            return Fail("Le catalogue d'espèces est vide : ajoutez des espèces depuis l'explorateur.", player.VirtualCurrency);
        }

        var poolByRarity = pool
            .GroupBy(s => s.Rarity)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Espèces déjà possédées, pour signaler les nouveautés
        var ownedSpeciesIds = (await _db.PlayerCards
                .Where(pc => pc.PlayerId == playerId)
                .Select(pc => pc.SpeciesCardId)
                .ToListAsync(ct))
            .ToHashSet();

        // 1. On tire d'abord la rareté de chaque carte
        var rolledRarities = Enumerable.Range(0, count).Select(_ => RollRarity()).ToList();

        // Garantie du ×10 : au moins une carte Rare ou mieux
        if (count == MultiPullCount && rolledRarities.All(r => r < CardRarity.Rare))
        {
            rolledRarities[_rng.Next(count)] = CardRarity.Rare;
        }

        // 2. On débite le joueur
        player.VirtualCurrency -= cost;
        int costPerCard = cost / count;

        // 3. Pour chaque rareté, on choisit une espèce et on crée l'exemplaire du joueur
        var drawnCards = new List<GachaDrawnCardDto>();
        foreach (var rarity in rolledRarities)
        {
            var species = PickSpecies(poolByRarity, rarity);

            var playerCard = new PlayerCard
            {
                PlayerId = player.Id,
                SpeciesCardId = species.Id,
                CurrentAttack = species.BaseAttack,
                CurrentDefense = species.BaseDefense,
                CurrentSpeed = species.BaseSpeed,
                WeightKg = species.AverageWeightKg,
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

            bool isNew = ownedSpeciesIds.Add(species.Id); // Add renvoie false si déjà présent
            drawnCards.Add(new GachaDrawnCardDto(
                playerCard.Id,
                species.Id,
                species.CommonName,
                species.ScientificName,
                species.Family,
                species.ImageUrl,
                species.Rarity,
                RarityLabel(species.Rarity),
                isNew));
        }

        // Un seul SaveChanges => débit + cartes + historique enregistrés ensemble (ou pas du tout)
        await _db.SaveChangesAsync(ct);

        return new GachaPullResultDto(
            true,
            $"{count} carte(s) ajoutée(s) à votre collection.",
            cost,
            player.VirtualCurrency,
            drawnCards);
    }

    public static string RarityLabel(CardRarity rarity) => rarity switch
    {
        CardRarity.Commune => "Commune",
        CardRarity.PeuCommune => "Peu commune",
        CardRarity.Rare => "Rare",
        CardRarity.Legendaire => "Légendaire",
        _ => rarity.ToString()
    };

    /// <summary>
    /// Espèces pouvant sortir au tirage : ni hybrides, ni créations du Laboratoire.
    /// </summary>
    private IQueryable<SpeciesCard> DrawablePool() =>
        _db.Cards.Where(c => !c.IsHybrid && c.SourceApi != "Laboratoire");

    private CardRarity RollRarity()
    {
        int totalWeight = DropTable.Sum(d => d.Weight);
        int roll = _rng.Next(totalWeight); // 0 .. total-1
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

    /// <summary>
    /// Choisit une espèce de la rareté demandée.
    /// S'il n'y en a aucune dans le catalogue, on prend la rareté inférieure la plus proche,
    /// puis la supérieure (évite un crash quand le catalogue est encore petit).
    /// </summary>
    private SpeciesCard PickSpecies(Dictionary<CardRarity, List<SpeciesCard>> poolByRarity, CardRarity wanted)
    {
        var fallbackOrder = Enum.GetValues<CardRarity>()
            .OrderBy(r => r > wanted)              // d'abord la rareté voulue et les inférieures...
            .ThenBy(r => Math.Abs((int)r - (int)wanted)); // ...de la plus proche à la plus lointaine

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