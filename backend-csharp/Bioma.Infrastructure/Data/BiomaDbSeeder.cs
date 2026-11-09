using Microsoft.EntityFrameworkCore;

namespace Bioma.Infrastructure.Data;

/// <summary>
/// Initialise uniquement la structure relationnelle de la base de données sans aucune fausse donnée (pas de fake data).
/// </summary>
public static class BiomaDbSeeder
{
    public static async Task SeedAsync(BiomaDbContext db)
    {
        await db.Database.EnsureCreatedAsync();
    }
}
