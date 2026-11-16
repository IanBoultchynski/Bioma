using Bioma.Domain.Enums;

namespace Bioma.Domain.Entities;

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
