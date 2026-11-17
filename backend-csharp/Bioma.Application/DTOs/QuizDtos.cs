using Bioma.Domain.Enums;

namespace Bioma.Application.DTOs;

/// <summary>
/// État du quota quotidien et statistiques du Quiz Chronométré d'un joueur (US-22 à US-26).
/// </summary>
public record QuizDailyStatusDto(
    bool CanPlayToday,
    int QuestionsAnsweredToday,
    int DailyMaxQuestions,
    int TotalCorrectToday,
    int TotalCurrencyEarnedToday,
    int TotalXpEarnedToday,
    string NextResetCountdownText,
    List<QuizHistoryItemDto> RecentAttempts
);

/// <summary>
/// Question aléatoire générée dynamiquement à partir des cartes du catalogue Bioma (US-22, US-23).
/// </summary>
public record QuizQuestionViewDto(
    Guid SessionToken,
    Guid QuestionId,
    Guid SpeciesCardId,
    string SpeciesImageUrl,
    CardRarity SpeciesRarity,
    string SpeciesRarityLabel,
    string QuestionCategory,
    string QuestionText,
    List<string> Choices,
    int TimeLimitSeconds,
    int RewardCurrency,
    int RewardXp,
    int CurrentQuestionNumber,
    int DailyMaxQuestions
);

/// <summary>
/// Requête de validation d'une réponse au Quiz Chronométré (US-23, US-24).
/// </summary>
public record SubmitQuizAnswerDto(
    Guid PlayerId,
    Guid SessionToken,
    Guid QuestionId,
    int SelectedChoiceIndex // 0..3, ou -1 si le chronomètre a expiré
);

/// <summary>
/// Résultat instantané d'une réponse au Quiz Chronométré avec récompense ou échec par dépassement de temps (US-24, US-25, US-26).
/// </summary>
public record QuizAnswerResultDto(
    bool IsCorrect,
    bool IsTimeOut,
    int SelectedChoiceIndex,
    int CorrectChoiceIndex,
    double ElapsedSeconds,
    int TimeLimitSeconds,
    int EarnedCurrency,
    int EarnedXp,
    int NewPlayerCurrency,
    int NewPlayerXp,
    int NewPlayerLevel,
    string StatusTitle,
    string ScientificExplanation,
    CardDto? CatalogueCard
);

/// <summary>
/// Élément de l'historique des tentatives de quiz en base Supabase.
/// </summary>
public record QuizHistoryItemDto(
    Guid AttemptId,
    string QuestionText,
    bool IsCorrect,
    bool IsTimeOut,
    double ElapsedSeconds,
    int EarnedCurrency,
    int EarnedXp,
    DateTimeOffset AnsweredAt
);
