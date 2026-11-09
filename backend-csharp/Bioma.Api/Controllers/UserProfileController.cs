using Bioma.Application.DTOs;
using Bioma.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Bioma.Api.Controllers;

/// <summary>
/// Contrôleur REST de l'Épique 1 : Utilisateurs &amp; Profil (US-01 à US-06).
/// </summary>
[ApiController]
[Route("api/users")]
public class UserProfileController : ControllerBase
{
    private readonly IUserProfileService _userService;

    public UserProfileController(IUserProfileService userService)
    {
        _userService = userService;
    }

    /// <summary>
    /// US-02 : Créer un profil utilisateur afin d'avoir une identité unique dans le jeu.
    /// </summary>
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponseDto>> Register(
        [FromBody] RegisterUserRequestDto request,
        CancellationToken ct = default)
    {
        var result = await _userService.RegisterAsync(request, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// US-01 : Se connecter de manière sécurisée afin de protéger son compte et ses cartes.
    /// </summary>
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login(
        [FromBody] LoginRequestDto request,
        CancellationToken ct = default)
    {
        var result = await _userService.LoginAsync(request, ct);
        return result.Success ? Ok(result) : Unauthorized(result);
    }

    /// <summary>
    /// Liste des profils utilisateurs existants.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<UserProfileSummaryDto>>> GetAll(CancellationToken ct = default)
    {
        var list = await _userService.GetAllProfilesAsync(ct);
        return Ok(list);
    }

    /// <summary>
    /// US-03 &amp; US-06 : Consulter son profil complet, son deck actif, son inventaire et ses statistiques globales.
    /// </summary>
    [HttpGet("{playerId:guid}")]
    public async Task<ActionResult<UserProfileDto>> GetProfile(Guid playerId, CancellationToken ct = default)
    {
        var profile = await _userService.GetProfileWithInventoryAsync(playerId, ct);
        return profile is null ? NotFound() : Ok(profile);
    }

    /// <summary>
    /// US-06 : Consulter ses statistiques globales (nombre de cartes, victoires) afin de suivre sa progression globale.
    /// </summary>
    [HttpGet("{playerId:guid}/stats")]
    public async Task<ActionResult<PlayerGlobalStatsDto>> GetGlobalStats(Guid playerId, CancellationToken ct = default)
    {
        var stats = await _userService.GetGlobalStatsAsync(playerId, ct);
        return stats is null ? NotFound() : Ok(stats);
    }

    /// <summary>
    /// US-05 : Modifier son avatar de profil afin de personnaliser son compte.
    /// </summary>
    [HttpPut("{playerId:guid}/avatar")]
    public async Task<ActionResult<UserProfileDto>> UpdateAvatar(
        Guid playerId,
        [FromBody] UpdateAvatarRequestDto request,
        CancellationToken ct = default)
    {
        var updated = await _userService.UpdateAvatarAsync(playerId, request.AvatarUrl, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    /// <summary>
    /// US-03 : Basculer une carte de l'inventaire dans ou hors du deck actif.
    /// </summary>
    [HttpPut("{playerId:guid}/deck/{playerCardId:guid}/toggle")]
    public async Task<ActionResult<UserProfileDto>> ToggleDeckCard(
        Guid playerId,
        Guid playerCardId,
        CancellationToken ct = default)
    {
        var updated = await _userService.ToggleCardInActiveDeckAsync(playerId, playerCardId, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    /// <summary>
    /// US-04 : Sauvegarder et synchroniser automatiquement sa progression sur le cloud.
    /// </summary>
    [HttpPost("{playerId:guid}/cloud-sync")]
    public async Task<ActionResult<CloudSyncResultDto>> SyncCloud(Guid playerId, CancellationToken ct = default)
    {
        var synced = await _userService.SyncProgressionToCloudAsync(playerId, ct);
        return synced is null ? NotFound() : Ok(synced);
    }

    /// <summary>
    /// US-04 : Restaurer son compte et sa progression depuis le Cloud (changement de téléphone / appareil).
    /// </summary>
    [HttpPost("cloud-restore")]
    public async Task<ActionResult<AuthResponseDto>> RestoreFromCloud(
        [FromBody] RestoreCloudRequestDto request,
        CancellationToken ct = default)
    {
        var restored = await _userService.RestoreProgressionFromCloudAsync(request.CloudTokenOrJson, ct);
        return restored.Success ? Ok(restored) : BadRequest(restored);
    }
}
