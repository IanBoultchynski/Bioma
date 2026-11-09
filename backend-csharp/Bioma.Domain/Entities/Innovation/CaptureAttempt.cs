using Bioma.Domain.Enums;

namespace Bioma.Domain.Entities;

/// <summary>
/// Épique 2 : Capture IRL & Reconnaissance CLIP (US-07 à US-15).
/// </summary>
public class CaptureAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlayerId { get; set; }
    public PlayerProfile? Player { get; set; }

    public string ImagePathOrBase64 { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public BiomeType DetectedBiome { get; set; } = BiomeType.ForetTemperee;
    public string? PredictedSpecies { get; set; }
    public double ConfidenceScore { get; set; }
    public bool IsRecognized { get; set; }
    public bool RequiresRetakeAngle { get; set; }
    public bool WasSyncedFromOfflineQueue { get; set; }
    public Guid? UnlockedPlayerCardId { get; set; }
    public DateTimeOffset CapturedAt { get; set; } = DateTimeOffset.UtcNow;
}
