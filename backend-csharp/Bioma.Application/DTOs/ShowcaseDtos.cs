using Bioma.Domain.Enums;

namespace Bioma.Application.DTOs;

/// <summary>
/// Informations sur un biome dans la Vitrine 3D : disponibilité du modèle 3D, vignette d'aperçu et état de création.
/// </summary>
public record BiomeSlotInfoDto(
    BiomeType Biome,
    string BiomeCode,
    string BiomeLabel,
    string ExpectedBlendFileName,
    string ExpectedGlbFileName,
    string? SourceBlendFile,
    string? GlbModelUrl,
    string? PreviewImageUrl,
    bool IsAvailable,
    bool IsCreatedByPlayer,
    int DisplayedCardsCount,
    int TotalPedestalsCount,
    string EcosystemDescription
);

/// <summary>
/// Vue d'ensemble des biomes du joueur (page d'accueil de la Vitrine 3D) :
/// - Biomes créés par le joueur (vide au départ)
/// - Biomes actuellement disponibles à la création
/// </summary>
public record PlayerBiomesOverviewDto(
    Guid OwnerPlayerId,
    string OwnerUsername,
    int TotalInventoryCardsCount,
    List<BiomeSlotInfoDto> CreatedBiomes,
    List<BiomeSlotInfoDto> AvailableBiomesToCreate
);

/// <summary>
/// Socle 3D d'exposition (US-62) positionné dans le biome 3D.
/// </summary>
public record ShowcasePedestalDto(
    Guid Id,
    int LocalSlotIndex,
    int GlobalSlotIndex,
    string SlotLabel,
    double PositionX,
    double PositionY,
    double PositionZ,
    PlayerCardDto? DisplayedCard
);

/// <summary>
/// État complet de la Vitrine 3D d'un biome ouvert par le joueur (US-61, US-62).
/// Toutes les cartes du joueur sont exposables librement dans n'importe quel biome.
/// </summary>
public record ShowcaseViewDto(
    Guid OwnerPlayerId,
    string OwnerUsername,
    string OwnerAvatarUrl,
    BiomeType ActiveBiome,
    string ActiveBiomeLabel,
    string? ActiveGlbModelUrl,
    string? ActivePreviewImageUrl,
    string? ActiveSourceBlendFile,
    bool IsActiveBiomeAvailable,
    List<BiomeSlotInfoDto> CreatedBiomes,
    List<ShowcasePedestalDto> Pedestals,
    List<PlayerCardDto> InventoryCards
);

/// <summary>
/// Résultat d'une action dans la Vitrine 3D (création/suppression de biome, exposition, retrait ou déplacement de socle).
/// </summary>
public record ShowcaseActionResultDto(
    bool Success,
    string Message,
    ShowcaseViewDto? Showcase
);
