namespace Bioma.Domain.Entities;

/// <summary>
/// Historique de réponse d'un joueur à un Quiz Chronométré (US-23 à US-26).
/// </summary>
public class QuizAttemptRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlayerId { get; set; }
    public PlayerProfile? Player { get; set; }

    public Guid QuestionId { get; set; }
    public BiologyQuizQuestion? Question { get; set; }

    public int SelectedChoiceIndex { get; set; }
    public double ElapsedSeconds { get; set; }
    public bool IsTimeOut { get; set; }
    public bool IsCorrect { get; set; }
    public int EarnedCurrency { get; set; }
    public int EarnedXp { get; set; }
    public DateTimeOffset AnsweredAt { get; set; } = DateTimeOffset.UtcNow;
}
