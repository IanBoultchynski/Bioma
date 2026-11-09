using Bioma.Application.DTOs;
using Bioma.Application.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace Bioma.Api.Hubs;

/// <summary>
/// Hub SignalR pour le Marché aux Enchères en temps réel (Cahier des Charges p.12-14 & User Stories #49-51).
/// Diffuse instantanément les surenchères ("AuctionUpdated") et notifie le joueur remboursé ("BidderRefunded").
/// </summary>
public class AuctionHub : Hub
{
    private readonly IBiomaGameService _gameService;

    public AuctionHub(IBiomaGameService gameService)
    {
        _gameService = gameService;
    }

    public async Task PlaceRealtimeBid(PlaceBidRequestDto request)
    {
        var (updatedListing, refundedBidderId, refundedAmount) = await _gameService.PlaceBidAsync(request);

        // Diffusion en direct à tous les joueurs connectés sur la page Marché aux Enchères
        await Clients.All.SendAsync("AuctionUpdated", updatedListing);

        // Notification de remboursement immédiat (User Story #51)
        if (refundedBidderId.HasValue)
        {
            await Clients.All.SendAsync("BidderRefunded", new
            {
                RefundedBidderId = refundedBidderId.Value,
                RefundedAmount = refundedAmount,
                CardName = updatedListing.CardName
            });
        }
    }
}
