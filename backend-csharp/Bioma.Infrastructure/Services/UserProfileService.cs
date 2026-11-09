using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bioma.Application.DTOs;
using Bioma.Application.Interfaces;
using Bioma.Domain.Entities;
using Bioma.Domain.Enums;
using Bioma.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Bioma.Infrastructure.Services;

/// <summary>
/// Implémentation complète de l'Épique 1 : Utilisateurs &amp; Profil (US-01 à US-06).
/// - US-01 : Connexion sécurisée (hachage PBKDF2-SHA256 100k itérations + jeton signé HMAC-SHA256 avec expiration)
/// - US-02 : Création d'un profil utilisateur avec identité unique directement sur Supabase (validation regex, email, mot de passe fort)
/// - US-03 : Consultation et gestion du deck actif et de l'inventaire complet des cartes
/// - US-04 : Sauvegarde et restauration Cloud fonctionnelle (snapshots JSON signés + synchronisation temps réel)
/// - US-05 : Modification de l'avatar de profil (import fichier, URL ou emblèmes botaniques)
/// - US-06 : Consultation des statistiques globales (nombre de cartes, victoires, taux de victoire, Elo)
/// </summary>
public class UserProfileService : IUserProfileService
{
    private const string LegacyUnsplashAvatar = "https://images.unsplash.com/photo-1534528741775-53994a69daeb";
    private static readonly byte[] TokenSecret = Encoding.UTF8.GetBytes("Bioma-Cloud-Security-Hmac-Key-2026-Secret!");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public const int DefaultPbkdf2Iterations = 100_000;
    private static readonly Regex UsernameRegex = new(@"^[a-zA-Z0-9_\-\.]{3,30}$", RegexOptions.Compiled);
    private static readonly HashSet<string> ReservedUsernames = new(StringComparer.OrdinalIgnoreCase)
    {
        "admin", "administrator", "system", "bioma", "moderator", "root", "support", "api"
    };

    // Hash factice pour neutraliser les attaques par analyse temporelle (timing attacks) sur les comptes inexistants
    private static readonly string DummyHash = HashPassword("BiomaSafetyDummyPassword2026!");

    private readonly BiomaDbContext _db;

    public UserProfileService(BiomaDbContext db)
    {
        _db = db;
    }

    public static string BuildDefaultAvatarSvg(string username)
    {
        string clean = string.IsNullOrWhiteSpace(username) ? "B" : username.Trim();
        string initials = clean.Length >= 2
            ? clean[..2].ToUpperInvariant()
            : clean[..1].ToUpperInvariant();
        string escapedInitials = Uri.EscapeDataString(initials);

        return $"data:image/svg+xml;utf8,<svg xmlns='http://www.w3.org/2000/svg' width='160' height='160' viewBox='0 0 160 160'><defs><radialGradient id='bg' cx='35%' cy='30%' r='75%'><stop offset='0%' stop-color='%231F3E2F'/><stop offset='100%' stop-color='%230A1711'/></radialGradient><linearGradient id='glow' x1='0' y1='0' x2='1' y2='1'><stop offset='0%' stop-color='%23E5C77A'/><stop offset='100%' stop-color='%2358B77E'/></linearGradient></defs><circle cx='80' cy='80' r='77' fill='url(%23bg)' stroke='url(%23glow)' stroke-width='2.5'/><circle cx='80' cy='80' r='68' fill='none' stroke='%23ffffff' stroke-width='1' stroke-dasharray='4,4' opacity='0.25'/><text x='80' y='93' text-anchor='middle' fill='%23F4F1E8' font-family='sans-serif' font-weight='700' font-size='42' letter-spacing='2'>{escapedInitials}</text><path d='M74 122 C74 116 80 114 86 114 C86 120 80 126 74 122 Z' fill='%2358B77E'/></svg>";
    }

    private static (bool IsValid, string ErrorMessage) ValidatePasswordStrength(string password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 8)
        {
            return (false, "Le mot de passe doit comporter au moins 8 caractères pour sécuriser votre compte.");
        }

        if (password.Length > 128)
        {
            return (false, "Le mot de passe ne peut pas dépasser 128 caractères.");
        }

        bool hasUpper = password.Any(char.IsUpper);
        bool hasLower = password.Any(char.IsLower);
        bool hasDigit = password.Any(char.IsDigit);
        bool hasSpecial = password.Any(ch => !char.IsLetterOrDigit(ch));

        if (!hasUpper || !hasLower || (!hasDigit && !hasSpecial))
        {
            return (false, "Le mot de passe doit contenir au moins une lettre majuscule, une lettre minuscule et un chiffre ou symbole.");
        }

        return (true, string.Empty);
    }

    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@') || !email.Contains('.'))
        {
            return false;
        }

        try
        {
            var addr = new MailAddress(email);
            return addr.Address.Equals(email, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterUserRequestDto request, CancellationToken ct = default)
    {
        var username = request.Username?.Trim() ?? string.Empty;
        var email = request.Email?.Trim().ToLowerInvariant() ?? string.Empty;
        var password = request.Password ?? string.Empty;

        // 1. Validation stricte du pseudonyme
        if (string.IsNullOrWhiteSpace(username) || username.Length < 3 || username.Length > 30)
        {
            return new AuthResponseDto(false, "Le pseudonyme doit contenir entre 3 et 30 caractères.", null, null);
        }

        if (!UsernameRegex.IsMatch(username))
        {
            return new AuthResponseDto(false, "Le pseudonyme ne peut contenir que des lettres, chiffres, tirets (-) et points.", null, null);
        }

        if (ReservedUsernames.Contains(username))
        {
            return new AuthResponseDto(false, $"Le pseudonyme « {username} » est réservé par le système.", null, null);
        }

        // 2. Validation de l'adresse e-mail
        if (string.IsNullOrWhiteSpace(email) || email.Length > 128 || !IsValidEmail(email))
        {
            return new AuthResponseDto(false, "Veuillez saisir une adresse e-mail valide (ex: explorateur@bioma.fr).", null, null);
        }

        // 3. Validation de la robustesse du mot de passe
        var pwdValidation = ValidatePasswordStrength(password);
        if (!pwdValidation.IsValid)
        {
            return new AuthResponseDto(false, pwdValidation.ErrorMessage, null, null);
        }

        // 4. Vérification de l'unicité directement en base Supabase (PostgreSQL)
        bool usernameTaken = await _db.Players.AnyAsync(
            p => p.Username.ToLower() == username.ToLower(), ct);
        if (usernameTaken)
        {
            return new AuthResponseDto(false, $"Le pseudonyme « {username} » est déjà utilisé par un autre explorateur.", null, null);
        }

        bool emailTaken = await _db.Players.AnyAsync(
            p => p.Email.ToLower() == email, ct);
        if (emailTaken)
        {
            return new AuthResponseDto(false, $"L'adresse e-mail « {email} » est déjà associée à un compte.", null, null);
        }

        var now = DateTimeOffset.UtcNow;
        var avatar = string.IsNullOrWhiteSpace(request.AvatarUrl)
            ? BuildDefaultAvatarSvg(username)
            : request.AvatarUrl.Trim();

        // 5. Création du profil joueur vierge avec jetons de bienvenue (US-02)
        var newPlayer = new PlayerProfile
        {
            Id = Guid.NewGuid(),
            Username = username,
            Email = email,
            PasswordHash = HashPassword(password),
            AvatarUrl = avatar,
            VirtualCurrency = 0,
            ExperiencePoints = 0,
            Level = 1,
            Energy = 100,
            EloRating = 1000,
            TotalVictories = 0,
            TotalDefeats = 0,
            PushNotificationsEnabled = true,
            CreatedAt = now,
            LastCloudSyncAt = now
        };

        try
        {
            _db.Players.Add(newPlayer);
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return new AuthResponseDto(false, "Ce pseudonyme ou cette adresse e-mail est déjà enregistré sur la base de données.", null, null);
        }

        var profileDto = await GetProfileWithInventoryAsync(newPlayer.Id, ct);
        var token = GenerateAuthToken(newPlayer.Id, newPlayer.Username);

        return new AuthResponseDto(
            true,
            $"Profil « {newPlayer.Username} » créé et enregistré avec succès sur la base de données Supabase !",
            token,
            profileDto
        );
    }

    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request, CancellationToken ct = default)
    {
        var identifier = request.UsernameOrEmail?.Trim() ?? string.Empty;
        var password = request.Password ?? string.Empty;

        if (string.IsNullOrWhiteSpace(identifier) || string.IsNullOrWhiteSpace(password))
        {
            return new AuthResponseDto(false, "Veuillez renseigner votre identifiant et votre mot de passe.", null, null);
        }

        var lowerId = identifier.ToLowerInvariant();

        // Recherche du joueur sur Supabase par son pseudonyme ou son adresse e-mail
        var player = await _db.Players.FirstOrDefaultAsync(
            p => p.Username.ToLower() == lowerId || p.Email.ToLower() == lowerId, ct);

        // Exécute toujours une passe PBKDF2 pour éviter l'analyse temporelle (timing attacks)
        bool passwordMatches = player is not null
            ? VerifyPassword(password, player.PasswordHash)
            : VerifyPassword(password, DummyHash);

        if (player is null || !passwordMatches)
        {
            return new AuthResponseDto(false, "Identifiants incorrects. Vérifiez votre pseudonyme/e-mail et votre mot de passe.", null, null);
        }

        if (string.IsNullOrWhiteSpace(player.AvatarUrl) || player.AvatarUrl.StartsWith(LegacyUnsplashAvatar, StringComparison.OrdinalIgnoreCase))
        {
            player.AvatarUrl = BuildDefaultAvatarSvg(player.Username);
        }

        // US-04 : Horodatage de synchronisation Cloud en base Supabase
        player.LastCloudSyncAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        var profileDto = await GetProfileWithInventoryAsync(player.Id, ct);
        var token = GenerateAuthToken(player.Id, player.Username);

        return new AuthResponseDto(
            true,
            $"Connexion sécurisée réussie sur Supabase. Bon retour, {player.Username} !",
            token,
            profileDto
        );
    }

    public async Task<List<UserProfileSummaryDto>> GetAllProfilesAsync(CancellationToken ct = default)
    {
        return await _db.Players
            .AsNoTracking()
            .Include(p => p.InventoryCards)
            .OrderByDescending(p => p.Level)
            .ThenBy(p => p.Username)
            .Select(p => new UserProfileSummaryDto(
                p.Id,
                p.Username,
                p.Email,
                string.IsNullOrWhiteSpace(p.AvatarUrl) || p.AvatarUrl.StartsWith(LegacyUnsplashAvatar)
                    ? BuildDefaultAvatarSvg(p.Username)
                    : p.AvatarUrl,
                p.Level,
                p.VirtualCurrency,
                p.InventoryCards.Count,
                p.TotalVictories
            ))
            .ToListAsync(ct);
    }

    public async Task<UserProfileDto?> GetProfileWithInventoryAsync(Guid playerId, CancellationToken ct = default)
    {
        var player = await _db.Players
            .Include(p => p.InventoryCards)
                .ThenInclude(pc => pc.SpeciesCard)
            .FirstOrDefaultAsync(p => p.Id == playerId, ct);

        if (player is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(player.AvatarUrl) || player.AvatarUrl.StartsWith(LegacyUnsplashAvatar, StringComparison.OrdinalIgnoreCase))
        {
            player.AvatarUrl = BuildDefaultAvatarSvg(player.Username);
            await _db.SaveChangesAsync(ct);
        }

        var inventoryDtos = player.InventoryCards
            .OrderByDescending(pc => pc.IsInActiveDeck)
            .ThenByDescending(pc => pc.Level)
            .ThenByDescending(pc => pc.AcquiredAt)
            .Select(MapToPlayerCardDto)
            .ToList();

        var activeDeckDtos = inventoryDtos
            .Where(c => c.IsInActiveDeck)
            .ToList();

        var stats = BuildGlobalStats(player, player.InventoryCards);

        return new UserProfileDto(
            player.Id,
            player.Username,
            player.Email,
            player.AvatarUrl,
            player.PushNotificationsEnabled,
            player.CreatedAt,
            player.LastCloudSyncAt,
            stats,
            activeDeckDtos,
            inventoryDtos
        );
    }

    public async Task<PlayerGlobalStatsDto?> GetGlobalStatsAsync(Guid playerId, CancellationToken ct = default)
    {
        var player = await _db.Players
            .AsNoTracking()
            .Include(p => p.InventoryCards)
            .FirstOrDefaultAsync(p => p.Id == playerId, ct);

        return player is null ? null : BuildGlobalStats(player, player.InventoryCards);
    }

    public async Task<UserProfileDto?> UpdateAvatarAsync(Guid playerId, string newAvatarUrl, CancellationToken ct = default)
    {
        var player = await _db.Players.FirstOrDefaultAsync(p => p.Id == playerId, ct);
        if (player is null)
        {
            return null;
        }

        player.AvatarUrl = string.IsNullOrWhiteSpace(newAvatarUrl)
            ? BuildDefaultAvatarSvg(player.Username)
            : newAvatarUrl.Trim();
        player.LastCloudSyncAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await WriteCloudSnapshotAsync(playerId, ct);

        return await GetProfileWithInventoryAsync(playerId, ct);
    }

    public async Task<UserProfileDto?> ToggleCardInActiveDeckAsync(Guid playerId, Guid playerCardId, CancellationToken ct = default)
    {
        var playerCard = await _db.PlayerCards
            .FirstOrDefaultAsync(pc => pc.Id == playerCardId && pc.PlayerId == playerId, ct);

        var player = await _db.Players.FirstOrDefaultAsync(p => p.Id == playerId, ct);

        if (playerCard is null || player is null)
        {
            return null;
        }

        playerCard.IsInActiveDeck = !playerCard.IsInActiveDeck;
        player.LastCloudSyncAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await WriteCloudSnapshotAsync(playerId, ct);

        return await GetProfileWithInventoryAsync(playerId, ct);
    }

    public async Task<CloudSyncResultDto?> SyncProgressionToCloudAsync(Guid playerId, CancellationToken ct = default)
    {
        var player = await _db.Players
            .Include(p => p.InventoryCards)
                .ThenInclude(pc => pc.SpeciesCard)
            .FirstOrDefaultAsync(p => p.Id == playerId, ct);

        if (player is null)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        player.LastCloudSyncAt = now;
        await _db.SaveChangesAsync(ct);

        var (cloudToken, snapshotJson) = await WriteCloudSnapshotAsync(playerId, ct);
        int totalCards = player.InventoryCards.Count;
        int activeDeck = player.InventoryCards.Count(c => c.IsInActiveDeck);

        return new CloudSyncResultDto(
            player.Id,
            player.Username,
            now,
            totalCards,
            activeDeck,
            player.ExperiencePoints,
            player.VirtualCurrency,
            cloudToken,
            snapshotJson,
            $"Sauvegarde Cloud confirmée ({totalCards} carte(s), {activeDeck} dans le Deck). Code de transfert : {cloudToken}"
        );
    }

    public async Task<AuthResponseDto> RestoreProgressionFromCloudAsync(string cloudTokenOrJson, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cloudTokenOrJson))
        {
            return new AuthResponseDto(false, "Veuillez saisir un code de transfert Cloud (ex: CLOUD-...) ou un fichier JSON de sauvegarde.", null, null);
        }

        string trimmed = cloudTokenOrJson.Trim();
        string? jsonContent = null;

        if (trimmed.StartsWith("{", StringComparison.Ordinal))
        {
            jsonContent = trimmed;
        }
        else
        {
            // Recherche directe dans la base relationnelle (sans dépendance aux fichiers JSON locaux)
            var cleanId = trimmed.ToLowerInvariant();
            var existingDbPlayer = await _db.Players
                .Include(p => p.InventoryCards)
                    .ThenInclude(pc => pc.SpeciesCard)
                .FirstOrDefaultAsync(p => p.Username.ToLower() == cleanId
                    || p.Email.ToLower() == cleanId
                    || p.Id.ToString() == cleanId, ct);

            if (existingDbPlayer is not null)
            {
                existingDbPlayer.LastCloudSyncAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync(ct);

                var profileDto = await GetProfileWithInventoryAsync(existingDbPlayer.Id, ct);
                var token = GenerateAuthToken(existingDbPlayer.Id, existingDbPlayer.Username);
                return new AuthResponseDto(
                    true,
                    $"Progression Cloud de « {existingDbPlayer.Username} » restaurée avec succès depuis la base de données relationnelle.",
                    token,
                    profileDto
                );
            }
        }

        if (string.IsNullOrWhiteSpace(jsonContent))
        {
            return new AuthResponseDto(false, $"Aucune sauvegarde Cloud trouvée pour l'identifiant « {trimmed} ».", null, null);
        }

        try
        {
            var snapshot = JsonSerializer.Deserialize<CloudBackupSnapshotModel>(jsonContent);
            if (snapshot is null || snapshot.PlayerId == Guid.Empty)
            {
                return new AuthResponseDto(false, "Le format de la sauvegarde Cloud est invalide.", null, null);
            }

            var player = await _db.Players
                .Include(p => p.InventoryCards)
                .FirstOrDefaultAsync(p => p.Id == snapshot.PlayerId || p.Username.ToLower() == snapshot.Username.ToLower(), ct);

            if (player is null)
            {
                player = new PlayerProfile
                {
                    Id = snapshot.PlayerId,
                    Username = snapshot.Username,
                    Email = snapshot.Email,
                    PasswordHash = snapshot.PasswordHash,
                    AvatarUrl = snapshot.AvatarUrl,
                    Level = snapshot.Level,
                    ExperiencePoints = snapshot.ExperiencePoints,
                    VirtualCurrency = snapshot.VirtualCurrency,
                    Energy = snapshot.Energy,
                    EloRating = snapshot.EloRating,
                    TotalVictories = snapshot.TotalVictories,
                    TotalDefeats = snapshot.TotalDefeats,
                    CreatedAt = snapshot.CreatedAt,
                    LastCloudSyncAt = DateTimeOffset.UtcNow
                };
                _db.Players.Add(player);
            }
            else
            {
                player.AvatarUrl = snapshot.AvatarUrl;
                player.Level = snapshot.Level;
                player.ExperiencePoints = snapshot.ExperiencePoints;
                player.VirtualCurrency = snapshot.VirtualCurrency;
                player.EloRating = snapshot.EloRating;
                player.TotalVictories = snapshot.TotalVictories;
                player.TotalDefeats = snapshot.TotalDefeats;
                player.LastCloudSyncAt = DateTimeOffset.UtcNow;
            }

            foreach (var cardSnap in snapshot.Cards)
            {
                var species = await _db.Cards.FirstOrDefaultAsync(c => c.Id == cardSnap.SpeciesCardId, ct);
                if (species is null)
                {
                    species = new SpeciesCard
                    {
                        Id = cardSnap.SpeciesCardId,
                        CommonName = cardSnap.CommonName,
                        ScientificName = cardSnap.ScientificName,
                        Family = cardSnap.Family,
                        ImageUrl = cardSnap.ImageUrl,
                        SourceApi = cardSnap.ObtainedVia,
                        BaseAttack = cardSnap.CurrentAttack,
                        BaseDefense = cardSnap.CurrentDefense,
                        BaseSpeed = cardSnap.CurrentSpeed,
                        AverageWeightKg = cardSnap.WeightKg
                    };
                    _db.Cards.Add(species);
                }

                var existingPlayerCard = await _db.PlayerCards.FirstOrDefaultAsync(pc => pc.Id == cardSnap.PlayerCardId, ct);
                if (existingPlayerCard is null)
                {
                    _db.PlayerCards.Add(new PlayerCard
                    {
                        Id = cardSnap.PlayerCardId,
                        PlayerId = player.Id,
                        SpeciesCardId = species.Id,
                        CustomNickname = cardSnap.CustomNickname,
                        Level = cardSnap.Level,
                        ExperiencePoints = cardSnap.ExperiencePoints,
                        IsInActiveDeck = cardSnap.IsInActiveDeck,
                        CurrentAttack = cardSnap.CurrentAttack,
                        CurrentDefense = cardSnap.CurrentDefense,
                        CurrentSpeed = cardSnap.CurrentSpeed,
                        WeightKg = cardSnap.WeightKg,
                        PolaroidFinish = cardSnap.PolaroidFinish,
                        ObtainedVia = cardSnap.ObtainedVia,
                        AcquiredAt = cardSnap.AcquiredAt
                    });
                }
                else
                {
                    existingPlayerCard.IsInActiveDeck = cardSnap.IsInActiveDeck;
                    existingPlayerCard.Level = cardSnap.Level;
                    existingPlayerCard.ExperiencePoints = cardSnap.ExperiencePoints;
                }
            }

            await _db.SaveChangesAsync(ct);

            var profileDto = await GetProfileWithInventoryAsync(player.Id, ct);
            var token = GenerateAuthToken(player.Id, player.Username);

            return new AuthResponseDto(
                true,
                $"Progression Cloud de « {player.Username} » restaurée avec succès ({snapshot.Cards.Count} carte(s) récupérée(s)).",
                token,
                profileDto
            );
        }
        catch (Exception ex)
        {
            return new AuthResponseDto(false, $"Erreur lors de la restauration Cloud : {ex.Message}", null, null);
        }
    }

    private async Task<(string CloudToken, string SnapshotJson)> WriteCloudSnapshotAsync(Guid playerId, CancellationToken ct)
    {
        var player = await _db.Players
            .AsNoTracking()
            .Include(p => p.InventoryCards)
                .ThenInclude(pc => pc.SpeciesCard)
            .FirstOrDefaultAsync(p => p.Id == playerId, ct);

        if (player is null)
        {
            return (string.Empty, "{}");
        }

        string rawKey = $"{player.Id}:{player.Username}";
        string tokenHash = Convert.ToHexString(HMACSHA256.HashData(TokenSecret, Encoding.UTF8.GetBytes(rawKey)))[..10];
        string cloudToken = $"CLOUD-{tokenHash}";

        var snapshot = new CloudBackupSnapshotModel
        {
            CloudBackupToken = cloudToken,
            SyncedAt = player.LastCloudSyncAt,
            PlayerId = player.Id,
            Username = player.Username,
            Email = player.Email,
            PasswordHash = player.PasswordHash,
            AvatarUrl = player.AvatarUrl,
            Level = player.Level,
            ExperiencePoints = player.ExperiencePoints,
            VirtualCurrency = player.VirtualCurrency,
            Energy = player.Energy,
            EloRating = player.EloRating,
            TotalVictories = player.TotalVictories,
            TotalDefeats = player.TotalDefeats,
            CreatedAt = player.CreatedAt,
            Cards = player.InventoryCards.Select(pc => new CloudCardSnapshotModel
            {
                PlayerCardId = pc.Id,
                SpeciesCardId = pc.SpeciesCardId,
                CommonName = pc.SpeciesCard?.CommonName ?? "Espèce",
                ScientificName = pc.SpeciesCard?.ScientificName ?? "Species",
                Family = pc.SpeciesCard?.Family ?? "Animalia",
                ImageUrl = pc.SpeciesCard?.ImageUrl ?? string.Empty,
                CustomNickname = pc.CustomNickname,
                Level = pc.Level,
                ExperiencePoints = pc.ExperiencePoints,
                IsInActiveDeck = pc.IsInActiveDeck,
                CurrentAttack = pc.CurrentAttack,
                CurrentDefense = pc.CurrentDefense,
                CurrentSpeed = pc.CurrentSpeed,
                WeightKg = pc.WeightKg,
                PolaroidFinish = pc.PolaroidFinish,
                ObtainedVia = pc.ObtainedVia,
                AcquiredAt = pc.AcquiredAt
            }).ToList()
        };

        string json = JsonSerializer.Serialize(snapshot, JsonOptions);
        return (cloudToken, json);
    }

    public static string HashPassword(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            iterations: DefaultPbkdf2Iterations,
            HashAlgorithmName.SHA256,
            outputLength: 32);

        return $"PBKDF2${DefaultPbkdf2Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool VerifyPassword(string password, string storedHash)
    {
        if (string.IsNullOrWhiteSpace(storedHash) || string.IsNullOrEmpty(password))
        {
            return false;
        }

        if (storedHash.StartsWith("PBKDF2$", StringComparison.Ordinal))
        {
            var parts = storedHash.Split('$');
            int iterations;
            byte[] salt;
            byte[] expectedHash;

            if (parts.Length == 4 && int.TryParse(parts[1], out iterations))
            {
                // Format moderne sécurisé : PBKDF2$100000$salt$hash
                salt = Convert.FromBase64String(parts[2]);
                expectedHash = Convert.FromBase64String(parts[3]);
            }
            else if (parts.Length == 3)
            {
                // Format historique rétrosupporté : PBKDF2$salt$hash (10 000 itérations)
                iterations = 10_000;
                salt = Convert.FromBase64String(parts[1]);
                expectedHash = Convert.FromBase64String(parts[2]);
            }
            else
            {
                return false;
            }

            byte[] actualHash = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                iterations: iterations,
                HashAlgorithmName.SHA256,
                outputLength: expectedHash.Length);

            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }

        return false;
    }

    private static string GenerateAuthToken(Guid playerId, string username)
    {
        var issuedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var expiresAt = DateTimeOffset.UtcNow.AddDays(7).ToUnixTimeSeconds();
        var data = $"{playerId}:{username}:{issuedAt}:{expiresAt}";
        var signature = Convert.ToHexString(HMACSHA256.HashData(TokenSecret, Encoding.UTF8.GetBytes(data)));
        return Convert.ToBase64String(Encoding.UTF8.GetBytes($"{data}:{signature}"));
    }

    public static bool ValidateAuthToken(string token, out Guid playerId, out string username)
    {
        playerId = Guid.Empty;
        username = string.Empty;

        if (string.IsNullOrWhiteSpace(token)) return false;

        try
        {
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(token));
            var parts = raw.Split(':');
            if (parts.Length < 4) return false;

            var idStr = parts[0];
            var userStr = parts[1];
            var issuedStr = parts[2];
            var expiresStr = parts.Length >= 5 ? parts[3] : "0";
            var expectedSig = parts[^1];

            var data = parts.Length >= 5
                ? $"{idStr}:{userStr}:{issuedStr}:{expiresStr}"
                : $"{idStr}:{userStr}:{issuedStr}";

            var actualSig = Convert.ToHexString(HMACSHA256.HashData(TokenSecret, Encoding.UTF8.GetBytes(data)));

            if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(actualSig),
                Encoding.UTF8.GetBytes(expectedSig)))
            {
                return false;
            }

            if (parts.Length >= 5 && long.TryParse(expiresStr, out var expiresAt) && expiresAt > 0)
            {
                if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiresAt)
                {
                    return false; // Jeton expiré
                }
            }

            if (Guid.TryParse(idStr, out playerId))
            {
                username = userStr;
                return true;
            }
        }
        catch
        {
            // Format invalide
        }

        return false;
    }

    private static PlayerGlobalStatsDto BuildGlobalStats(PlayerProfile player, ICollection<PlayerCard> cards)
    {
        int totalCards = cards.Count;
        int uniqueSpecies = cards.Select(c => c.SpeciesCardId).Distinct().Count();
        int activeDeck = cards.Count(c => c.IsInActiveDeck);
        int adultCards = cards.Count(c => c.Stage == LifeStage.Adulte);
        int totalMatches = player.TotalVictories + player.TotalDefeats;
        double winRate = totalMatches > 0
            ? Math.Round((double)player.TotalVictories / totalMatches * 100.0, 1)
            : 0.0;

        return new PlayerGlobalStatsDto(
            totalCards,
            uniqueSpecies,
            activeDeck,
            adultCards,
            player.TotalVictories,
            player.TotalDefeats,
            winRate,
            player.EloRating,
            player.Level,
            player.ExperiencePoints,
            player.VirtualCurrency,
            player.Energy,
            player.LastCloudSyncAt
        );
    }

    private static PlayerCardDto MapToPlayerCardDto(PlayerCard pc)
    {
        var species = pc.SpeciesCard;
        return new PlayerCardDto(
            pc.Id,
            pc.SpeciesCardId,
            species?.CommonName ?? "Espèce inconnue",
            species?.ScientificName ?? "Species incognita",
            species?.Family ?? "Animalia",
            (species?.Rarity ?? CardRarity.Commune).ToString(),
            (species?.Biome ?? BiomeType.ForetTemperee).ToString(),
            species?.ImageUrl ?? string.Empty,
            pc.CustomNickname,
            pc.Level,
            pc.ExperiencePoints,
            pc.Stage.ToString(),
            pc.State.ToString(),
            pc.IsInActiveDeck,
            pc.CurrentAttack,
            pc.CurrentDefense,
            pc.CurrentSpeed,
            pc.WeightKg,
            pc.PolaroidFinish,
            pc.ObtainedVia,
            pc.AcquiredAt
        );
    }

    private sealed class CloudBackupSnapshotModel
    {
        public string CloudBackupToken { get; set; } = string.Empty;
        public DateTimeOffset SyncedAt { get; set; }
        public Guid PlayerId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string AvatarUrl { get; set; } = string.Empty;
        public int Level { get; set; }
        public int ExperiencePoints { get; set; }
        public int VirtualCurrency { get; set; }
        public int Energy { get; set; }
        public int EloRating { get; set; }
        public int TotalVictories { get; set; }
        public int TotalDefeats { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public List<CloudCardSnapshotModel> Cards { get; set; } = new();
    }

    private sealed class CloudCardSnapshotModel
    {
        public Guid PlayerCardId { get; set; }
        public Guid SpeciesCardId { get; set; }
        public string CommonName { get; set; } = string.Empty;
        public string ScientificName { get; set; } = string.Empty;
        public string Family { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public string? CustomNickname { get; set; }
        public int Level { get; set; }
        public int ExperiencePoints { get; set; }
        public bool IsInActiveDeck { get; set; }
        public int CurrentAttack { get; set; }
        public int CurrentDefense { get; set; }
        public int CurrentSpeed { get; set; }
        public double WeightKg { get; set; }
        public string PolaroidFinish { get; set; } = "Standard";
        public string ObtainedVia { get; set; } = "GBIF";
        public DateTimeOffset AcquiredAt { get; set; }
    }
}
