using Bioma.Domain.Enums;
using Bioma.Infrastructure.Data;
using Bioma.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bioma.Tests;

public class Showcase3DServiceTests
{
    private static BiomaDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<BiomaDbContext>()
            .UseInMemoryDatabase($"BiomaShowcaseTestDb_{Guid.NewGuid()}")
            .Options;

        return new BiomaDbContext(options);
    }

    [Fact]
    public async Task OverviewStartsEmpty_AndOnlyProposesTheSixAvailableBiomes()
    {
        using var db = CreateInMemoryContext();
        await BiomaDbSeeder.SeedAsync(db);

        var service = new Showcase3DService(db);
        var ianId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        var overview = await service.GetPlayerBiomesOverviewAsync(ianId);

        Assert.Empty(overview.CreatedBiomes);
        Assert.Equal(6, overview.AvailableBiomesToCreate.Count);
        Assert.All(overview.AvailableBiomesToCreate, b =>
        {
            Assert.True(b.IsAvailable);
            Assert.False(string.IsNullOrEmpty(b.PreviewImageUrl));
        });

        // Les 6 biomes présents en .blend/.glb sont proposés
        Assert.Contains(overview.AvailableBiomesToCreate, b => b.Biome == BiomeType.Taiga);
        Assert.Contains(overview.AvailableBiomesToCreate, b => b.Biome == BiomeType.Toundra);
        Assert.Contains(overview.AvailableBiomesToCreate, b => b.Biome == BiomeType.Desert);
        Assert.Contains(overview.AvailableBiomesToCreate, b => b.Biome == BiomeType.Montagne);
        Assert.Contains(overview.AvailableBiomesToCreate, b => b.Biome == BiomeType.ForetTemperee);
        Assert.Contains(overview.AvailableBiomesToCreate, b => b.Biome == BiomeType.Savane);

        // Les 4 biomes restant à faire ne sont pas encore disponibles
        Assert.DoesNotContain(overview.AvailableBiomesToCreate, b => b.Biome == BiomeType.ForetTropicale);
        Assert.DoesNotContain(overview.AvailableBiomesToCreate, b => b.Biome == BiomeType.ZoneUrbaine);
        Assert.DoesNotContain(overview.AvailableBiomesToCreate, b => b.Biome == BiomeType.MilieuMarin);
        Assert.DoesNotContain(overview.AvailableBiomesToCreate, b => b.Biome == BiomeType.ZoneHumide);

        // Aucun biome ne doit être forcé avant que le joueur ne le crée
        var showcaseBeforeCreate = await service.GetPlayerShowcaseAsync(ianId);
        Assert.Null(showcaseBeforeCreate);
    }

    [Fact]
    public async Task CreateBiomeAsync_AllowsFreeCardPlacementAcrossAnyCreatedBiome()
    {
        using var db = CreateInMemoryContext();
        await BiomaDbSeeder.SeedAsync(db);

        var service = new Showcase3DService(db);
        var ianId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        // Impossible de créer un biome pas encore modélisé (ex: Jungle)
        var unavailableRes = await service.CreateBiomeAsync(ianId, BiomeType.ForetTropicale);
        Assert.False(unavailableRes.Success);

        // Création du biome Forêt de Sapins
        var createRes = await service.CreateBiomeAsync(ianId, BiomeType.Taiga);
        Assert.True(createRes.Success);
        Assert.NotNull(createRes.Showcase);

        var showcase = createRes.Showcase!;
        Assert.Equal(BiomeType.Taiga, showcase.ActiveBiome);
        Assert.Equal("/models/biomes/BiomeSapins.glb", showcase.ActiveGlbModelUrl);
        Assert.Equal("/models/biomes/BiomeSapins.png", showcase.ActivePreviewImageUrl);
        Assert.Equal(5, showcase.Pedestals.Count);
        Assert.All(showcase.Pedestals, p => Assert.Null(p.DisplayedCard));

        // Toutes les cartes du joueur sont disponibles librement sans filtre par biome
        var playerCards = await db.PlayerCards
            .Include(pc => pc.SpeciesCard)
            .Where(pc => pc.PlayerId == ianId)
            .ToListAsync();
        Assert.Equal(playerCards.Count, showcase.InventoryCards.Count);

        var foxCard = playerCards.First(pc => pc.SpeciesCard!.ScientificName == "Vulpes vulpes");
        var lionCard = playerCards.First(pc => pc.SpeciesCard!.ScientificName == "Panthera leo");

        // On peut placer librement le Renard ET le Lion dans la Forêt de Sapins
        var okFox = await service.PlaceCardOnPedestalAsync(ianId, BiomeType.Taiga, 0, foxCard.Id);
        Assert.True(okFox.Success);

        var okLion = await service.PlaceCardOnPedestalAsync(ianId, BiomeType.Taiga, 1, lionCard.Id);
        Assert.True(okLion.Success);
        Assert.Equal(foxCard.Id, okLion.Showcase!.Pedestals[0].DisplayedCard?.Id);
        Assert.Equal(lionCard.Id, okLion.Showcase!.Pedestals[1].DisplayedCard?.Id);

        // Déplacement, retrait et suppression
        var moveRes = await service.UpdatePedestalPositionAsync(ianId, BiomeType.Taiga, 0, 1.25, 0.30, -1.15);
        Assert.True(moveRes.Success);
        Assert.Equal(1.25, moveRes.Showcase!.Pedestals[0].PositionX);

        var removeRes = await service.RemoveCardFromPedestalAsync(ianId, BiomeType.Taiga, 1);
        Assert.True(removeRes.Success);
        Assert.Null(removeRes.Showcase!.Pedestals[1].DisplayedCard);

        var delRes = await service.DeleteBiomeAsync(ianId, BiomeType.Taiga);
        Assert.True(delRes.Success);
        var overviewAfterDelete = await service.GetPlayerBiomesOverviewAsync(ianId);
        Assert.Empty(overviewAfterDelete.CreatedBiomes);
    }
}
