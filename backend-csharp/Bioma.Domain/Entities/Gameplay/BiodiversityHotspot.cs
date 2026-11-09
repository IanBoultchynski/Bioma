using Bioma.Domain.Enums;

namespace Bioma.Domain.Entities;

/// <summary>
/// Épique 3 : Expéditions Data & Globe 3D (US-16 à US-21, US-65).
/// </summary>
public class BiodiversityHotspot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string CountryOrRegion { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public BiomeType Biome { get; set; }
    public Season ActiveSeason { get; set; }
    public int SpeciesDensityScore { get; set; } = 75;
}
