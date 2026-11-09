using Bioma.Application.DTOs;
using Bioma.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Bioma.Api.Controllers;

/// <summary>
/// Contrôleur REST minimaliste pour le début du Back-Office C# :
/// - Recherche dans GBIF et iNaturalist
/// - Lecture, ajout et suppression de cartes d'espèces
/// </summary>
[ApiController]
[Route("api/biodiversity")]
public class BiomaControllers : ControllerBase
{
    private readonly IBiomaGameService _service;

    public BiomaControllers(IBiomaGameService service)
    {
        _service = service;
    }

    [HttpGet("search")]
    public async Task<ActionResult<ApiSearchResultDto>> Search(
        [FromQuery] string query = "Vulpes vulpes",
        [FromQuery] int limit = 4,
        CancellationToken ct = default)
    {
        var result = await _service.SearchApisAsync(query, limit, ct);
        return Ok(result);
    }

    [HttpGet("gbif")]
    public async Task<ActionResult<List<ExternalObservationDto>>> SearchGbif(
        [FromQuery] string query = "Vulpes vulpes",
        [FromQuery] int limit = 4,
        CancellationToken ct = default)
    {
        var items = await _service.FetchFromGbifAsync(query, limit, ct);
        return Ok(items);
    }

    [HttpGet("inaturalist")]
    public async Task<ActionResult<List<ExternalObservationDto>>> SearchInaturalist(
        [FromQuery] string query = "Vulpes vulpes",
        [FromQuery] int limit = 4,
        CancellationToken ct = default)
    {
        var items = await _service.FetchFromInaturalistAsync(query, limit, ct);
        return Ok(items);
    }

    [HttpGet("cards")]
    public async Task<ActionResult<List<CardDto>>> GetSavedCards()
    {
        var cards = await _service.GetSavedCardsAsync();
        return Ok(cards);
    }

    [HttpPost("cards")]
    public async Task<ActionResult<CardDto>> SaveCard([FromBody] ExternalObservationDto observation)
    {
        var saved = await _service.SaveObservationToBackOfficeAsync(observation);
        return Ok(saved);
    }

    [HttpDelete("cards/{id:guid}")]
    public async Task<IActionResult> DeleteCard(Guid id)
    {
        var deleted = await _service.DeleteCardAsync(id);
        return deleted ? NoContent() : NotFound();
    }
}
