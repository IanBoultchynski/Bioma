using Bioma.Domain.Entities;
using Bioma.Domain.Enums;

namespace Bioma.Application.Interfaces;

// ============================================================================
// CONTRATS D'APPLICATION PRÉPARATOIRES POUR LES 13 ÉPIQUES (US-01 à US-66)
// (À implémenter au fur et à mesure de l'avancement des User Stories)
// ============================================================================

/// <summary>
/// Épique 1 : Utilisateurs & Profil (US-01 à US-06) + Paramètres (US-66).
/// </summary>
public interface IUserProfileService
{
    // US-01 : Connexion sécurisée
    // US-02 : Création de profil unique
    // US-03 : Consultation du deck et de l'inventaire
    // US-04 : Synchronisation cloud de la progression
    // US-05 : Modification de l'avatar
    // US-06 : Consultation des statistiques globales (cartes, victoires)
    // US-66 : Activation / désactivation des notifications push
}

/// <summary>
/// Épique 2 : Capture IRL & Reconnaissance d'image via animal_id.py (US-07 à US-15).
/// </summary>
public interface ICaptureIrlService
{
    // US-07 : Ouverture du module Instantané (caméra)
    // US-08 : Enregistrement GPS et association au biome réel
    // US-09 : Identification automatique via CLIP (animal_id.py) et déblocage de carte
    // US-10 : Alerte si non reconnu (flou / luminosité insuffisante)
    // US-11 : Invitation à reprendre sous un meilleur angle en cas d'hésitation (seuil CLIP)
    // US-12 : Récupération des vraies statistiques scientifiques (poids, habitat)
    // US-13 : Retour pour l'animation d'impression style Polaroïd
    // US-14 : Attribution de monnaie virtuelle / XP si doublon photographié
    // US-15 : File d'attente hors-ligne et synchronisation différée
}

/// <summary>
/// Épique 3 : Expéditions Data & Globe 3D (US-16 à US-21, US-65).
/// </summary>
public interface IExpeditionService
{
    // US-16 : Données pour le globe 3D interactif
    // US-17 : Zones lumineuses (hotspots) de concentration d'espèces
    // US-18 : Envoi de cartes animales en expédition
    // US-19 : Vérification des saisons réelles d'apparition
    // US-20 : Résolution d'expédition, notification succès/échec et récompenses
    // US-21 : Annulation d'une expédition en cours pour récupérer l'animal
    // US-65 : Cohérence environnementale des visuels d'expédition avec le biome
}

/// <summary>
/// Épique 4 : Quiz Chronométré (US-22 à US-26).
/// </summary>
public interface IQuizService
{
    // US-22 : Génération d'événements de quiz sur la biologie
    // US-23 : Chronométrage strict et équitable côté serveur
    // US-24 : Validation instantanée de la réponse
    // US-25 : Récompense en monnaie virtuelle ou XP si bonne réponse
    // US-26 : Gestion du dépassement de temps (échec sans récompense)
}

/// <summary>
/// Épique 5 : Tirage / Gacha (US-27 à US-31).
/// </summary>
public interface IGachaService
{
    // US-27 : Dépense de monnaie virtuelle pour un tirage aléatoire
    // US-28 : Pondération des probabilités selon la rareté réelle dans la nature
    // US-29 : Données pour l'animation de loterie
    // US-30 : Indication claire du niveau de rareté (Commune, Rare, Légendaire)
    // US-31 : Multi-tirage (ex: x10)
}

/// <summary>
/// Épique 6 : Progression & Évolution (US-32 à US-36).
/// </summary>
public interface IProgressionService
{
    // US-32 : Gain d'XP des animaux via quiz et expéditions
    // US-33 : Évolution du stade Juvénile vers Adulte au seuil d'XP
    // US-34 : Amélioration des statistiques de combat après évolution
    // US-35 : Déblocage de la finition visuelle distincte du cadre Polaroïd
    // US-36 : Soins virtuels quotidiens (+bonus d'XP)
}

/// <summary>
/// Épique 7 : Le Laboratoire & Fusion Hybride (US-37 à US-46).
/// </summary>
public interface ILaboratoryService
{
    // US-37 : Accès au Laboratoire de croisement d'espèces
    // US-38 : Placement de deux cartes "Adulte" distinctes
    // US-39 : Interdiction de fusionner deux fois la même carte
    // US-40 : Dépense d'énergie ou de monnaie pour valider la fusion
    // US-41 : Application d'un objet "Boost" catalysant (+10% stats)
    // US-42 : Calcul d'héritage équilibré des statistiques des deux parents
    // US-43 : Génération d'image unique par IA pour l'hybride
    // US-44 : Remboursement automatique et message d'excuse en cas de surcharge
    // US-45 : Ajout définitif de la carte hybride à l'inventaire
    // US-46 : Consommation ou mise au repos des deux cartes parentes
}

/// <summary>
/// Épique 8 : Marché aux Enchères en Temps Réel (US-47 à US-51).
/// </summary>
public interface IAuctionMarketService
{
    // US-47 : Consultation de l'hôtel des ventes
    // US-48 : Mise aux enchères des doublons ou hybrides
    // US-49 : Flux temps réel (prix et temps restant)
    // US-50 : Placement d'une surenchère
    // US-51 : Remboursement immédiat du précédent enchérisseur
}

/// <summary>
/// Épique 9 : Social & Échanges (US-52 à US-54).
/// </summary>
public interface ISocialTradeService
{
    // US-52 : Gestion de la liste d'amis
    // US-53 : Proposition et validation d'échange sécurisé de cartes
    // US-54 : Envoi direct d'un cadeau (carte ou objet) à un ami
}

/// <summary>
/// Épique 10 : Batailles 1v1 (US-55 à US-60).
/// </summary>
public interface IBattleService
{
    // US-55 : Matchmaking et recherche d'adversaire en ligne
    // US-56 : Sélection de l'équipe active du deck pour le combat
    // US-57 : Résolution tactique basée sur poids, dangerosité et biome réel
    // US-58 : Mise à jour du classement Elo (+ victoire / - défaite)
    // US-59 : Données du déroulé pour l'animation de victoire ou défaite
    // US-60 : Historique des précédents matchs
}

/// <summary>
/// Épique 11 : Vitrine 3D / Terrarium Virtuel (US-61 à US-63).
/// </summary>
public interface IShowcase3DService
{
    // US-61 : Accès à son terrarium / musée virtuel 3D
    // US-62 : Positionnement et exposition des cartes sur des socles 3D
    // US-63 : Visite de la Vitrine 3D d'autres joueurs
}
