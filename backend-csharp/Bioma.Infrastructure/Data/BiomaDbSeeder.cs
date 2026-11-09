using Bioma.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bioma.Infrastructure.Data;

/// <summary>
/// Données initiales minimalistes pour le début du Back-Office C#.
/// </summary>
public static class BiomaDbSeeder
{
    public static async Task SeedAsync(BiomaDbContext db)
    {
        if (await db.Cards.AnyAsync())
        {
            return;
        }

        db.Cards.AddRange(
            new SpeciesCard
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                CommonName = "Renard roux",
                ScientificName = "Vulpes vulpes",
                Family = "Canidae",
                Kingdom = "Animalia",
                SourceApi = "GBIF",
                ExternalId = "gbif-2434559",
                Latitude = 47.4684,
                Longitude = -0.5562,
                CountryOrPlace = "France (Vallée de la Loire)",
                ObservedDate = "2025-04-12",
                ImageUrl = "https://images.unsplash.com/photo-1474511320723-9a56873867b5?auto=format&fit=crop&w=600&q=80"
            },
            new SpeciesCard
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                CommonName = "Martin-pêcheur d'Europe",
                ScientificName = "Alcedo atthis",
                Family = "Alcedinidae",
                Kingdom = "Animalia",
                SourceApi = "iNaturalist",
                ExternalId = "inat-4829104",
                Latitude = 43.5512,
                Longitude = 4.6214,
                CountryOrPlace = "France (Camargue)",
                ObservedDate = "2025-05-03",
                ImageUrl = "https://images.unsplash.com/photo-1444464666168-49d633b86797?auto=format&fit=crop&w=600&q=80"
            }
        );

        await db.SaveChangesAsync();
    }
}
