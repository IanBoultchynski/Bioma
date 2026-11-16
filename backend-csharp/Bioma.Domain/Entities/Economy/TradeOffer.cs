using Bioma.Domain.Enums;

namespace Bioma.Domain.Entities;

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
