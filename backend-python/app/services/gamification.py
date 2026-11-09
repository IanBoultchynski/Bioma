"""
Étape 4 du Pipeline Data : Moteur de Gamification (Transformation en Données de Jeu)
------------------------------------------------------------------------------------
Transforme les données scientifiques réelles (Poids max en kg, Vitesse max en km/h,
Indice de dangerosité [1..10], Longévité en années, Occurrences GBIF/iNaturalist)
en statistiques de cartes équilibrées via normalisation logarithmique et Min-Max :

1. Points de Vie (PV / HP) :
   Basés sur l'échelle logarithmique de la masse corporelle et l'espérance de vie :
   HP = round(45 + 38 * log10(Poids_kg * 10 + 1) + 1.8 * Longevite_ans)  -> borné entre [40, 320]

2. Attaque (ATK) :
   Basée sur l'indice de dangerosité scientifique (1-10) et la vitesse de pointe (km/h) :
   ATK = round(15 + (Danger * 14.5) + (Vitesse_kmh * 0.65))              -> borné entre [20, 250]

3. Défense (DEF) :
   Basée sur la robustesse corporelle (masse) et la résilience :
   DEF = round(25 + 28 * log10(Poids_kg * 5 + 1) + (Danger * 4.0))       -> borné entre [20, 220]

4. Puissance de Combat (Formule du diagramme de séquence page 7 du Cahier des Charges) :
   Puissance = Poids_Normalise * 0.4 + Danger_Normalise * 0.6

5. Rareté & Taux de Drop (Indexés sur la rareté d'observation GBIF et la grille Figma) :
   - Commune (65%)      : > 50 000 observations
   - Peu commune (28%)  : 10 000 à 50 000 observations
   - Rare (7%)          : < 10 000 observations (ou espèces protégées)
"""

import math
from app.models.schemas import BiologicalMetricsInput, GameCardStatsDto, RarityLevel


class GamificationService:
    """Service de conversion mathématique déterministe des données biologiques en statistiques de cartes."""

    @staticmethod
    def compute_card_statistics(bio: BiologicalMetricsInput) -> GameCardStatsDto:
        # 1. Calcul des Points de Vie (PV) avec compression logarithmique de la masse (évite qu'un éléphant ait 1000x les PV d'un oiseau)
        log_weight = math.log10(max(0.01, bio.weight_kg) * 10.0 + 1.0)
        raw_hp = 45.0 + (38.0 * log_weight) + (1.8 * min(bio.lifespan_years, 60.0))
        hp = int(max(40, min(320, round(raw_hp))))

        # 2. Calcul de l'Attaque (ATK) combinant dangerosité scientifique et vitesse de pointe
        raw_attack = 15.0 + (bio.danger_index * 14.5) + (min(bio.max_speed_kmh, 140.0) * 0.65)
        attack = int(max(20, min(250, round(raw_attack))))

        # 3. Calcul de la Défense (DEF)
        raw_defense = 25.0 + (28.0 * math.log10(max(0.01, bio.weight_kg) * 5.0 + 1.0)) + (bio.danger_index * 4.0)
        defense = int(max(20, min(220, round(raw_defense))))

        # 4. Normalisation Min-Max de la vitesse sur une échelle [10..100]
        speed = int(max(10, min(100, round((min(bio.max_speed_kmh, 120.0) / 120.0) * 90.0 + 10.0))))

        # 5. Formule officielle du Diagramme de Séquence (Combat 1v1) :
        # Puissance = PoidsScore * 0.4 + DangerScore * 0.6
        weight_score = min(100.0, (math.log10(max(0.05, bio.weight_kg) * 20.0 + 1.0) / 4.5) * 100.0)
        danger_score = (bio.danger_index / 10.0) * 100.0
        combat_power = round((weight_score * 0.4) + (danger_score * 0.6), 1)

        # 6. Détermination de la rareté et du taux de drop (aligné sur le mockup Figma : 65% / 28% / 7%)
        if bio.gbif_occurrences >= 50000:
            rarity = RarityLevel.COMMON
            drop_rate = 65.0
        elif bio.gbif_occurrences >= 10000:
            rarity = RarityLevel.UNCOMMON
            drop_rate = 28.0
        else:
            rarity = RarityLevel.RARE
            drop_rate = 7.0

        return GameCardStatsDto(
            scientific_name=bio.scientific_name,
            common_name=bio.common_name,
            family=bio.family,
            habitat=bio.habitat,
            hp=hp,
            attack=attack,
            defense=defense,
            speed=speed,
            combat_power=combat_power,
            rarity=rarity,
            drop_rate_percent=drop_rate,
        )
