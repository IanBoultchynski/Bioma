using Bioma.Application.DTOs;
using Bioma.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Bioma.Api.Controllers;

/// <summary>
/// Épique 5 : Tirage / Gacha (US-27 à US-31).
/// NB : tant que la connexion (US-01) n'existe pas, l'id du joueur passe dans l'URL.
/// Une fois l'authentification en place, il faudra le lire depuis le token (User.Claims).
/// </summary>
[ApiController]
[Route("api/gacha")]
public class GachaController : ControllerBase
{
    private readonly IGachaService _gacha;

    public GachaController(IGachaService gacha)
    {
        _gacha = gacha;
    }

    // GET /api/gacha/{playerId}  -> prix, solde, probabilités
    [HttpGet("{playerId:guid}")]
    public async Task<ActionResult<GachaBannerDto>> GetBanner(Guid playerId, CancellationToken ct)
    {
        return Ok(await _gacha.GetBannerAsync(playerId, ct));
    }

    // POST /api/gacha/{playerId}/pull?count=1   ou   ?count=10
    [HttpPost("{playerId:guid}/pull")]
    public async Task<ActionResult<GachaPullResultDto>> Pull(Guid playerId, [FromQuery] int count = 1, CancellationToken ct = default)
    {
        var result = await _gacha.PullAsync(playerId, count, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}