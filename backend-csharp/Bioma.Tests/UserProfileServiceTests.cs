using Bioma.Application.DTOs;
using Bioma.Infrastructure.Data;
using Bioma.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bioma.Tests;

public class UserProfileServiceTests
{
    private static (UserProfileService UserService, BiomaGameService GameService) CreateServices(string dbName)
    {
        var options = new DbContextOptionsBuilder<BiomaDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var db = new BiomaDbContext(options);
        BiomaDbSeeder.SeedAsync(db).GetAwaiter().GetResult();

        var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        return (new UserProfileService(db), new BiomaGameService(db, httpClient));
    }

    [Fact]
    public async Task US02_RegisterAsync_CreatesEmptyUniqueProfile_AndRejectsDuplicates()
    {
        var (service, _) = CreateServices(nameof(US02_RegisterAsync_CreatesEmptyUniqueProfile_AndRejectsDuplicates));

        var registerReq = new RegisterUserRequestDto("AigleRoyal99", "aigle@bioma.fr", "SecretPass123!");
        var response = await service.RegisterAsync(registerReq);

        Assert.True(response.Success);
        Assert.NotNull(response.AuthToken);
        Assert.NotNull(response.Profile);
        Assert.Equal("AigleRoyal99", response.Profile!.Username);
        Assert.Empty(response.Profile.Inventory);
        Assert.Equal(0, response.Profile.Stats.TotalCardsOwned);
        Assert.Equal(0, response.Profile.Stats.TotalVictories);

        // Un doublon de pseudo doit être refusé pour garantir l'identité unique (US-02)
        var duplicate = await service.RegisterAsync(new RegisterUserRequestDto("AigleRoyal99", "autre@bioma.fr", "SecretPass123!"));
        Assert.False(duplicate.Success);

        // Un doublon d'email doit être refusé
        var duplicateEmail = await service.RegisterAsync(new RegisterUserRequestDto("AutrePseudo", "aigle@bioma.fr", "SecretPass123!"));
        Assert.False(duplicateEmail.Success);
    }

    [Fact]
    public async Task US02_RegisterAsync_RejectsInvalidInputs_AndEnforcesSecurityRules()
    {
        var (service, _) = CreateServices(nameof(US02_RegisterAsync_RejectsInvalidInputs_AndEnforcesSecurityRules));

        // Mot de passe trop court (< 8 car)
        var shortPwd = await service.RegisterAsync(new RegisterUserRequestDto("SecUser1", "sec1@bioma.fr", "Pass1!"));
        Assert.False(shortPwd.Success);
        Assert.Contains("8 caractères", shortPwd.Message);

        // Mot de passe sans majuscule
        var noUpper = await service.RegisterAsync(new RegisterUserRequestDto("SecUser2", "sec2@bioma.fr", "secret123!"));
        Assert.False(noUpper.Success);

        // Mot de passe sans chiffre ni symbole
        var noDigit = await service.RegisterAsync(new RegisterUserRequestDto("SecUser3", "sec3@bioma.fr", "SecretPassword"));
        Assert.False(noDigit.Success);

        // Pseudo avec caractères interdits (espaces ou balises)
        var badUser = await service.RegisterAsync(new RegisterUserRequestDto("Bad User<script>", "sec4@bioma.fr", "SecretPass123!"));
        Assert.False(badUser.Success);

        // Pseudo réservé
        var reserved = await service.RegisterAsync(new RegisterUserRequestDto("admin", "admin@bioma.fr", "SecretPass123!"));
        Assert.False(reserved.Success);

        // Email invalide
        var badEmail = await service.RegisterAsync(new RegisterUserRequestDto("ValidUser", "not-an-email", "SecretPass123!"));
        Assert.False(badEmail.Success);
    }

    [Fact]
    public async Task US01_LoginAsync_ValidatesSecurePasswordHash_AndRejectsWrongPassword()
    {
        var (service, _) = CreateServices(nameof(US01_LoginAsync_ValidatesSecurePasswordHash_AndRejectsWrongPassword));

        await service.RegisterAsync(new RegisterUserRequestDto("JoueurTest", "joueur@bioma.fr", "MonMotDePasse123!"));

        // Connexion par pseudo
        var validLogin = await service.LoginAsync(new LoginRequestDto("JoueurTest", "MonMotDePasse123!"));
        Assert.True(validLogin.Success);
        Assert.NotNull(validLogin.AuthToken);
        Assert.NotNull(validLogin.Profile);

        // Connexion par email (insensible à la casse)
        var emailLogin = await service.LoginAsync(new LoginRequestDto("JOUEUR@BIOMA.FR", "MonMotDePasse123!"));
        Assert.True(emailLogin.Success);

        // Mauvais mot de passe rejeté
        var wrongLogin = await service.LoginAsync(new LoginRequestDto("JoueurTest", "MauvaisMotDePasse123!"));
        Assert.False(wrongLogin.Success);
        Assert.Null(wrongLogin.AuthToken);

        // Joueur inexistant rejeté proprement
        var unknownLogin = await service.LoginAsync(new LoginRequestDto("Inconnu123", "MonMotDePasse123!"));
        Assert.False(unknownLogin.Success);
    }

    [Fact]
    public async Task US03_US04_US05_US06_ProfileDeckAvatarCloudSyncAndGlobalStats_WorkEndToEnd()
    {
        var (userService, gameService) = CreateServices(nameof(US03_US04_US05_US06_ProfileDeckAvatarCloudSyncAndGlobalStats_WorkEndToEnd));

        var reg = await userService.RegisterAsync(new RegisterUserRequestDto("ExplorateurReel", "reel@bioma.fr", "BiomaPass123!"));
        Assert.True(reg.Success);
        var playerId = reg.Profile!.Id;

        // Capture/enregistrement d'une vraie observation pour ce joueur
        var obs = new ExternalObservationDto(
            "gbif-101",
            "GBIF",
            "Lynx lynx",
            "Lynx boréal",
            "Felidae",
            "Animalia",
            46.5,
            6.2,
            "Jura, France",
            "2026-11-09",
            "https://images.unsplash.com/photo-1546182990-dffeafbe841d?auto=format&fit=crop&w=600&q=80"
        );
        await gameService.SaveObservationToBackOfficeAsync(obs, playerId);

        // US-03 & US-06 : Consultation de l'inventaire et des statistiques globales
        var profile = await userService.GetProfileWithInventoryAsync(playerId);
        Assert.NotNull(profile);
        Assert.Single(profile!.Inventory);
        Assert.Empty(profile.ActiveDeck);
        Assert.Equal(1, profile.Stats.TotalCardsOwned);

        // US-03 : Ajouter la carte au deck actif
        var cardId = profile.Inventory[0].Id;
        var afterToggle = await userService.ToggleCardInActiveDeckAsync(playerId, cardId);
        Assert.NotNull(afterToggle);
        Assert.Single(afterToggle!.ActiveDeck);

        // US-05 : Modifier l'avatar du profil
        const string newAvatar = "https://example.com/custom-avatar.png";
        var afterAvatar = await userService.UpdateAvatarAsync(playerId, newAvatar);
        Assert.NotNull(afterAvatar);
        Assert.Equal(newAvatar, afterAvatar!.AvatarUrl);

        // US-04 : Synchronisation Cloud et Restauration multi-appareil
        var cloudSync = await userService.SyncProgressionToCloudAsync(playerId);
        Assert.NotNull(cloudSync);
        Assert.StartsWith("CLOUD-", cloudSync!.CloudBackupToken);
        Assert.Equal(1, cloudSync.SavedCardsCount);
        Assert.False(string.IsNullOrWhiteSpace(cloudSync.CloudSnapshotJson));

        var restored = await userService.RestoreProgressionFromCloudAsync(cloudSync.CloudSnapshotJson);
        Assert.True(restored.Success);
        Assert.NotNull(restored.Profile);
        Assert.Single(restored.Profile!.Inventory);
        Assert.Single(restored.Profile.ActiveDeck);
    }
}
