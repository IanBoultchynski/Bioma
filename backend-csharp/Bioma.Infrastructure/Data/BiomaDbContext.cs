using Bioma.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bioma.Infrastructure.Data;

/// <summary>
/// Contexte Entity Framework Core configuré pour PostgreSQL (et InMemory pour le mode démo/test).
/// Respecte les exigences de la page 6 du Cahier des Charges :
/// - Clés primaires UUID (Guid) contre l'énumération d'API
/// - Clés étrangères explicites de liaison (ParentACardId, ParentBCardId, SellerId, HighestBidderId)
/// - Timestamps en DateTimeOffset
/// - Verrouillage optimiste via RowVersion sur l'entité Card (Carte)
/// </summary>
public class BiomaDbContext : DbContext
{
    public BiomaDbContext(DbContextOptions<BiomaDbContext> options) : base(options)
    {
    }

    public DbSet<Player> Players => Set<Player>();
    public DbSet<Species> Species => Set<Species>();
    public DbSet<Card> Cards => Set<Card>();
    public DbSet<BiomeShowcase> Biomes => Set<BiomeShowcase>();
    public DbSet<Deck> Decks => Set<Deck>();
    public DbSet<GeoHotspotZone> HotspotZones => Set<GeoHotspotZone>();
    public DbSet<Expedition> Expeditions => Set<Expedition>();
    public DbSet<AuctionListing> AuctionListings => Set<AuctionListing>();
    public DbSet<AuctionBid> AuctionBids => Set<AuctionBid>();
    public DbSet<QuizQuestion> QuizQuestions => Set<QuizQuestion>();
    public DbSet<ActiveQuizAttempt> ActiveQuizAttempts => Set<ActiveQuizAttempt>();
    public DbSet<Combat1v1> Combats => Set<Combat1v1>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Player>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Pseudo).HasMaxLength(64).IsRequired();
        });

        modelBuilder.Entity<Species>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.HasIndex(s => s.ScientificName).IsUnique();
        });

        modelBuilder.Entity<Card>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.RowVersion).IsRowVersion();

            entity.HasOne(c => c.Owner)
                .WithMany(p => p.Cards)
                .HasForeignKey(c => c.OwnerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(c => c.Species)
                .WithMany()
                .HasForeignKey(c => c.SpeciesId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<AuctionListing>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.HasOne(a => a.Card)
                .WithMany()
                .HasForeignKey(a => a.CardId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(a => a.Bids)
                .WithOne()
                .HasForeignKey(b => b.AuctionListingId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
