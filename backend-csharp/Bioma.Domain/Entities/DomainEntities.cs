using System.ComponentModel.DataAnnotations;

namespace Bioma.Domain.Entities;

public enum CardRarity
{
    Commune = 1,
    PeuCommune = 2,
    Rare = 3,
    Epique = 4,
    Legendaire = 5
}

public enum EvolutionStage
{
    Juvenile = 1,
    Adulte = 2
}

public enum AuctionStatus
{
    Active = 1,
    Completed = 2,
    Cancelled = 3
}

public enum ExpeditionStatus
{
    InProgress = 1,
    Completed = 2,
    Cancelled = 3
}

/// <summary>
/// Entité Joueur (Pôle Référentiel & Inventaire - Diagramme de classe p.6).
/// Identifiée par UUID (Guid) pour empêcher l'énumération d'API.
/// </summary>
public class Player
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Pseudo { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string AvatarInitials { get; set; } = "C";
    public int Level { get; set; } = 1;
    public int Experience { get; set; } = 0;
    public int FeuillesCurrency { get; set; } = 1240; // Monnaie virtuelle ("feuilles") visible sur le Figma
    public int EloRating { get; set; } = 1200;
    public int Victories { get; set; } = 8;
    public int Defeats { get; set; } = 3;
    public bool PushNotificationsEnabled { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Card> Cards { get; set; } = new List<Card>();
    public ICollection<Deck> Decks { get; set; } = new List<Deck>();
    public ICollection<BiomeShowcase> Biomes { get; set; } = new List<BiomeShowcase>();
}

/// <summary>
/// Entité Espèce (Référentiel scientifique alimenté par GBIF / iNaturalist).
/// </summary>
public class Species
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ScientificName { get; set; } = string.Empty;
    public string CommonName { get; set; } = string.Empty;
    public string Family { get; set; } = string.Empty;
    public string Kingdom { get; set; } = "Animalia"; // Animalia ou Plantae ("Animaux et plantes" dans Figma)
    public string Habitat { get; set; } = "Forêt";
    public string Description { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;

    // Données scientifiques réelles
    public double MaxWeightKg { get; set; }
    public double MaxSpeedKmh { get; set; }
    public double DangerIndex { get; set; } // 1..10
    public int GbifOccurrences { get; set; }

    // Statistiques de base gamifiées
    public int BaseHp { get; set; }
    public int BaseAttack { get; set; }
    public int BaseDefense { get; set; }
    public int BaseSpeed { get; set; }
    public CardRarity Rarity { get; set; } = CardRarity.Commune;
}

/// <summary>
/// Entité Carte (Polaroïd) appartenant à un joueur.
/// Implémente un verrouillage optimiste (RowVersion) et les clés étrangères explicites des parents hybrides (p.6).
/// </summary>
public class Card
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int SpecimenNumber { get; set; } = 1;

    public Guid OwnerId { get; set; }
    public Player? Owner { get; set; }

    public Guid? SpeciesId { get; set; }
    public Species? Species { get; set; }

    // Clés étrangères explicites de liaison pour les hybrides (exigé dans le Cahier des Charges p.6)
    public bool IsHybrid { get; set; }
    public Guid? ParentACardId { get; set; }
    public Guid? ParentBCardId { get; set; }
    public string CustomName { get; set; } = string.Empty;
    public string ScientificSubtitle { get; set; } = string.Empty;
    public string FamilyName { get; set; } = string.Empty;
    public string HabitatName { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;

    public CardRarity Rarity { get; set; } = CardRarity.Commune;
    public EvolutionStage Stage { get; set; } = EvolutionStage.Juvenile;
    public int CurrentXp { get; set; } = 0;
    public int MaxXpForEvolution { get; set; } = 1000;

    // Statistiques actuelles (évoluent au passage Adulte ou via Catalyseur)
    public int Hp { get; set; }
    public int Attack { get; set; }
    public int Defense { get; set; }
    public int Speed { get; set; }
    public double WeightKg { get; set; }
    public double DangerIndex { get; set; }

    // États de verrouillage transactionnel (Enchère, Expédition, Repos post-fusion, Vitrine 3D)
    public bool IsLockedForAuction { get; set; }
    public bool IsInExpedition { get; set; }
    public Guid? DisplayedInBiomeId { get; set; }
    public DateTimeOffset? RestingUntil { get; set; }
    public DateTimeOffset? LastCareAppliedAt { get; set; }

    // Métadonnées de capture (Timestamp DateTimeOffset indépendant du fuseau horaire)
    public DateTimeOffset CapturedAt { get; set; } = DateTimeOffset.UtcNow;
    public double? CaptureLatitude { get; set; }
    public double? CaptureLongitude { get; set; }

    /// <summary>
    /// Verrouillage optimiste (Optimistic Concurrency Control) exigé à la page 6 du Cahier des Charges.
    /// </summary>
    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

/// <summary>
/// Entité Vitrine 3D / Biome ("La Clairière", "Les Dunes", "Les Berges" sur le Figma p.16).
/// </summary>
public class BiomeShowcase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string BiomeType { get; set; } = "Forêt"; // Forêt, Désert, Zone humide
    public string Subtitle { get; set; } = string.Empty;
    public string BackgroundImageUrl { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int MaxSlots { get; set; } = 8;
    public List<Guid> ExposedCardIds { get; set; } = new();
}

/// <summary>
/// Entité Deck pour les Batailles 1v1.
/// </summary>
public class Deck
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlayerId { get; set; }
    public string Name { get; set; } = "Deck Principal";
    public bool IsActive { get; set; } = true;
    public List<Guid> ActiveCardIds { get; set; } = new();
}

/// <summary>
/// Entité ZoneGéographique (Hotspot généré par DBSCAN Python pour le Globe 3D).
/// </summary>
public class GeoHotspotZone
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string RegionName { get; set; } = string.Empty;
    public string TargetScientificName { get; set; } = string.Empty;
    public string TargetCommonName { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double RadiusKm { get; set; } = 85.0;
    public string OptimalSeason { get; set; } = "Printemps";
    public bool IsSeasonOpen { get; set; } = true;
    public double DensityScore { get; set; } = 0.85;
}

/// <summary>
/// Entité Expédition (Timers typés en DateTimeOffset - p.6).
/// </summary>
public class Expedition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlayerId { get; set; }
    public Guid CompanionCardId { get; set; }
    public string CompanionName { get; set; } = string.Empty;
    public string TargetSpeciesName { get; set; } = string.Empty;
    public string RegionName { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset EndsAt { get; set; } = DateTimeOffset.UtcNow.AddHours(24);
    public int DurationHours { get; set; } = 24;
    public ExpeditionStatus Status { get; set; } = ExpeditionStatus.InProgress;
    public int RewardFeuilles { get; set; } = 120;
    public int RewardXp { get; set; } = 150;
}

/// <summary>
/// Entité Enchère (Marché aux enchères temps réel SignalR - p.6 & p.19).
/// </summary>
public class AuctionListing
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int LotNumber { get; set; }
    public Guid CardId { get; set; }
    public Card? Card { get; set; }

    public Guid SellerId { get; set; }
    public string SellerPseudo { get; set; } = string.Empty;

    public Guid? HighestBidderId { get; set; }
    public string? HighestBidderPseudo { get; set; }

    public int StartingPrice { get; set; }
    public int CurrentBid { get; set; }
    public int BidCount { get; set; }
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset EndsAt { get; set; }
    public AuctionStatus Status { get; set; } = AuctionStatus.Active;

    public ICollection<AuctionBid> Bids { get; set; } = new List<AuctionBid>();
}

/// <summary>
/// Entité Offre sur une enchère.
/// </summary>
public class AuctionBid
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AuctionListingId { get; set; }
    public Guid BidderId { get; set; }
    public string BidderPseudo { get; set; } = string.Empty;
    public int Amount { get; set; }
    public DateTimeOffset BidAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Entité Quiz Chronométré (Validation serveur stricte des 10 secondes - Diagramme de séquence p.7).
/// </summary>
public class QuizQuestion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Theme { get; set; } = "Reconnaître les espèces";
    public string ScientificSubtitle { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string QuestionText { get; set; } = string.Empty;
    public List<string> Options { get; set; } = new();
    public int CorrectOptionIndex { get; set; }
    public int TimeLimitSeconds { get; set; } = 10;
    public int RewardXp { get; set; } = 80;
    public int RewardFeuilles { get; set; } = 45;
}

/// <summary>
/// Session active de Quiz pour calculer côté serveur : tempsEcoule = now() - dateDebutQuiz.
/// </summary>
public class ActiveQuizAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlayerId { get; set; }
    public Guid QuestionId { get; set; }
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsAnswered { get; set; }
}

/// <summary>
/// Entité Combat1v1 (Historique et calcul scientifique de puissance - Diagramme de séquence p.7).
/// </summary>
public class Combat1v1
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AttackerId { get; set; }
    public string AttackerPseudo { get; set; } = string.Empty;
    public Guid DefenderId { get; set; }
    public string DefenderPseudo { get; set; } = string.Empty;
    public string ArenaBiome { get; set; } = "Forêt";
    public double AttackerScore { get; set; }
    public double DefenderScore { get; set; }
    public bool AttackerWon { get; set; }
    public int EloDelta { get; set; }
    public string CombatSummary { get; set; } = string.Empty;
    public DateTimeOffset FoughtAt { get; set; } = DateTimeOffset.UtcNow;
}
