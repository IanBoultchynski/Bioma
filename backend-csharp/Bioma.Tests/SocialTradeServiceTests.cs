using Bioma.Domain.Entities;
using Bioma.Domain.Enums;
using Bioma.Infrastructure.Data;
using Bioma.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bioma.Tests;

public class SocialTradeServiceTests
{
    // Joueurs de démo créés par BiomaDbSeeder
    private static readonly Guid IanId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ArfaoumoId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static (SocialTradeService Service, BiomaDbContext Db) Create(string dbName)
    {
        var options = new DbContextOptionsBuilder<BiomaDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var db = new BiomaDbContext(options);
        BiomaDbSeeder.SeedAsync(db).GetAwaiter().GetResult();

        return (new SocialTradeService(db), db);
    }

    private static async Task MakeFriendsAsync(SocialTradeService service)
    {
        await service.SendFriendRequestAsync(IanId, "Arfaoumo");
        var overview = await service.GetOverviewAsync(ArfaoumoId);
        var request = Assert.Single(overview.PendingRequests, r => r.IsIncoming);
        await service.RespondToFriendRequestAsync(ArfaoumoId, request.FriendshipId, accept: true);
    }

    private static Task<PlayerCard> FirstAvailableCardAsync(BiomaDbContext db, Guid playerId) =>
        db.PlayerCards.FirstAsync(pc => pc.PlayerId == playerId && pc.State == CardState.Disponible);

    // ---------------------------------------------------------------- US-52

    [Fact]
    public async Task FriendRequest_ThenAccept_MakesBothPlayersFriends()
    {
        var (service, _) = Create(nameof(FriendRequest_ThenAccept_MakesBothPlayersFriends));

        var sent = await service.SendFriendRequestAsync(IanId, "arfaoumo");
        Assert.True(sent.Success);

        await MakeFriendsAsync(service);

        Assert.Contains((await service.GetOverviewAsync(IanId)).Friends, f => f.PlayerId == ArfaoumoId);
        Assert.Contains((await service.GetOverviewAsync(ArfaoumoId)).Friends, f => f.PlayerId == IanId);
    }

    [Fact]
    public async Task FriendRequest_ToSelfOrUnknownPlayer_IsRejected()
    {
        var (service, db) = Create(nameof(FriendRequest_ToSelfOrUnknownPlayer_IsRejected));

        Assert.False((await service.SendFriendRequestAsync(IanId, "Ian")).Success);
        Assert.False((await service.SendFriendRequestAsync(IanId, "joueur-qui-n-existe-pas")).Success);
        Assert.Equal(0, await db.Friendships.CountAsync());
    }

    [Fact]
    public async Task FriendRequest_Refused_IsDeleted()
    {
        var (service, db) = Create(nameof(FriendRequest_Refused_IsDeleted));
        await service.SendFriendRequestAsync(IanId, "Arfaoumo");
        var request = Assert.Single((await service.GetOverviewAsync(ArfaoumoId)).PendingRequests);

        var result = await service.RespondToFriendRequestAsync(ArfaoumoId, request.FriendshipId, accept: false);

        Assert.True(result.Success);
        Assert.Equal(0, await db.Friendships.CountAsync());
    }

    // ---------------------------------------------------------------- US-53

    [Fact]
    public async Task Trade_WithNonFriend_IsRejected()
    {
        var (service, db) = Create(nameof(Trade_WithNonFriend_IsRejected));
        var ianCard = await FirstAvailableCardAsync(db, IanId);
        var arfaoumoCard = await FirstAvailableCardAsync(db, ArfaoumoId);

        var result = await service.ProposeTradeAsync(IanId, ArfaoumoId, ianCard.Id, arfaoumoCard.Id);

        Assert.False(result.Success);
        Assert.Equal(0, await db.TradeOffers.CountAsync());
    }

    [Fact]
    public async Task Trade_Accepted_SwapsTheTwoCards()
    {
        var (service, db) = Create(nameof(Trade_Accepted_SwapsTheTwoCards));
        await MakeFriendsAsync(service);
        var ianCard = await FirstAvailableCardAsync(db, IanId);
        var arfaoumoCard = await FirstAvailableCardAsync(db, ArfaoumoId);

        Assert.True((await service.ProposeTradeAsync(IanId, ArfaoumoId, ianCard.Id, arfaoumoCard.Id)).Success);

        // Rien ne bouge tant que l'ami n'a pas accepté
        Assert.Equal(IanId, (await db.PlayerCards.FindAsync(ianCard.Id))!.PlayerId);

        var offer = Assert.Single((await service.GetOverviewAsync(ArfaoumoId)).IncomingOffers);
        var result = await service.RespondToTradeAsync(ArfaoumoId, offer.Id, accept: true);

        Assert.True(result.Success);
        Assert.Equal(ArfaoumoId, (await db.PlayerCards.FindAsync(ianCard.Id))!.PlayerId);
        Assert.Equal(IanId, (await db.PlayerCards.FindAsync(arfaoumoCard.Id))!.PlayerId);
        Assert.Equal(TradeStatus.Accepte, (await db.TradeOffers.FindAsync(offer.Id))!.Status);
    }

    [Fact]
    public async Task Trade_Refused_KeepsTheCards()
    {
        var (service, db) = Create(nameof(Trade_Refused_KeepsTheCards));
        await MakeFriendsAsync(service);
        var ianCard = await FirstAvailableCardAsync(db, IanId);
        var arfaoumoCard = await FirstAvailableCardAsync(db, ArfaoumoId);
        await service.ProposeTradeAsync(IanId, ArfaoumoId, ianCard.Id, arfaoumoCard.Id);
        var offer = Assert.Single((await service.GetOverviewAsync(ArfaoumoId)).IncomingOffers);

        await service.RespondToTradeAsync(ArfaoumoId, offer.Id, accept: false);

        Assert.Equal(IanId, (await db.PlayerCards.FindAsync(ianCard.Id))!.PlayerId);
        Assert.Equal(ArfaoumoId, (await db.PlayerCards.FindAsync(arfaoumoCard.Id))!.PlayerId);
        Assert.Equal(TradeStatus.Refuse, (await db.TradeOffers.FindAsync(offer.Id))!.Status);
    }

    [Fact]
    public async Task Trade_FailsAndIsCancelled_IfOfferedCardWasGivenAwayMeanwhile()
    {
        var (service, db) = Create(nameof(Trade_FailsAndIsCancelled_IfOfferedCardWasGivenAwayMeanwhile));
        await MakeFriendsAsync(service);
        var ianCard = await FirstAvailableCardAsync(db, IanId);
        var arfaoumoCard = await FirstAvailableCardAsync(db, ArfaoumoId);
        await service.ProposeTradeAsync(IanId, ArfaoumoId, ianCard.Id, arfaoumoCard.Id);
        var offer = Assert.Single((await service.GetOverviewAsync(ArfaoumoId)).IncomingOffers);

        // Ian envoie sa carte en expédition avant que l'échange soit accepté
        ianCard.State = CardState.EnExpedition;
        await db.SaveChangesAsync();

        var result = await service.RespondToTradeAsync(ArfaoumoId, offer.Id, accept: true);

        Assert.False(result.Success);
        Assert.Equal(IanId, (await db.PlayerCards.FindAsync(ianCard.Id))!.PlayerId);
        Assert.Equal(ArfaoumoId, (await db.PlayerCards.FindAsync(arfaoumoCard.Id))!.PlayerId);
        Assert.Equal(TradeStatus.Annule, (await db.TradeOffers.FindAsync(offer.Id))!.Status);
    }

    // ---------------------------------------------------------------- US-54

    [Fact]
    public async Task Gift_TransfersTheCardImmediately()
    {
        var (service, db) = Create(nameof(Gift_TransfersTheCardImmediately));
        await MakeFriendsAsync(service);
        var ianCard = await FirstAvailableCardAsync(db, IanId);

        var result = await service.SendGiftAsync(IanId, ArfaoumoId, ianCard.Id);

        Assert.True(result.Success);
        var card = (await db.PlayerCards.FindAsync(ianCard.Id))!;
        Assert.Equal(ArfaoumoId, card.PlayerId);
        Assert.False(card.IsInActiveDeck);
        Assert.Equal("Cadeau", card.ObtainedVia);
        Assert.Contains((await service.GetOverviewAsync(ArfaoumoId)).History, h => h.IsGift && h.IsIncoming);
    }

    [Fact]
    public async Task Gift_ToNonFriend_IsRejected()
    {
        var (service, db) = Create(nameof(Gift_ToNonFriend_IsRejected));
        var ianCard = await FirstAvailableCardAsync(db, IanId);

        var result = await service.SendGiftAsync(IanId, ArfaoumoId, ianCard.Id);

        Assert.False(result.Success);
        Assert.Equal(IanId, (await db.PlayerCards.FindAsync(ianCard.Id))!.PlayerId);
    }
}