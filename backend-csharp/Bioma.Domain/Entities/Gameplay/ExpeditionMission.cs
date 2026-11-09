using Bioma.Domain.Enums;

namespace Bioma.Domain.Entities;

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
