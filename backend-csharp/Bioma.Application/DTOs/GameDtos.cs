using Bioma.Domain.Entities;

namespace Bioma.Application.DTOs;

public record PlayerProfileDto(
    Guid Id,
    string Pseudo,
    string AvatarInitials,
    int Level,
    int Experience,
    int FeuillesCurrency,
    int EloRating,
    int Victories,
    int Defeats,
    int DistinctSpeciesCount,
    int TotalSpecimensCount
);

public record CardDto(
    Guid Id,
    int SpecimenNumber,
    string Name,
    string ScientificSubtitle,
    string FamilyName,
    string HabitatName,
    string ImageUrl,
    string RarityLabel,
    CardRarity Rarity,
    string StageLabel,
    bool IsAdult,
    bool IsHybrid,
    int CurrentXp,
    int MaxXpForEvolution,
    int Hp,
    int Attack,
    int Defense,
    int Speed,
    double WeightKg,
    double DangerIndex,
    bool IsLockedForAuction,
    bool IsInExpedition,
    Guid? DisplayedInBiomeId
);

public record PackOpeningResultDto(
    int RemainingCurrency,
    int PacksOpened,
    List<CardDto> DrawnCards
);

public record CaptureRequestDto(
    Guid PlayerId,
    string ImageBase64OrUrl,
    double? Latitude,
    double? Longitude,
    string? FilenameHint
);

public record CaptureCandidateDto(
    string ScientificName,
    string CommonName,
    string Family,
    double Confidence
);

public record CaptureResponseDto(
    string Status, // RECOGNIZED, UNCERTAIN, REJECTED
    double Confidence,
    string Message,
    bool WasDuplicate,
    int BonusFeuillesAwarded,
    int BonusXpAwarded,
    CardDto? UnlockedCard,
    List<CaptureCandidateDto> Candidates
);

public record FusionLabRequestDto(
    Guid PlayerId,
    Guid ParentACardId,
    Guid ParentBCardId,
    bool UseCatalystBoost
);

public record FusionLabResponseDto(
    bool Success,
    string Message,
    int RemainingCurrency,
    string GeneratedPrompt,
    CardDto? HybridCard
);

public record StartQuizResponseDto(
    Guid AttemptId,
    Guid QuestionId,
    string Theme,
    string ScientificSubtitle,
    string ImageUrl,
    string QuestionText,
    List<string> Options,
    int TimeLimitSeconds,
    DateTimeOffset ServerStartedAt
);

public record SubmitQuizAnswerRequestDto(
    Guid AttemptId,
    Guid PlayerId,
    Guid QuestionId,
    int SelectedOptionIndex
);

public record SubmitQuizAnswerResponseDto(
    bool IsSuccess,
    bool IsTimeout,
    double ElapsedSeconds,
    string Message,
    int XpEarned,
    int FeuillesEarned,
    int CorrectOptionIndex
);

public record PlaceBidRequestDto(
    Guid AuctionListingId,
    Guid BidderId,
    string BidderPseudo,
    int BidAmount
);

public record CreateAuctionRequestDto(
    Guid SellerId,
    Guid CardId,
    int StartingPrice,
    int DurationMinutes
);

public record AuctionListingDto(
    Guid Id,
    int LotNumber,
    Guid CardId,
    string CardName,
    string ScientificSubtitle,
    string FamilyAndRarity,
    string RarityBadge,
    string ImageUrl,
    bool IsHybrid,
    Guid SellerId,
    string SellerPseudo,
    Guid? HighestBidderId,
    string? HighestBidderPseudo,
    int CurrentBid,
    int BidCount,
    DateTimeOffset EndsAt,
    string TimeRemainingText
);

public record LaunchExpeditionRequestDto(
    Guid PlayerId,
    Guid CompanionCardId,
    string TargetSpeciesName,
    string RegionName,
    int DurationHours
);

public record CombatRequestDto(
    Guid AttackerId,
    string ArenaBiome
);

public record CombatResultDto(
    Guid CombatId,
    string AttackerPseudo,
    string DefenderPseudo,
    string ArenaBiome,
    double AttackerScore,
    double DefenderScore,
    bool AttackerWon,
    int EloDelta,
    int NewEloRating,
    string FormulaBreakdown
);
