using Bioma.Domain.Enums;

namespace Bioma.Domain.Entities;

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
