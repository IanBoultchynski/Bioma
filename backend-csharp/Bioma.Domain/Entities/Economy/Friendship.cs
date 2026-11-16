namespace Bioma.Domain.Entities;

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
