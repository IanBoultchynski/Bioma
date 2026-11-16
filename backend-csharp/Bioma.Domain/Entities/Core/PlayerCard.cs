using Bioma.Domain.Enums;

namespace Bioma.Domain.Entities;

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
