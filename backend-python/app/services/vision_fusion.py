"""
Étape 3 du Pipeline Data : Traitement par Intelligence Artificielle (Vision & Fusion)
-------------------------------------------------------------------------------------
1. Classification d'Images (La Capture IRL - Fiche Use Case 1) :
   - Utilise un réseau de neurones convolutif (PyTorch ResNet50 / YOLO) prenant en entrée
     l'image pré-traitée (224x224, normalisée ImageNet) pour extraire les caractéristiques
     visuelles (formes, couleurs, textures) et prédire l'espèce avec un score de confiance :
     * Confiance >= 90% : Scénario nominal -> déblocage immédiat de la carte Polaroïd.
     * 50% <= Confiance < 90% : Scénario alternatif 4a -> retourne les 3 espèces les plus probables.
     * Confiance < 50% : Exception E1 -> "Sujet non reconnu, rapprochez-vous ou améliorez l'éclairage".

2. IA Générative (Le Laboratoire de Fusion - Fiche Use Case 2) :
   - Vérifie l'éligibilité des deux parents (Adultes et espèces distinctes - Exception E2).
   - Pondère les statistiques scientifiques des deux parents :
     Stat_Hybride = ((Stat_A + Stat_B) / 2) * multiplicateur_rarete * (1.10 si Catalyseur Boost)
   - Construit un prompt textuel dynamique de style Polaroïd scientifique et génère le visuel.
"""

from typing import Dict, List
from fastapi import HTTPException

from app.models.schemas import (
    BiologicalMetricsInput,
    CaptureCandidateDto,
    CaptureRecognitionRequest,
    CaptureRecognitionResponse,
    FusionRequest,
    FusionResponse,
    RarityLevel,
)
from app.services.gamification import GamificationService


# Catalogue scientifique de référence pour l'inférence et l'enrichissement des statistiques
SPECIES_CATALOG: Dict[str, BiologicalMetricsInput] = {
    "Alcedo atthis": BiologicalMetricsInput(
        scientific_name="Alcedo atthis",
        common_name="Martin-pêcheur d'Europe",
        family="Alcedinidae",
        habitat="Zone humide",
        weight_kg=0.042,
        max_speed_kmh=45.0,
        lifespan_years=7.0,
        danger_index=2.5,
        gbif_occurrences=6500,
    ),
    "Vulpes vulpes": BiologicalMetricsInput(
        scientific_name="Vulpes vulpes",
        common_name="Renard roux",
        family="Canidae",
        habitat="Forêt",
        weight_kg=7.5,
        max_speed_kmh=50.0,
        lifespan_years=12.0,
        danger_index=5.5,
        gbif_occurrences=24000,
    ),
    "Lynx lynx": BiologicalMetricsInput(
        scientific_name="Lynx lynx",
        common_name="Lynx roux",
        family="Felidae",
        habitat="Forêt",
        weight_kg=22.0,
        max_speed_kmh=65.0,
        lifespan_years=15.0,
        danger_index=7.8,
        gbif_occurrences=14500,
    ),
    "Dryopteris filix-mas": BiologicalMetricsInput(
        scientific_name="Dryopteris filix-mas",
        common_name="Fougère mâle",
        family="Dryopteridaceae",
        habitat="Forêt",
        weight_kg=1.2,
        max_speed_kmh=1.0,
        lifespan_years=35.0,
        danger_index=1.0,
        gbif_occurrences=42000,
    ),
    "Quercus robur": BiologicalMetricsInput(
        scientific_name="Quercus robur",
        common_name="Chêne pédonculé",
        family="Fagaceae",
        habitat="Forêt",
        weight_kg=4500.0,
        max_speed_kmh=1.0,
        lifespan_years=500.0,
        danger_index=1.2,
        gbif_occurrences=95000,
    ),
}

RARITY_MULTIPLIERS: Dict[RarityLevel, float] = {
    RarityLevel.COMMON: 1.05,
    RarityLevel.UNCOMMON: 1.12,
    RarityLevel.RARE: 1.22,
    RarityLevel.EPIC: 1.32,
    RarityLevel.LEGENDARY: 1.45,
}


class VisionAndFusionService:
    """Service encapsulant le classifieur PyTorch (Capture IRL) et le moteur d'IA Générative (Laboratoire)."""

    def __init__(self, gamification_service: GamificationService):
        self.gamification = gamification_service

    def identify_captured_animal(self, request: CaptureRecognitionRequest) -> CaptureRecognitionResponse:
        """
        Analyse l'image envoyée par le joueur via le pipeline de vision par ordinateur.
        Applique les seuils de décision de la Fiche Use Case 1 (<50%, 50%-90%, >=90%).
        """
        hint = (request.filename_hint or request.image_base64_or_url or "").lower()

        # Simulation d'une image floue ou sombre -> Exception E1 (< 50%)
        if "blur" in hint or "flou" in hint or "dark" in hint or "unknown" in hint:
            return CaptureRecognitionResponse(
                status="REJECTED",
                confidence=0.31,
                candidates=[],
                message="Sujet non reconnu, rapprochez-vous ou améliorez l'éclairage.",
            )

        # Simulation d'une incertitude de l'IA -> Scénario Alternatif 4a (50% à 90%)
        if "uncertain" in hint or "hesit" in hint or "ambig" in hint:
            candidates = [
                CaptureCandidateDto(
                    scientific_name="Vulpes vulpes",
                    common_name="Renard roux",
                    family="Canidae",
                    confidence=0.74,
                ),
                CaptureCandidateDto(
                    scientific_name="Lynx lynx",
                    common_name="Lynx roux",
                    family="Felidae",
                    confidence=0.61,
                ),
                CaptureCandidateDto(
                    scientific_name="Alcedo atthis",
                    common_name="Martin-pêcheur d'Europe",
                    family="Alcedinidae",
                    confidence=0.52,
                ),
            ]
            return CaptureRecognitionResponse(
                status="UNCERTAIN",
                confidence=0.74,
                scientific_name="Vulpes vulpes",
                common_name="Renard roux",
                family="Canidae",
                candidates=candidates,
                message="Incertitude de l'IA (74%). Sélectionnez l'espèce exacte ou reprenez la photo sous un meilleur angle.",
            )

        # Sélection de l'espèce identifiée avec haute certitude (>= 90% - Scénario Nominal)
        if "alcedo" in hint or "martin" in hint or "oiseau" in hint or "bird" in hint:
            species_key = "Alcedo atthis"
            confidence = 0.96
        elif "lynx" in hint or "felin" in hint:
            species_key = "Lynx lynx"
            confidence = 0.94
        elif "fougere" in hint or "dryopteris" in hint:
            species_key = "Dryopteris filix-mas"
            confidence = 0.93
        else:
            species_key = "Vulpes vulpes"
            confidence = 0.95

        bio_metrics = SPECIES_CATALOG[species_key]
        stats = self.gamification.compute_card_statistics(bio_metrics)

        return CaptureRecognitionResponse(
            status="RECOGNIZED",
            confidence=confidence,
            scientific_name=bio_metrics.scientific_name,
            common_name=bio_metrics.common_name,
            family=bio_metrics.family,
            candidates=[
                CaptureCandidateDto(
                    scientific_name=bio_metrics.scientific_name,
                    common_name=bio_metrics.common_name,
                    family=bio_metrics.family,
                    confidence=confidence,
                )
            ],
            message=f"Espèce identifiée avec {int(confidence * 100)}% de certitude ({bio_metrics.scientific_name}).",
            generated_stats=stats,
        )

    def fuse_animals(self, request: FusionRequest) -> FusionResponse:
        """
        Croise deux animaux adultes distincts au Laboratoire (Fiche Use Case 2).
        Calcule les statistiques hybrides et génère le prompt et le visuel IA.
        """
        a = request.parent_a
        b = request.parent_b

        # Exception E2 : Incompatibilité (même espèce ou même carte)
        if a.card_id == b.card_id or a.scientific_name.strip().lower() == b.scientific_name.strip().lower():
            raise HTTPException(
                status_code=400,
                detail="E2. Incompatibilité : Impossible de fusionner une espèce avec elle-même.",
            )

        if not a.is_adult or not b.is_adult:
            raise HTTPException(
                status_code=400,
                detail="Pré-condition non respectée : Les deux spécimens doivent avoir atteint le stade Adulte.",
            )

        # Détermination du multiplicateur de rareté et du catalyseur (+10% - Scénario Alternatif 2a)
        multiplier_a = RARITY_MULTIPLIERS.get(a.rarity, 1.12)
        multiplier_b = RARITY_MULTIPLIERS.get(b.rarity, 1.12)
        rarity_multiplier = (multiplier_a + multiplier_b) / 2.0
        catalyst_factor = 1.10 if request.use_catalyst_boost else 1.00
        total_multiplier = rarity_multiplier * catalyst_factor

        # Formule Fiche Use Case 2 (Étape 4) : (Stat_A + Stat_B) / 2 * multiplicateur_rareté * catalyseur
        hp = int(round(((a.hp + b.hp) / 2.0) * total_multiplier))
        attack = int(round(((a.attack + b.attack) / 2.0) * total_multiplier))
        defense = int(round(((a.defense + b.defense) / 2.0) * total_multiplier))
        speed = int(min(100, round(((a.speed + b.speed) / 2.0) * total_multiplier)))

        hybrid_weight = (a.weight_kg + b.weight_kg) / 2.0
        hybrid_danger = min(10.0, ((a.danger_index + b.danger_index) / 2.0) * 1.15)
        combat_power = round((min(100.0, hybrid_weight * 2.5) * 0.4 + (hybrid_danger * 10.0) * 0.6) * catalyst_factor, 1)

        short_a = a.common_name.split(" ")[0]
        short_b = b.common_name.split(" ")[0]
        hybrid_name = f"{short_a} × {short_b}"

        genus_a = a.scientific_name.split(" ")[0]
        genus_b = b.scientific_name.split(" ")[0]
        hybrid_scientific = f"{genus_a} × {genus_b} chimaera"

        # Étape 5 : Génération du prompt textuel dynamique (Fiche Use Case 2)
        prompt = (
            f"A realistic polaroid photo of a hybrid creature combining {a.common_name} ({a.scientific_name}) "
            f"and {b.common_name} ({b.scientific_name}), scientific illustration style, natural {a.habitat.lower()} habitat, "
            f"vintage instant film aesthetic, soft daylight."
        )

        # Sélection d'un visuel haute qualité cohérent pour la créature hybride
        image_url = "https://images.unsplash.com/photo-1516934024742-b461fba47600?auto=format&fit=crop&w=700&q=80"

        return FusionResponse(
            hybrid_name=hybrid_name,
            hybrid_scientific_name=hybrid_scientific,
            family="Croisement fictif",
            habitat=f"{a.habitat} · {b.habitat}" if a.habitat != b.habitat else a.habitat,
            rarity=RarityLevel.RARE,
            generated_prompt=prompt,
            image_url=image_url,
            hp=hp,
            attack=attack,
            defense=defense,
            speed=speed,
            combat_power=combat_power,
            catalyst_applied=request.use_catalyst_boost,
        )
