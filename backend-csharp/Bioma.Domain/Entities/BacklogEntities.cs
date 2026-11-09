using Bioma.Domain.Enums;

namespace Bioma.Domain.Entities;

// ============================================================================
// SCHÉMA RELATIONNEL COMPLET DE LA BASE DE DONNÉES BIOMA
// (Utilisateurs, inventaire de cartes, progression, captures, expéditions,
//  quiz, gacha, laboratoire, enchères, social, batailles 1v1, vitrine 3D)
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

    // Progression globale et ressources du joueur
    public int VirtualCurrency { get; set; } = 500;
    public int ExperiencePoints { get; set; }
    public int Level { get; set; } = 1;
    public int Energy { get; set; } = 100;
    public int EloRating { get; set; } = 1000;
    public int TotalVictories { get; set; }
    public int TotalDefeats { get; set; }

    // Paramètres & Synchronisation Cloud (US-04, US-66)
    public bool PushNotificationsEnabled { get; set; } = true;
    public DateTimeOffset LastCloudSyncAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Contenu possédé par l'utilisateur (Relations 1-N)
    public ICollection<PlayerCard> InventoryCards { get; set; } = new List<PlayerCard>();
    public ICollection<CaptureAttempt> Captures { get; set; } = new List<CaptureAttempt>();
    public ICollection<ExpeditionMission> Expeditions { get; set; } = new List<ExpeditionMission>();
    public ICollection<QuizAttemptRecord> QuizAttempts { get; set; } = new List<QuizAttemptRecord>();
    public ICollection<GachaPullRecord> GachaPulls { get; set; } = new List<GachaPullRecord>();
    public ICollection<LaboratoryFusion> LaboratoryFusions { get; set; } = new List<LaboratoryFusion>();
    public ICollection<ShowcasePedestal> ShowcasePedestals { get; set; } = new List<ShowcasePedestal>();
}

/// <summary>
/// Exemplaire d'une carte possédée par un utilisateur avec sa progression individuelle
/// (US-03, US-32 à US-36, US-38, US-46, US-56).
/// </summary>
public class PlayerCard
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlayerId { get; set; }
    public PlayerProfile? Player { get; set; }

    public Guid SpeciesCardId { get; set; }
    public SpeciesCard? SpeciesCard { get; set; }

    public string? CustomNickname { get; set; }

    // Progression et évolution de l'animal (US-32 à US-36)
    public int ExperiencePoints { get; set; }
    public int Level { get; set; } = 1;
    public LifeStage Stage { get; set; } = LifeStage.Juvenile;
    public CardState State { get; set; } = CardState.Disponible;
    public bool IsInActiveDeck { get; set; }

    // Statistiques actuelles (évoluent avec le niveau, le stade Adulte ou le Boost catalyseur)
    public int CurrentAttack { get; set; } = 30;
    public int CurrentDefense { get; set; } = 30;
    public int CurrentSpeed { get; set; } = 30;
    public double WeightKg { get; set; } = 10.0;

    // Finition visuelle Polaroïd & soins (US-13, US-35, US-36)
    public string PolaroidFinish { get; set; } = "Standard"; // "Standard", "Bronze", "Or", "Holographique"
    public DateTimeOffset? LastDailyCareAt { get; set; }
    public string ObtainedVia { get; set; } = "Capture"; // "Capture", "Gacha", "Laboratoire", "Enchere", "Echange"
    public DateTimeOffset AcquiredAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Épique 2 : Capture IRL & Reconnaissance CLIP (US-07 à US-15).
/// </summary>
public class CaptureAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlayerId { get; set; }
    public PlayerProfile? Player { get; set; }

    public string ImagePathOrBase64 { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public BiomeType DetectedBiome { get; set; } = BiomeType.ForetTemperee;
    public string? PredictedSpecies { get; set; }
    public double ConfidenceScore { get; set; }
    public bool IsRecognized { get; set; }
    public bool RequiresRetakeAngle { get; set; }
    public bool WasSyncedFromOfflineQueue { get; set; }
    public Guid? UnlockedPlayerCardId { get; set; }
    public DateTimeOffset CapturedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Épique 3 : Expéditions Data & Globe 3D (US-16 à US-21, US-65).
/// </summary>
public class BiodiversityHotspot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string CountryOrRegion { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public BiomeType Biome { get; set; }
    public Season ActiveSeason { get; set; }
    public int SpeciesDensityScore { get; set; } = 75;
}

/// <summary>
/// Mission d'expédition envoyée par un joueur sur un hotspot (US-18 à US-21).
/// </summary>
public class ExpeditionMission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlayerId { get; set; }
    public PlayerProfile? Player { get; set; }

    public Guid PlayerCardId { get; set; }
    public PlayerCard? PlayerCard { get; set; }

    public Guid HotspotId { get; set; }
    public BiodiversityHotspot? Hotspot { get; set; }

    public ExpeditionStatus Status { get; set; } = ExpeditionStatus.EnCours;
    public int RewardCurrency { get; set; }
    public int RewardXp { get; set; }
    public Guid? DiscoveredSpeciesCardId { get; set; }
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset EndsAt { get; set; }
}

/// <summary>
/// Épique 4 : Question de Quiz Chronométré (US-22 à US-26).
/// </summary>
public class BiologyQuizQuestion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string QuestionText { get; set; } = string.Empty;
    public string ChoiceA { get; set; } = string.Empty;
    public string ChoiceB { get; set; } = string.Empty;
    public string ChoiceC { get; set; } = string.Empty;
    public string ChoiceD { get; set; } = string.Empty;
    public int CorrectChoiceIndex { get; set; } // 0..3
    public string ScientificExplanation { get; set; } = string.Empty;
    public int TimeLimitSeconds { get; set; } = 15;
    public int RewardCurrency { get; set; } = 50;
    public int RewardXp { get; set; } = 25;
}

/// <summary>
/// Historique de réponse d'un joueur à un Quiz Chronométré (US-23 à US-26).
/// </summary>
public class QuizAttemptRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlayerId { get; set; }
    public PlayerProfile? Player { get; set; }

    public Guid QuestionId { get; set; }
    public BiologyQuizQuestion? Question { get; set; }

    public int SelectedChoiceIndex { get; set; }
    public double ElapsedSeconds { get; set; }
    public bool IsTimeOut { get; set; }
    public bool IsCorrect { get; set; }
    public int EarnedCurrency { get; set; }
    public int EarnedXp { get; set; }
    public DateTimeOffset AnsweredAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Épique 5 : Historique des tirages Gacha d'un joueur (US-27 à US-31).
/// </summary>
public class GachaPullRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlayerId { get; set; }
    public PlayerProfile? Player { get; set; }

    public Guid PlayerCardId { get; set; }
    public PlayerCard? PlayerCard { get; set; }

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
    public PlayerProfile? Player { get; set; }

    public Guid ParentPlayerCardAId { get; set; }
    public Guid ParentPlayerCardBId { get; set; }
    public bool UsedCatalystBoost { get; set; }
    public int EnergySpent { get; set; }
    public int CurrencySpent { get; set; }
    public Guid? ResultingPlayerCardId { get; set; }
    public string? GeneratedAiImageUrl { get; set; }
    public bool RefundedOnOverload { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Épique 8 : Annonce sur le Marché aux Enchères (US-47 à US-51).
/// </summary>
public class AuctionListing
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SellerPlayerId { get; set; }
    public PlayerProfile? SellerPlayer { get; set; }

    public Guid PlayerCardId { get; set; }
    public PlayerCard? PlayerCard { get; set; }

    public int StartingPrice { get; set; }
    public int CurrentHighestBid { get; set; }
    public Guid? HighestBidderPlayerId { get; set; }
    public AuctionStatus Status { get; set; } = AuctionStatus.Active;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset EndsAt { get; set; }

    public ICollection<AuctionBid> Bids { get; set; } = new List<AuctionBid>();
}

/// <summary>
/// Surenchère placée par un joueur sur une annonce (US-50, US-51).
/// </summary>
public class AuctionBid
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AuctionListingId { get; set; }
    public AuctionListing? AuctionListing { get; set; }

    public Guid BidderPlayerId { get; set; }
    public PlayerProfile? BidderPlayer { get; set; }

    public int BidAmount { get; set; }
    public bool IsRefunded { get; set; }
    public DateTimeOffset PlacedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Épique 9 : Relation d'amitié entre deux joueurs (US-52).
/// </summary>
public class Friendship
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RequesterPlayerId { get; set; }
    public Guid AddresseePlayerId { get; set; }
    public bool IsAccepted { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Épique 9 : Proposition d'échange ou envoi de cadeau entre amis (US-53, US-54).
/// </summary>
public class TradeOffer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SenderPlayerId { get; set; }
    public Guid ReceiverPlayerId { get; set; }
    public Guid OfferedPlayerCardId { get; set; }
    public Guid? RequestedPlayerCardId { get; set; } // Null s'il s'agit d'un cadeau direct (US-54)
    public TradeStatus Status { get; set; } = TradeStatus.EnAttente;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Épique 10 : Match de Bataille 1v1 et impact Elo (US-55 à US-60).
/// </summary>
public class BattleMatch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlayerAId { get; set; }
    public Guid PlayerBId { get; set; }
    public BiomeType ArenaBiome { get; set; }
    public BattleOutcome OutcomeForPlayerA { get; set; } = BattleOutcome.EnCours;
    public int EloDeltaPlayerA { get; set; }
    public int EloDeltaPlayerB { get; set; }
    public string BattleSummaryLog { get; set; } = string.Empty;
    public DateTimeOffset PlayedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Épique 11 : Socle d'exposition dans la Vitrine 3D / Terrarium d'un joueur (US-61 à US-63).
/// </summary>
public class ShowcasePedestal
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerPlayerId { get; set; }
    public PlayerProfile? OwnerPlayer { get; set; }

    public Guid? DisplayedPlayerCardId { get; set; }
    public PlayerCard? DisplayedPlayerCard { get; set; }

    public int SlotIndex { get; set; }
    public double PositionX { get; set; }
    public double PositionY { get; set; }
    public double PositionZ { get; set; }
}
