using Bioma.Domain.Enums;

namespace Bioma.Application.DTOs;

/// <summary>
/// Probabilité d'obtenir une rareté donnée pour UNE carte tirée (US-28, US-30).
/// </summary>
public record GachaOddsDto(
    CardRarity Rarity,
    string Label,
    double Percent
);

/// <summary>
/// Informations affichées avant le tirage : prix, solde du joueur, stock de tirages gratuits (max 10, +1 / 10 min) et probabilités.
/// </summary>
public record GachaBannerDto(
    int SinglePullCost,
    int MultiPullCost,
    int MultiPullCount,
    int PlayerCurrency,
    int CatalogueSize,
    List<GachaOddsDto> Odds,
    int FreePullsAvailable = 10,
    int MaxFreePulls = 10,
    int FreePullRegenMinutes = 10,
    int SecondsUntilNextFreePull = 0,
    DateTimeOffset? FreePullsFullAtUtc = null
);

/// <summary>
/// Une carte obtenue lors d'un tirage, avec sa rareté clairement indiquée et ses statistiques (US-28, US-29, US-30).
/// </summary>
public record GachaDrawnCardDto(
    Guid PlayerCardId,
    Guid SpeciesCardId,
    string CommonName,
    string ScientificName,
    string Family,
    string ImageUrl,
    CardRarity Rarity,
    string RarityLabel,
    bool IsNew, // true si le joueur ne possédait pas encore cette espèce
    long NaturalOccurrencesCount = 0,
    double DropRatePercent = 60.0,
    int Attack = 40,
    int Defense = 35,
    int Speed = 45,
    bool IsInActiveDeck = false
);

/// <summary>
/// Résultat d'un tirage simple (×1), multiple (×10) ou gratuit (stock temporel).
/// </summary>
public record GachaPullResultDto(
    bool Success,
    string Message,
    int CurrencySpent,
    int RemainingCurrency,
    List<GachaDrawnCardDto> Cards,
    int RemainingFreePulls = 10,
    int SecondsUntilNextFreePull = 0,
    DateTimeOffset? FreePullsFullAtUtc = null
);
