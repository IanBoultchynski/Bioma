using Bioma.Application.DTOs;
using Bioma.Domain.Entities;

namespace Bioma.Application.Interfaces;

public interface IPythonAiClient
{
    Task<CaptureResponseDto> IdentifyCaptureAsync(CaptureRequestDto request, CancellationToken ct = default);
    Task<(string HybridName, string HybridScientific, string Prompt, string ImageUrl, int Hp, int Attack, int Defense, int Speed)> FuseCardsAsync(
        Card parentA, Card parentB, bool useCatalyst, CancellationToken ct = default);
    Task<List<GeoHotspotZone>> GetExpeditionHotspotsAsync(string scientificName, int? month = null, CancellationToken ct = default);
}

public interface IBiomaGameService
{
    Task<PlayerProfileDto> GetPlayerProfileAsync(Guid? playerId = null);
    Task<List<CardDto>> GetPlayerCollectionAsync(Guid? playerId = null);
    Task<PackOpeningResultDto> OpenPackAsync(Guid playerId, int packCount = 1);
    Task<CardDto> ApplyDailyCareAsync(Guid cardId);
    Task<CardDto> EvolveCardAsync(Guid cardId);

    // Biomes / Vitrine 3D
    Task<List<BiomeShowcase>> GetPlayerBiomesAsync(Guid? playerId = null);
    Task<BiomeShowcase> ToggleCardInBiomeAsync(Guid biomeId, Guid cardId);

    // Capture IRL & Laboratoire
    Task<CaptureResponseDto> ProcessIrlCaptureAsync(CaptureRequestDto request);
    Task<FusionLabResponseDto> ExecuteLaboratoryFusionAsync(FusionLabRequestDto request);

    // Quiz Chronométré (Serveur strict 10s)
    Task<StartQuizResponseDto> StartTimedQuizAsync(Guid playerId, int questionIndex = 0);
    Task<SubmitQuizAnswerResponseDto> SubmitQuizAnswerAsync(SubmitQuizAnswerRequestDto request);

    // Expéditions Data & Globe 3D
    Task<List<GeoHotspotZone>> GetHotspotsForSpeciesAsync(string scientificName, int? month = null);
    Task<List<Expedition>> GetPlayerExpeditionsAsync(Guid? playerId = null);
    Task<Expedition> LaunchExpeditionAsync(LaunchExpeditionRequestDto request);
    Task<bool> CancelExpeditionAsync(Guid expeditionId);

    // Marché aux Enchères (SignalR)
    Task<List<AuctionListingDto>> GetActiveAuctionsAsync();
    Task<AuctionListingDto> CreateAuctionAsync(CreateAuctionRequestDto request);
    Task<(AuctionListingDto Listing, Guid? RefundedBidderId, int RefundedAmount)> PlaceBidAsync(PlaceBidRequestDto request);

    // Batailles 1v1
    Task<CombatResultDto> ResolveCombat1v1Async(CombatRequestDto request);
    Task<List<Combat1v1>> GetCombatHistoryAsync(Guid? playerId = null);
}
