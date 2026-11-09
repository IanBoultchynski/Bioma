using Bioma.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Bioma.Infrastructure.Data;

/// <summary>
/// Configuration partagée de la base de données Bioma (PostgreSQL sous Docker ou SQLite persistant en local).
/// </summary>
public static class BiomaDbConfiguration
{
    public static string ResolveSqlitePath(string contentRootPath)
    {
        var dir = new DirectoryInfo(contentRootPath);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Bioma.sln")))
        {
            dir = dir.Parent;
        }

        var rootDir = dir?.FullName ?? contentRootPath;
        var dataDir = Path.Combine(rootDir, "data");
        Directory.CreateDirectory(dataDir);
        return Path.Combine(dataDir, "bioma.db");
    }

    public static void Configure(DbContextOptionsBuilder options, IConfiguration configuration, string contentRootPath)
    {
        var postgresConn = configuration.GetConnectionString("Postgres");
        if (!string.IsNullOrWhiteSpace(postgresConn))
        {
            options.UseNpgsql(postgresConn);
        }
        else
        {
            var sqliteFile = ResolveSqlitePath(contentRootPath);
            options.UseSqlite($"Data Source={sqliteFile}");
        }
    }
}

/// <summary>
/// Contexte Entity Framework Core complet de la base de données Bioma.
/// Gère les utilisateurs, leurs cartes et leur progression, ainsi que l'ensemble des modules des 13 épiques.
/// </summary>
public class BiomaDbContext : DbContext
{
    public BiomaDbContext(DbContextOptions<BiomaDbContext> options) : base(options)
    {
    }

    // Catalogue d'espèces et observations (Back-Office)
    public DbSet<SpeciesCard> Cards => Set<SpeciesCard>();

    // Utilisateurs, inventaire de cartes et progression
    public DbSet<PlayerProfile> Players => Set<PlayerProfile>();
    public DbSet<PlayerCard> PlayerCards => Set<PlayerCard>();

    // Épique 2 : Capture IRL
    public DbSet<CaptureAttempt> CaptureAttempts => Set<CaptureAttempt>();

    // Épique 3 : Expéditions Data & Globe 3D
    public DbSet<BiodiversityHotspot> Hotspots => Set<BiodiversityHotspot>();
    public DbSet<ExpeditionMission> Expeditions => Set<ExpeditionMission>();

    // Épique 4 : Quiz Chronométré
    public DbSet<BiologyQuizQuestion> QuizQuestions => Set<BiologyQuizQuestion>();
    public DbSet<QuizAttemptRecord> QuizAttempts => Set<QuizAttemptRecord>();

    // Épique 5 : Tirage (Gacha)
    public DbSet<GachaPullRecord> GachaPulls => Set<GachaPullRecord>();

    // Épique 7 : Le Laboratoire
    public DbSet<LaboratoryFusion> LaboratoryFusions => Set<LaboratoryFusion>();

    // Épique 8 : Marché aux Enchères
    public DbSet<AuctionListing> AuctionListings => Set<AuctionListing>();
    public DbSet<AuctionBid> AuctionBids => Set<AuctionBid>();

    // Épique 9 : Social & Échanges
    public DbSet<Friendship> Friendships => Set<Friendship>();
    public DbSet<TradeOffer> TradeOffers => Set<TradeOffer>();

    // Épique 10 : Batailles 1v1
    public DbSet<BattleMatch> BattleMatches => Set<BattleMatch>();

    // Épique 11 : Vitrine 3D
    public DbSet<ShowcasePedestal> ShowcasePedestals => Set<ShowcasePedestal>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1. Catalogue d'espèces (SpeciesCard)
        modelBuilder.Entity<SpeciesCard>(entity =>
        {
            entity.ToTable("SpeciesCards");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.ScientificName).HasMaxLength(128).IsRequired();
            entity.Property(c => c.CommonName).HasMaxLength(128);
            entity.Property(c => c.SourceApi).HasMaxLength(32);
            entity.HasIndex(c => c.ScientificName);
        });

        // 2. Utilisateurs (PlayerProfile)
        modelBuilder.Entity<PlayerProfile>(entity =>
        {
            entity.ToTable("Players");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Username).HasMaxLength(64).IsRequired();
            entity.Property(p => p.Email).HasMaxLength(128).IsRequired();
            entity.HasIndex(p => p.Username).IsUnique();
            entity.HasIndex(p => p.Email).IsUnique();
        });

        // 3. Inventaire de cartes et progression d'un joueur (PlayerCard)
        modelBuilder.Entity<PlayerCard>(entity =>
        {
            entity.ToTable("PlayerCards");
            entity.HasKey(pc => pc.Id);
            entity.Property(pc => pc.PolaroidFinish).HasMaxLength(32);
            entity.Property(pc => pc.ObtainedVia).HasMaxLength(32);

            entity.HasOne(pc => pc.Player)
                .WithMany(p => p.InventoryCards)
                .HasForeignKey(pc => pc.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(pc => pc.SpeciesCard)
                .WithMany(sc => sc.PlayerCards)
                .HasForeignKey(pc => pc.SpeciesCardId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(pc => new { pc.PlayerId, pc.IsInActiveDeck });
        });

        // 4. Captures IRL (CaptureAttempt)
        modelBuilder.Entity<CaptureAttempt>(entity =>
        {
            entity.ToTable("CaptureAttempts");
            entity.HasKey(ca => ca.Id);
            entity.HasOne(ca => ca.Player)
                .WithMany(p => p.Captures)
                .HasForeignKey(ca => ca.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 5. Hotspots & Expéditions (BiodiversityHotspot, ExpeditionMission)
        modelBuilder.Entity<BiodiversityHotspot>(entity =>
        {
            entity.ToTable("BiodiversityHotspots");
            entity.HasKey(h => h.Id);
            entity.Property(h => h.Name).HasMaxLength(128).IsRequired();
        });

        modelBuilder.Entity<ExpeditionMission>(entity =>
        {
            entity.ToTable("ExpeditionMissions");
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Player)
                .WithMany(p => p.Expeditions)
                .HasForeignKey(e => e.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.PlayerCard)
                .WithMany()
                .HasForeignKey(e => e.PlayerCardId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Hotspot)
                .WithMany()
                .HasForeignKey(e => e.HotspotId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 6. Quiz Chronométré (BiologyQuizQuestion, QuizAttemptRecord)
        modelBuilder.Entity<BiologyQuizQuestion>(entity =>
        {
            entity.ToTable("BiologyQuizQuestions");
            entity.HasKey(q => q.Id);
            entity.Property(q => q.QuestionText).HasMaxLength(512).IsRequired();
        });

        modelBuilder.Entity<QuizAttemptRecord>(entity =>
        {
            entity.ToTable("QuizAttempts");
            entity.HasKey(qa => qa.Id);
            entity.HasOne(qa => qa.Player)
                .WithMany(p => p.QuizAttempts)
                .HasForeignKey(qa => qa.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(qa => qa.Question)
                .WithMany()
                .HasForeignKey(qa => qa.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 7. Tirage Gacha (GachaPullRecord)
        modelBuilder.Entity<GachaPullRecord>(entity =>
        {
            entity.ToTable("GachaPulls");
            entity.HasKey(g => g.Id);
            entity.HasOne(g => g.Player)
                .WithMany(p => p.GachaPulls)
                .HasForeignKey(g => g.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(g => g.PlayerCard)
                .WithMany()
                .HasForeignKey(g => g.PlayerCardId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 8. Laboratoire & Fusions (LaboratoryFusion)
        modelBuilder.Entity<LaboratoryFusion>(entity =>
        {
            entity.ToTable("LaboratoryFusions");
            entity.HasKey(lf => lf.Id);
            entity.HasOne(lf => lf.Player)
                .WithMany(p => p.LaboratoryFusions)
                .HasForeignKey(lf => lf.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 9. Marché aux Enchères (AuctionListing, AuctionBid)
        modelBuilder.Entity<AuctionListing>(entity =>
        {
            entity.ToTable("AuctionListings");
            entity.HasKey(al => al.Id);
            entity.HasOne(al => al.SellerPlayer)
                .WithMany()
                .HasForeignKey(al => al.SellerPlayerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(al => al.PlayerCard)
                .WithMany()
                .HasForeignKey(al => al.PlayerCardId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AuctionBid>(entity =>
        {
            entity.ToTable("AuctionBids");
            entity.HasKey(ab => ab.Id);
            entity.HasOne(ab => ab.AuctionListing)
                .WithMany(al => al.Bids)
                .HasForeignKey(ab => ab.AuctionListingId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ab => ab.BidderPlayer)
                .WithMany()
                .HasForeignKey(ab => ab.BidderPlayerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 10. Social & Échanges (Friendship, TradeOffer)
        modelBuilder.Entity<Friendship>(entity =>
        {
            entity.ToTable("Friendships");
            entity.HasKey(f => f.Id);
            entity.HasIndex(f => new { f.RequesterPlayerId, f.AddresseePlayerId }).IsUnique();
        });

        modelBuilder.Entity<TradeOffer>(entity =>
        {
            entity.ToTable("TradeOffers");
            entity.HasKey(t => t.Id);
        });

        // 11. Batailles 1v1 (BattleMatch)
        modelBuilder.Entity<BattleMatch>(entity =>
        {
            entity.ToTable("BattleMatches");
            entity.HasKey(bm => bm.Id);
            entity.HasIndex(bm => bm.PlayerAId);
            entity.HasIndex(bm => bm.PlayerBId);
        });

        // 12. Vitrine 3D (ShowcasePedestal)
        modelBuilder.Entity<ShowcasePedestal>(entity =>
        {
            entity.ToTable("ShowcasePedestals");
            entity.HasKey(sp => sp.Id);
            entity.HasOne(sp => sp.OwnerPlayer)
                .WithMany(p => p.ShowcasePedestals)
                .HasForeignKey(sp => sp.OwnerPlayerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(sp => sp.DisplayedPlayerCard)
                .WithMany()
                .HasForeignKey(sp => sp.DisplayedPlayerCardId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
