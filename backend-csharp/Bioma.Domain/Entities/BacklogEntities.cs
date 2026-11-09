using Bioma.Domain.Enums;

namespace Bioma.Domain.Entities;

// ============================================================================
// SOCLE PRÉPARATOIRE DES ENTITÉS POUR LES 66 USER STORIES (13 ÉPIQUES)
// (Terrain préparé pour l'implémentation progressive par épique)
// ============================================================================

/// <summary>
/// Épique 1 : Utilisateurs & Profil (US-01 à US-06) + Paramètres (US-66) + Elo (US-58).
/// </summary>
public class PlayerProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public int VirtualCurrency { get; set; }
    public int ExperiencePoints { get; set; }
    public int Energy { get; set; } = 100;
    public int EloRating { get; set; } = 1000;
    public int TotalVictories { get; set; }
    public int TotalDefeats { get; set; }
    public bool PushNotificationsEnabled { get; set; } = true;
    public DateTimeOffset LastCloudSyncAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Épique 2 : Capture IRL & Reconnaissance CLIP (US-07 à US-15).
/// </summary>
public class CaptureAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlayerId { get; set; }
    public string ImagePathOrBase64 { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public BiomeType DetectedBiome { get; set; } = BiomeType.ForetTemperee;
    public string? PredictedSpecies { get; set; }
    public double ConfidenceScore { get; set; }
    public bool IsRecognized { get; set; }
    public bool RequiresRetakeAngle { get; set; }
    public bool WasSyncedFromOfflineQueue { get; set; }
    public DateTimeOffset CapturedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Épique 3 : Expéditions Data & Globe 3D (US-16 à US-21, US-65).
/// </summary>
public class BiodiversityHotspot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public BiomeType Biome { get; set; }
    public Season ActiveSeason { get; set; }
    public int SpeciesDensityScore { get; set; }
}

/// <summary>
/// Mission d'expédition envoyée par un joueur sur un hotspot (US-18 à US-21).
/// </summary>
public class ExpeditionMission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlayerId { get; set; }
    public Guid CardId { get; set; }
    public Guid HotspotId { get; set; }
    public ExpeditionStatus Status { get; set; } = ExpeditionStatus.EnCours;
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset EndsAt { get; set; }
}

/// <summary>
/// Épique 4 : Quiz Chronométré (US-22 à US-26).
/// </summary>
public class BiologyQuizQuestion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string QuestionText { get; set; } = string.Empty;
    public List<string> Choices { get; set; } = new();
    public int CorrectChoiceIndex { get; set; }
    public int TimeLimitSeconds { get; set; } = 15;
    public int RewardCurrency { get; set; } = 50;
    public int RewardXp { get; set; } = 25;
}

/// <summary>
/// Épique 5 : Tirage / Gacha (US-27 à US-31).
/// </summary>
public class GachaPullRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlayerId { get; set; }
    public Guid ObtainedCardId { get; set; }
    public CardRarity Rarity { get; set; }
    public int CurrencySpent { get; set; }
    public bool IsMultiPull { get; set; }
    public DateTimeOffset PulledAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Épique 7 : Le Laboratoire & Fusion Hybride (US-37 à US-46).
/// </summary>
public class LaboratoryFusion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlayerId { get; set; }
    public Guid ParentCardAId { get; set; }
    public Guid ParentCardBId { get; set; }
    public bool UsedCatalystBoost { get; set; }
    public Guid? ResultingHybridCardId { get; set; }
    public string? GeneratedAiImageUrl { get; set; }
    public bool RefundedOnOverload { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Épique 8 : Marché aux Enchères (US-47 à US-51).
/// </summary>
public class AuctionListing
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SellerPlayerId { get; set; }
    public Guid CardId { get; set; }
    public int StartingPrice { get; set; }
    public int CurrentHighestBid { get; set; }
    public Guid? HighestBidderPlayerId { get; set; }
    public AuctionStatus Status { get; set; } = AuctionStatus.Active;
    public DateTimeOffset EndsAt { get; set; }
}

/// <summary>
/// Épique 9 : Social & Échanges (US-52 à US-54).
/// </summary>
public class TradeOffer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SenderPlayerId { get; set; }
    public Guid ReceiverPlayerId { get; set; }
    public Guid OfferedCardId { get; set; }
    public Guid? RequestedCardId { get; set; } // Null s'il s'agit d'un cadeau direct (US-54)
    public TradeStatus Status { get; set; } = TradeStatus.EnAttente;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Épique 10 : Batailles 1v1 (US-55 à US-60).
/// </summary>
public class BattleMatch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlayerAId { get; set; }
    public Guid PlayerBId { get; set; }
    public BiomeType ArenaBiome { get; set; }
    public BattleOutcome OutcomeForPlayerA { get; set; } = BattleOutcome.EnCours;
    public int EloDeltaPlayerA { get; set; }
    public DateTimeOffset PlayedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Épique 11 : Vitrine 3D / Terrarium virtuel (US-61 à US-63).
/// </summary>
public class ShowcasePedestal
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerPlayerId { get; set; }
    public Guid? DisplayedCardId { get; set; }
    public int SlotIndex { get; set; }
    public double PositionX { get; set; }
    public double PositionY { get; set; }
    public double PositionZ { get; set; }
}
