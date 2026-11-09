using Bioma.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bioma.Infrastructure.Data;

/// <summary>
/// Contexte Entity Framework Core simplifié pour le début du Back-Office C#.
/// </summary>
public class BiomaDbContext : DbContext
{
    public BiomaDbContext(DbContextOptions<BiomaDbContext> options) : base(options)
    {
    }

    public DbSet<SpeciesCard> Cards => Set<SpeciesCard>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<SpeciesCard>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.ScientificName).HasMaxLength(128).IsRequired();
            entity.Property(c => c.CommonName).HasMaxLength(128);
            entity.Property(c => c.SourceApi).HasMaxLength(32);
        });
    }
}
