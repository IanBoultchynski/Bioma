namespace Bioma.Domain.Entities;

/// <summary>
/// Surenchère placée par un joueur sur une annonce (US-50, US-51).
/// </summary>
public class AuctionBid
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AuctionListingId { get; set; }
    public AuctionListing? AuctionListing { get; set; }

    public Guid BidderPlayerId { get; set; }
    public PlayerProfile? BidderPlayer { get; set; }

    public int BidAmount { get; set; }
    public bool IsRefunded { get; set; }
    public DateTimeOffset PlacedAt { get; set; } = DateTimeOffset.UtcNow;
}
