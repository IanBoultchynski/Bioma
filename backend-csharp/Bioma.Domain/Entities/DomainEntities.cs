namespace Bioma.Domain.Entities;

/// <summary>
/// Entité simplifiée du Back-Office C# représentant une espèce/observation
/// importée depuis GBIF ou iNaturalist et enregistrée dans la collection.
/// </summary>
public class SpeciesCard
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CommonName { get; set; } = string.Empty;
    public string ScientificName { get; set; } = string.Empty;
    public string Family { get; set; } = "Animalia";
    public string Kingdom { get; set; } = "Animalia";
    public string SourceApi { get; set; } = "GBIF"; // "GBIF" ou "iNaturalist"
    public string ExternalId { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string CountryOrPlace { get; set; } = string.Empty;
    public string ObservedDate { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
