using Bioma.Domain.Enums;

namespace Bioma.Domain.Entities;

/// <summary>
/// Épique 8 : Annonce sur le Marché aux Enchères (US-47 à US-51).
/// </summary>
public class AuctionListing
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SellerPlayerId { get; set; }
    public PlayerProfile? SellerPlayer { get; set; }

    public Guid PlayerCardId { get; set; }
    public PlayerCard? PlayerCard { get; set; }

    public int StartingPrice { get; set; }
    public int CurrentHighestBid { get; set; }
    public Guid? HighestBidderPlayerId { get; set; }
    public AuctionStatus Status { get; set; } = AuctionStatus.Active;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset EndsAt { get; set; }

    public ICollection<AuctionBid> Bids { get; set; } = new List<AuctionBid>();
}
