using Bioma.Application.DTOs;

namespace Bioma.Application.Interfaces;

/// <summary>
/// Interface du début de Back-Office C# :
/// - Connexions simples aux APIs GBIF et iNaturalist
/// - Gestion basique (CRUD) des cartes sauvegardées en base de données
/// </summary>
public interface IBiomaGameService
{
    Task<ApiSearchResultDto> SearchApisAsync(string query, int limitPerSource = 4, CancellationToken ct = default);
    Task<List<ExternalObservationDto>> FetchFromGbifAsync(string query, int limit = 4, CancellationToken ct = default);
    Task<List<ExternalObservationDto>> FetchFromInaturalistAsync(string query, int limit = 4, CancellationToken ct = default);

    Task<List<CardDto>> GetSavedCardsAsync();
    Task<CardDto> SaveObservationToBackOfficeAsync(ExternalObservationDto observation);
    Task<bool> DeleteCardAsync(Guid cardId);
}
