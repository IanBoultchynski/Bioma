using Bioma.Domain.Enums;

namespace Bioma.Domain.Entities;

/// <summary>
/// Entité du catalogue d'espèces / cartes scientifiques (importées depuis GBIF, iNaturalist ou créées au Laboratoire).
/// Contient à la fois les métadonnées d'observation et les caractéristiques biologiques/tactiques (US-12, US-30, US-57, US-64).
/// </summary>
public class SpeciesCard
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CommonName { get; set; } = string.Empty;
    public string ScientificName { get; set; } = string.Empty;
    public string Family { get; set; } = "Animalia";
    public string Kingdom { get; set; } = "Animalia";
    public string SourceApi { get; set; } = "GBIF"; // "GBIF", "iNaturalist" ou "Laboratoire"
    public string ExternalId { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string CountryOrPlace { get; set; } = string.Empty;
    public string ObservedDate { get; set; } = string.Empty;

    // Données scientifiques et de gameplay (US-12, US-28, US-30, US-57, US-64)
    public CardRarity Rarity { get; set; } = CardRarity.Commune;
    public BiomeType Biome { get; set; } = BiomeType.ForetTemperee;
    public double AverageWeightKg { get; set; } = 10.0;
    public int DangerScore { get; set; } = 20; // 1 à 100
    public string HabitatDescription { get; set; } = string.Empty;
    public string BiologicalDescription { get; set; } = string.Empty;

    // Statistiques de base pour le combat et la fusion (US-34, US-42, US-57)
    public int BaseAttack { get; set; } = 30;
    public int BaseDefense { get; set; } = 30;
    public int BaseSpeed { get; set; } = 30;
    public bool IsHybrid { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation : exemplaires possédés par les joueurs
    public ICollection<PlayerCard> PlayerCards { get; set; } = new List<PlayerCard>();
}
