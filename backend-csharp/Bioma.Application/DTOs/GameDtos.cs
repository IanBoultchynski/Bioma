using Bioma.Domain.Enums;

namespace Bioma.Application.DTOs;

/// <summary>
/// Observation brute récupérée en direct depuis les APIs publiques GBIF ou iNaturalist,
/// enrichie avec la rareté biologique réelle dans la nature et sa corrélation avec le tirage Gacha (US-28, US-30).
/// </summary>
public record ExternalObservationDto(
    string ExternalId,
    string SourceApi, // "GBIF" | "iNaturalist"
    string ScientificName,
    string CommonName,
    string Family,
    string Kingdom,
    double? Latitude,
    double? Longitude,
    string CountryOrPlace,
    string ObservedDate,
    string ImageUrl,
    long NaturalOccurrencesCount = 0,
    CardRarity Rarity = CardRarity.Commune,
    string RarityLabel = "Commune",
    double DropRatePercent = 60.0,
    int BaseAttack = 40,
    int BaseDefense = 35,
    int BaseSpeed = 45,
    BiomeType Biome = BiomeType.Taiga,
    string BiomeLabel = "Forêt de Sapins (Taïga)"
);

/// <summary>
/// Résultat combiné d'une recherche sur GBIF et iNaturalist avec état des connexions.
/// </summary>
public record ApiSearchResultDto(
    string Query,
    bool GbifConnected,
    bool InaturalistConnected,
    int GbifCount,
    int InaturalistCount,
    string StatusMessage,
    List<ExternalObservationDto> Observations
);

/// <summary>
/// Carte enregistrée dans le Back-Office C# et disponible dans le pool de Tirage Gacha.
/// </summary>
public record CardDto(
    Guid Id,
    string Name,
    string ScientificSubtitle,
    string FamilyName,
    string SourceApi,
    string ImageUrl,
    string CoordinatesText,
    string CountryOrPlace,
    string ObservedDate,
    CardRarity Rarity = CardRarity.Commune,
    string RarityLabel = "Commune",
    long NaturalOccurrencesCount = 0,
    double DropRatePercent = 60.0,
    int BaseAttack = 40,
    int BaseDefense = 35,
    int BaseSpeed = 45,
    BiomeType Biome = BiomeType.Taiga,
    string BiomeLabel = "Forêt de Sapins (Taïga)"
);
