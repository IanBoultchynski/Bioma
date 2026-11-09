namespace Bioma.Domain.Enums;

/// <summary>
/// Niveau de rareté d'une carte (US-28, US-30).
/// </summary>
public enum CardRarity
{
    Commune = 0,
    PeuCommune = 1,
    Rare = 2,
    Legendaire = 3
}

/// <summary>
/// Stade d'évolution d'un animal (US-33, US-38).
/// </summary>
public enum LifeStage
{
    Juvenile = 0,
    Adulte = 1
}

/// <summary>
/// État de disponibilité d'une carte dans l'inventaire du joueur (US-18, US-21, US-46, US-48).
/// </summary>
public enum CardState
{
    Disponible = 0,
    EnExpedition = 1,
    AuxEncheres = 2,
    AuRepos = 3,
    Consommee = 4
}

/// <summary>
/// Saisons réelles d'apparition pour les expéditions (US-19).
/// </summary>
public enum Season
{
    Printemps = 0,
    Ete = 1,
    Automne = 2,
    Hiver = 3
}

/// <summary>
/// Biomes réels utilisés pour la capture IRL, les expéditions et les batailles 1v1 (US-08, US-57, US-65).
/// </summary>
public enum BiomeType
{
    ForetTemperee = 0,
    ForetTropicale = 1,
    Savane = 2,
    Desert = 3,
    Toundra = 4,
    Taiga = 5,
    EauDouce = 6,
    MilieuMarin = 7,
    ZoneHumide = 8,
    Montagne = 9,
    ZoneUrbaine = 10
}

/// <summary>
/// Statut d'une mission d'expédition (US-18 à US-21).
/// </summary>
public enum ExpeditionStatus
{
    EnCours = 0,
    Succes = 1,
    Echec = 2,
    Annulee = 3
}

/// <summary>
/// Statut d'une vente aux enchères (US-47 à US-51).
/// </summary>
public enum AuctionStatus
{
    Active = 0,
    Terminee = 1,
    Annulee = 2
}

/// <summary>
/// Statut d'une proposition d'échange entre amis (US-53).
/// </summary>
public enum TradeStatus
{
    EnAttente = 0,
    Accepte = 1,
    Refuse = 2,
    Annule = 3
}

/// <summary>
/// Issue d'un duel tactique 1v1 (US-57 à US-60).
/// </summary>
public enum BattleOutcome
{
    EnCours = 0,
    Victoire = 1,
    Defaite = 2,
    Egalite = 3
}
