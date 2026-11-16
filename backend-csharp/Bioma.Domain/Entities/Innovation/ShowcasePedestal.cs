namespace Bioma.Domain.Entities;

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
