using Bioma.Domain.Enums;
using Bioma.Infrastructure.Data;
using Bioma.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bioma.Tests;

public class GachaServiceTests
{
    // Joueur de démo créé par BiomaDbSeeder (1250 de monnaie)
    private static readonly Guid AlphaId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static (GachaService Service, BiomaDbContext Db) Create(string dbName, int seed = 42)
    {
        var options = new DbContextOptionsBuilder<BiomaDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var db = new BiomaDbContext(options);
        BiomaDbSeeder.SeedAsync(db).GetAwaiter().GetResult();

        return (new GachaService(db, new Random(seed)), db);
    }

    [Fact]
    public async Task Pull_Single_DebitsCurrencyAndAddsCardToInventory()
    {
        var (service, db) = Create(nameof(Pull_Single_DebitsCurrencyAndAddsCardToInventory));
        int before = (await db.Players.FindAsync(AlphaId))!.VirtualCurrency;
        int cardsBefore = await db.PlayerCards.CountAsync(pc => pc.PlayerId == AlphaId);

        var result = await service.PullAsync(AlphaId, 1);

        Assert.True(result.Success);
        Assert.Single(result.Cards);
        Assert.Equal(before - GachaService.SinglePullCost, result.RemainingCurrency);
        Assert.Equal(cardsBefore + 1, await db.PlayerCards.CountAsync(pc => pc.PlayerId == AlphaId));
        Assert.Equal(1, await db.GachaPulls.CountAsync(g => g.PlayerId == AlphaId));
    }

    [Fact]
    public async Task Pull_Multi_ReturnsTenCardsWithAtLeastOneRare()
    {
        var (service, _) = Create(nameof(Pull_Multi_ReturnsTenCardsWithAtLeastOneRare));

        var result = await service.PullAsync(AlphaId, GachaService.MultiPullCount);

        Assert.True(result.Success);
        Assert.Equal(GachaService.MultiPullCount, result.Cards.Count);
        Assert.Contains(result.Cards, c => c.Rarity >= CardRarity.Rare);
    }

    [Fact]
    public async Task Pull_WithoutEnoughCurrency_FailsAndChangesNothing()
    {
        var (service, db) = Create(nameof(Pull_WithoutEnoughCurrency_FailsAndChangesNothing));
        var player = (await db.Players.FindAsync(AlphaId))!;
        player.VirtualCurrency = 50;
        await db.SaveChangesAsync();

        var result = await service.PullAsync(AlphaId, 1);

        Assert.False(result.Success);
        Assert.Empty(result.Cards);
        Assert.Equal(50, (await db.Players.FindAsync(AlphaId))!.VirtualCurrency);
        Assert.Equal(0, await db.GachaPulls.CountAsync());
    }

    [Fact]
    public async Task Pull_InvalidCount_IsRejected()
    {
        var (service, _) = Create(nameof(Pull_InvalidCount_IsRejected));

        var result = await service.PullAsync(AlphaId, 5);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Banner_OddsSumTo100()
    {
        var (service, _) = Create(nameof(Banner_OddsSumTo100));

        var banner = await service.GetBannerAsync(AlphaId);

        Assert.Equal(100, banner.Odds.Sum(o => o.Percent), precision: 1);
    }
}