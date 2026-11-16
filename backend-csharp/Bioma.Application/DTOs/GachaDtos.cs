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
/// Informations affichées avant le tirage : prix, solde du joueur, probabilités (US-27, US-31).
/// </summary>
public record GachaBannerDto(
    int SinglePullCost,
    int MultiPullCost,
    int MultiPullCount,
    int PlayerCurrency,
    int CatalogueSize,
    List<GachaOddsDto> Odds
);

/// <summary>
/// Une carte obtenue lors d'un tirage, avec sa rareté clairement indiquée (US-29, US-30).
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
    bool IsNew // true si le joueur ne possédait pas encore cette espèce
);

/// <summary>
/// Résultat d'un tirage simple (×1) ou multiple (×10).
/// </summary>
public record GachaPullResultDto(
    bool Success,
    string Message,
    int CurrencySpent,
    int RemainingCurrency,
    List<GachaDrawnCardDto> Cards
);