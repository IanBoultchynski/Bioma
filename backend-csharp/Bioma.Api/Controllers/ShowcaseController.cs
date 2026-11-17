using Bioma.Application.DTOs;
using Bioma.Application.Interfaces;
using Bioma.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Bioma.Api.Controllers;

/// <summary>
/// Contrôleur REST pour l'Épique 11 : Vitrine 3D / Terrarium Virtuel (US-61 à US-63).
/// </summary>
[ApiController]
[Route("api/showcase")]
public class ShowcaseController : ControllerBase
{
    private readonly IShowcase3DService _showcaseService;

    public ShowcaseController(IShowcase3DService showcaseService)
    {
        _showcaseService = showcaseService;
    }

    [HttpGet("{playerId:guid}/biomes")]
    public async Task<ActionResult<PlayerBiomesOverviewDto>> GetBiomesOverview(
        Guid playerId,
        CancellationToken ct = default)
    {
        var overview = await _showcaseService.GetPlayerBiomesOverviewAsync(playerId, ct);
        return Ok(overview);
    }

    [HttpPost("{playerId:guid}/biomes/{biome}")]
    public async Task<ActionResult<ShowcaseActionResultDto>> CreateBiome(
        Guid playerId,
        BiomeType biome,
        CancellationToken ct = default)
    {
        var res = await _showcaseService.CreateBiomeAsync(playerId, biome, ct);
        return res.Success ? Ok(res) : BadRequest(res);
    }

    [HttpDelete("{playerId:guid}/biomes/{biome}")]
    public async Task<ActionResult<ShowcaseActionResultDto>> DeleteBiome(
        Guid playerId,
        BiomeType biome,
        CancellationToken ct = default)
    {
        var res = await _showcaseService.DeleteBiomeAsync(playerId, biome, ct);
        return Ok(res);
    }

    [HttpGet("{playerId:guid}")]
    public async Task<ActionResult<ShowcaseViewDto>> GetShowcase(
        Guid playerId,
        [FromQuery] BiomeType? biome = null,
        CancellationToken ct = default)
    {
        var view = await _showcaseService.GetPlayerShowcaseAsync(playerId, biome, ct);
        return view is not null ? Ok(view) : NotFound();
    }

    public record PlaceCardRequest(BiomeType Biome, int LocalSlotIndex, Guid PlayerCardId);
    public record RemoveCardRequest(BiomeType Biome, int LocalSlotIndex);
    public record MovePedestalRequest(BiomeType Biome, int LocalSlotIndex, double PosX, double PosY, double PosZ);

    [HttpPost("{playerId:guid}/place")]
    public async Task<ActionResult<ShowcaseActionResultDto>> PlaceCard(
        Guid playerId,
        [FromBody] PlaceCardRequest request,
        CancellationToken ct = default)
    {
        var res = await _showcaseService.PlaceCardOnPedestalAsync(
            playerId,
            request.Biome,
            request.LocalSlotIndex,
            request.PlayerCardId,
            ct);
        return res.Success ? Ok(res) : BadRequest(res);
    }

    [HttpPost("{playerId:guid}/remove")]
    public async Task<ActionResult<ShowcaseActionResultDto>> RemoveCard(
        Guid playerId,
        [FromBody] RemoveCardRequest request,
        CancellationToken ct = default)
    {
        var res = await _showcaseService.RemoveCardFromPedestalAsync(
            playerId,
            request.Biome,
            request.LocalSlotIndex,
            ct);
        return Ok(res);
    }

    [HttpPut("{playerId:guid}/position")]
    public async Task<ActionResult<ShowcaseActionResultDto>> UpdatePedestalPosition(
        Guid playerId,
        [FromBody] MovePedestalRequest request,
        CancellationToken ct = default)
    {
        var res = await _showcaseService.UpdatePedestalPositionAsync(
            playerId,
            request.Biome,
            request.LocalSlotIndex,
            request.PosX,
            request.PosY,
            request.PosZ,
            ct);
        return Ok(res);
    }
}
