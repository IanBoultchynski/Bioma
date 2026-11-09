namespace Bioma.Application.DTOs;

/// <summary>
/// Observation brute récupérée en direct depuis les APIs publiques GBIF ou iNaturalist.
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
    string ImageUrl
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
/// Carte enregistrée dans le Back-Office C#.
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
    string ObservedDate
);
