namespace Bioma.Application.DTOs;

/// <summary>
/// US-02 : Requête de création d'un profil utilisateur avec identité unique.
/// </summary>
public record RegisterUserRequestDto(
    string Username,
    string Email,
    string Password,
    string? AvatarUrl = null
);

/// <summary>
/// US-01 : Requête de connexion sécurisée d'un joueur.
/// </summary>
public record LoginRequestDto(
    string UsernameOrEmail,
    string Password
);

/// <summary>
/// US-05 : Requête de modification de l'avatar de profil.
/// </summary>
public record UpdateAvatarRequestDto(
    string AvatarUrl
);

/// <summary>
/// US-03 : Carte possédée par un joueur dans son inventaire ou son deck actif.
/// </summary>
public record PlayerCardDto(
    Guid Id,
    Guid SpeciesCardId,
    string CommonName,
    string ScientificName,
    string Family,
    string Rarity,
    string Biome,
    string ImageUrl,
    string? CustomNickname,
    int Level,
    int ExperiencePoints,
    string Stage,
    string State,
    bool IsInActiveDeck,
    int CurrentAttack,
    int CurrentDefense,
    int CurrentSpeed,
    double WeightKg,
    string PolaroidFinish,
    string ObtainedVia,
    DateTimeOffset AcquiredAt
);

/// <summary>
/// US-06 : Statistiques globales d'un joueur (nombre de cartes, victoires, progression).
/// </summary>
public record PlayerGlobalStatsDto(
    int TotalCardsOwned,
    int UniqueSpeciesCount,
    int ActiveDeckCount,
    int AdultCardsCount,
    int TotalVictories,
    int TotalDefeats,
    double WinRatePercent,
    int EloRating,
    int Level,
    int ExperiencePoints,
    int VirtualCurrency,
    int Energy,
    DateTimeOffset LastCloudSyncAt
);

/// <summary>
/// US-04 : Résultat d'une synchronisation cloud de la progression du joueur.
/// </summary>
public record CloudSyncResultDto(
    Guid PlayerId,
    string Username,
    DateTimeOffset SyncedAt,
    int SavedCardsCount,
    int ActiveDeckCount,
    int ExperiencePoints,
    int VirtualCurrency,
    string CloudBackupToken,
    string CloudSnapshotJson,
    string StatusMessage
);

/// <summary>
/// US-04 : Requête de restauration d'un profil et de ses cartes depuis un code Cloud ou JSON Cloud.
/// </summary>
public record RestoreCloudRequestDto(
    string CloudTokenOrJson
);

/// <summary>
/// Résumé léger d'un profil utilisateur (pour sélection ou liste).
/// </summary>
public record UserProfileSummaryDto(
    Guid Id,
    string Username,
    string Email,
    string AvatarUrl,
    int Level,
    int VirtualCurrency,
    int TotalCards,
    int TotalVictories
);

/// <summary>
/// Profil complet du joueur incluant statistiques globales (US-06), deck actif et inventaire (US-03).
/// </summary>
public record UserProfileDto(
    Guid Id,
    string Username,
    string Email,
    string AvatarUrl,
    bool PushNotificationsEnabled,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastCloudSyncAt,
    PlayerGlobalStatsDto Stats,
    List<PlayerCardDto> ActiveDeck,
    List<PlayerCardDto> Inventory
);

/// <summary>
/// Réponse d'authentification (US-01, US-02).
/// </summary>
public record AuthResponseDto(
    bool Success,
    string Message,
    string? AuthToken,
    UserProfileDto? Profile
);
