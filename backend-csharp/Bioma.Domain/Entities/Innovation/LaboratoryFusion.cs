namespace Bioma.Domain.Entities;

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
