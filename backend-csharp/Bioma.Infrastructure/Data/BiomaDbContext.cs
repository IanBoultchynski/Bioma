using Bioma.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Bioma.Infrastructure.Data;

/// <summary>
/// Configuration partagée et résiliente de la base de données Bioma.
/// Tente de se connecter au PostgreSQL sous Docker ; si Docker n'est pas allumé, bascule
/// automatiquement et sans crash vers une base SQLite locale.
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

    /// <summary>
    /// Sonde la disponibilité de PostgreSQL avec un délai court (2 secondes max).
    /// </summary>
    public static bool CanConnectToPostgres(string connectionString, int timeoutSeconds = 2)
    {
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString)
            {
                Timeout = timeoutSeconds,
                CommandTimeout = timeoutSeconds
            };
            using var conn = new NpgsqlConnection(builder.ConnectionString);
            conn.Open();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static void Configure(DbContextOptionsBuilder options, IConfiguration configuration, string contentRootPath)
    {
        var postgresConn = configuration.GetConnectionString("Postgres");
        if (!string.IsNullOrWhiteSpace(postgresConn) && CanConnectToPostgres(postgresConn, timeoutSeconds: 2))
        {
            Console.WriteLine("[Bioma] Connexion PostgreSQL Docker établie avec succès.");
            options.UseNpgsql(postgresConn);
        }
        else
        {
            var sqliteFile = ResolveSqlitePath(contentRootPath);
            Console.WriteLine($"[Bioma] PostgreSQL non joignable (Docker éteint). Bascule transparente vers SQLite local : {sqliteFile}");
            options.UseSqlite($"Data Source={sqliteFile}");
        }
    }
}

/// <summary>
/// Contexte Entity Framework Core complet de la base de données Bioma.
/// Gère les entités modulaires, les index composites indispensables et les règles d'intégrité référentielle (Cascade / Restrict).
/// </summary>
public class BiomaDbContext : DbContext
{
    public BiomaDbContext(DbContextOptions<BiomaDbContext> options) : base(options)
    {
    }

    // Core
    public DbSet<SpeciesCard> Cards => Set<SpeciesCard>();
    public DbSet<PlayerProfile> Players => Set<PlayerProfile>();
    public DbSet<PlayerCard> PlayerCards => Set<PlayerCard>();

    // Innovation
    public DbSet<CaptureAttempt> CaptureAttempts => Set<CaptureAttempt>();
    public DbSet<LaboratoryFusion> LaboratoryFusions => Set<LaboratoryFusion>();
    public DbSet<ShowcasePedestal> ShowcasePedestals => Set<ShowcasePedestal>();

    // Gameplay
    public DbSet<BiodiversityHotspot> Hotspots => Set<BiodiversityHotspot>();
    public DbSet<ExpeditionMission> Expeditions => Set<ExpeditionMission>();
    public DbSet<BiologyQuizQuestion> QuizQuestions => Set<BiologyQuizQuestion>();
    public DbSet<QuizAttemptRecord> QuizAttempts => Set<QuizAttemptRecord>();
    public DbSet<BattleMatch> BattleMatches => Set<BattleMatch>();

    // Economy & Social
    public DbSet<GachaPullRecord> GachaPulls => Set<GachaPullRecord>();
    public DbSet<AuctionListing> AuctionListings => Set<AuctionListing>();
    public DbSet<AuctionBid> AuctionBids => Set<AuctionBid>();
    public DbSet<Friendship> Friendships => Set<Friendship>();
    public DbSet<TradeOffer> TradeOffers => Set<TradeOffer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ====================================================================
        // 1. CORE : SpeciesCard, PlayerProfile, PlayerCard
        // ====================================================================

        modelBuilder.Entity<SpeciesCard>(entity =>
        {
            entity.ToTable("SpeciesCards");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.ScientificName).HasMaxLength(128).IsRequired();
            entity.Property(c => c.CommonName).HasMaxLength(128);
            entity.Property(c => c.SourceApi).HasMaxLength(32);

            entity.HasIndex(c => c.ScientificName);
            entity.HasIndex(c => c.CommonName);
            entity.HasIndex(c => new { c.Biome, c.Rarity });
        });

        modelBuilder.Entity<PlayerProfile>(entity =>
        {
            entity.ToTable("Players");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Username).HasMaxLength(64).IsRequired();
            entity.Property(p => p.Email).HasMaxLength(128).IsRequired();

            entity.HasIndex(p => p.Username).IsUnique();
            entity.HasIndex(p => p.Email).IsUnique();
            entity.HasIndex(p => p.EloRating);
        });

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
            entity.HasIndex(pc => new { pc.PlayerId, pc.SpeciesCardId });
            entity.HasIndex(pc => new { pc.PlayerId, pc.Level });
        });

        // ====================================================================
        // 2. GAMEPLAY : BiodiversityHotspot, ExpeditionMission, Quiz, Battle
        // ====================================================================

        modelBuilder.Entity<BiodiversityHotspot>(entity =>
        {
            entity.ToTable("BiodiversityHotspots");
            entity.HasKey(h => h.Id);
            entity.Property(h => h.Name).HasMaxLength(128).IsRequired();
            entity.HasIndex(h => new { h.Biome, h.ActiveSeason });
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

            entity.HasIndex(e => new { e.PlayerId, e.Status });
            entity.HasIndex(e => new { e.HotspotId, e.Status });
        });

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

            entity.HasIndex(qa => new { qa.PlayerId, qa.AnsweredAt });
            entity.HasIndex(qa => new { qa.PlayerId, qa.QuestionId });
        });

        modelBuilder.Entity<BattleMatch>(entity =>
        {
            entity.ToTable("BattleMatches");
            entity.HasKey(bm => bm.Id);
            entity.HasIndex(bm => new { bm.PlayerAId, bm.PlayedAt });
            entity.HasIndex(bm => new { bm.PlayerBId, bm.PlayedAt });
        });

        // ====================================================================
        // 3. ECONOMY & SOCIAL : Auction, Trade, Gacha, Friendship
        // ====================================================================

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

            entity.HasIndex(g => new { g.PlayerId, g.PulledAt });
            entity.HasIndex(g => new { g.PlayerId, g.Rarity });
        });

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

            entity.HasIndex(al => new { al.Status, al.EndsAt });
            entity.HasIndex(al => new { al.SellerPlayerId, al.Status });
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

            entity.HasIndex(ab => new { ab.AuctionListingId, ab.BidAmount });
            entity.HasIndex(ab => new { ab.BidderPlayerId, ab.PlacedAt });
        });

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
            entity.HasIndex(t => new { t.SenderPlayerId, t.Status });
            entity.HasIndex(t => new { t.ReceiverPlayerId, t.Status });
        });

        // ====================================================================
        // 4. INNOVATION : CaptureAttempt, LaboratoryFusion, ShowcasePedestal
        // ====================================================================

        modelBuilder.Entity<CaptureAttempt>(entity =>
        {
            entity.ToTable("CaptureAttempts");
            entity.HasKey(ca => ca.Id);

            entity.HasOne(ca => ca.Player)
                .WithMany(p => p.Captures)
                .HasForeignKey(ca => ca.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(ca => new { ca.PlayerId, ca.CapturedAt });
            entity.HasIndex(ca => new { ca.PlayerId, ca.IsRecognized });
        });

        modelBuilder.Entity<LaboratoryFusion>(entity =>
        {
            entity.ToTable("LaboratoryFusions");
            entity.HasKey(lf => lf.Id);

            entity.HasOne(lf => lf.Player)
                .WithMany(p => p.LaboratoryFusions)
                .HasForeignKey(lf => lf.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(lf => new { lf.PlayerId, lf.CreatedAt });
        });

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

            entity.HasIndex(sp => new { sp.OwnerPlayerId, sp.SlotIndex }).IsUnique();
        });
    }
}
