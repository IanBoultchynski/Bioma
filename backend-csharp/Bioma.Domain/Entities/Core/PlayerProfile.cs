using Bioma.Domain.Enums;

namespace Bioma.Domain.Entities;

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
