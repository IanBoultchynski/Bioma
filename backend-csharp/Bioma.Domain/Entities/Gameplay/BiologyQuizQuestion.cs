namespace Bioma.Domain.Entities;

/// <summary>
/// Épique 4 : Question de Quiz Chronométré (US-22 à US-26).
/// </summary>
public class BiologyQuizQuestion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string QuestionText { get; set; } = string.Empty;
    public string ChoiceA { get; set; } = string.Empty;
    public string ChoiceB { get; set; } = string.Empty;
    public string ChoiceC { get; set; } = string.Empty;
    public string ChoiceD { get; set; } = string.Empty;
    public int CorrectChoiceIndex { get; set; } // 0..3
    public string ScientificExplanation { get; set; } = string.Empty;
    public int TimeLimitSeconds { get; set; } = 15;
    public int RewardCurrency { get; set; } = 50;
    public int RewardXp { get; set; } = 25;
}
