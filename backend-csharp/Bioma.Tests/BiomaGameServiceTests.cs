using Bioma.Application.DTOs;
using Bioma.Infrastructure.Data;
using Bioma.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bioma.Tests;

public class BiomaGameServiceTests
{
    private static BiomaGameService CreateService(string dbName)
    {
        var options = new DbContextOptionsBuilder<BiomaDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var db = new BiomaDbContext(options);
        BiomaDbSeeder.SeedAsync(db).GetAwaiter().GetResult();

        var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        return new BiomaGameService(db, httpClient);
    }

    [Fact]
    public async Task SearchApisAsync_ReturnsGbifAndInaturalistObservations()
    {
        var service = CreateService(nameof(SearchApisAsync_ReturnsGbifAndInaturalistObservations));

        var result = await service.SearchApisAsync("Vulpes vulpes", limitPerSource: 2);

        Assert.NotNull(result);
        Assert.True(result.GbifCount > 0);
        Assert.True(result.InaturalistCount > 0);
        Assert.Contains(result.Observations, o => o.SourceApi == "GBIF");
        Assert.Contains(result.Observations, o => o.SourceApi == "iNaturalist");
    }

    [Fact]
    public async Task SaveAndDeleteCard_PersistsInBackOfficeDatabase()
    {
        var service = CreateService(nameof(SaveAndDeleteCard_PersistsInBackOfficeDatabase));

        var initialCards = await service.GetSavedCardsAsync();
        int initialCount = initialCards.Count;

        var newObs = new ExternalObservationDto(
            "gbif-999",
            "GBIF",
            "Lynx lynx",
            "Lynx boréal",
            "Felidae",
            "Animalia",
            46.5,
            6.2,
            "Jura, France",
            "2025-05-01",
            "https://images.unsplash.com/photo-1546182990-dffeafbe841d?auto=format&fit=crop&w=600&q=80"
        );

        var saved = await service.SaveObservationToBackOfficeAsync(newObs);
        var afterSave = await service.GetSavedCardsAsync();
        Assert.Equal(initialCount + 1, afterSave.Count);

        var deleted = await service.DeleteCardAsync(saved.Id);
        Assert.True(deleted);
    }
}
