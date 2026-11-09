from datetime import datetime
from enum import Enum
from typing import Dict, List, Optional
from pydantic import BaseModel, Field


class RarityLevel(str, Enum):
    COMMON = "Commune"
    UNCOMMON = "Peu commune"
    RARE = "Rare"
    EPIC = "Épique"
    LEGENDARY = "Légendaire"


class Season(str, Enum):
    SPRING = "Printemps"
    SUMMER = "Été"
    AUTUMN = "Automne"
    WINTER = "Hiver"
    ALL_YEAR = "Toute l'année"


class RawObservationDto(BaseModel):
    """Observation brute issue de GBIF (taxonKey=1) ou iNaturalist (iconic_taxa=Animalia)."""
    source: str = Field(default="GBIF", description="GBIF ou iNaturalist")
    scientific_name: str = Field(..., example="Vulpes vulpes")
    common_name: str = Field(..., example="Renard roux")
    family: str = Field(default="Canidae")
    kingdom: str = Field(default="Animalia")
    latitude: Optional[float] = Field(None, example=45.8992)
    longitude: Optional[float] = Field(None, example=6.1294)
    uncertainty_meters: Optional[float] = Field(default=50.0)
    observed_on: Optional[str] = Field(None, example="2026-05-14T09:30:00Z")
    media_url: Optional[str] = Field(None)
    quality_grade: str = Field(default="research")
    expected_climate: Optional[str] = Field(default="Tempéré", description="Polaire, Tropical, Tempéré, Aride, Marin")


class PreprocessingReportDto(BaseModel):
    """Rapport détaillé de l'étape 1 : Acquisition & Nettoyage Pandas."""
    scientific_name: str
    raw_count: int
    cleaned_count: int
    dropped_missing_gps_or_date: int
    dropped_geographic_outliers: int
    normalized_images_size: str = "224x224 (RGB ImageNet normalized)"
    sample_cleaned_records: List[Dict]


class HotspotClusterDto(BaseModel):
    """Zone de haute densité calculée par DBSCAN pour le Globe 3D (Étape 2)."""
    cluster_id: int
    region_name: str
    center_latitude: float
    center_longitude: float
    radius_km: float
    observation_count: int
    density_score: float = Field(..., description="Score normalisé [0..1] pour l'intensité lumineuse sur le Globe 3D")
    peak_months: List[int] = Field(..., description="Mois de visibilité maximale (1 = Janvier ... 12 = Décembre)")
    optimal_season: Season
    is_currently_open: bool


class ExpeditionAnalysisResponse(BaseModel):
    """Réponse complète du moteur spatio-temporel pour une espèce."""
    scientific_name: str
    common_name: str
    total_valid_observations: int
    algorithm: str = "DBSCAN (metric=haversine, eps_km=150, min_samples=5)"
    hotspots: List[HotspotClusterDto]


class BiologicalMetricsInput(BaseModel):
    """Données biologiques réelles en entrée du moteur de Gamification (Étape 4)."""
    scientific_name: str = Field(..., example="Alcedo atthis")
    common_name: str = Field(..., example="Martin-pêcheur d'Europe")
    family: str = Field(default="Alcedinidae")
    habitat: str = Field(default="Zone humide")
    weight_kg: float = Field(..., gt=0, example=0.04)
    max_speed_kmh: float = Field(..., gt=0, example=45.0)
    lifespan_years: float = Field(default=10.0, gt=0)
    danger_index: float = Field(..., ge=1.0, le=10.0, description="Indice scientifique de dangerosité/prédation (1 à 10)")
    gbif_occurrences: int = Field(default=25000, ge=1, description="Nombre d'occurrences mondiales GBIF/iNaturalist")


class GameCardStatsDto(BaseModel):
    """Statistiques de jeu calculées mathématiquement à partir de la réalité scientifique."""
    scientific_name: str
    common_name: str
    family: str
    habitat: str
    hp: int = Field(..., description="Points de Vie calculés par normalisation logarithmique de la masse et longévité")
    attack: int = Field(..., description="Attaque calculée via vitesse max et indice de dangerosité")
    defense: int = Field(..., description="Défense calculée via robustesse et masse")
    speed: int = Field(..., description="Vitesse normalisée sur 100")
    combat_power: float = Field(..., description="Puissance = PoidsScore*0.4 + DangerScore*0.6")
    rarity: RarityLevel
    drop_rate_percent: float


class CaptureCandidateDto(BaseModel):
    scientific_name: str
    common_name: str
    family: str
    confidence: float


class CaptureRecognitionRequest(BaseModel):
    """Requête d'identification visuelle (L'Instantané - Fiche Use Case 1)."""
    image_base64_or_url: str
    latitude: Optional[float] = None
    longitude: Optional[float] = None
    filename_hint: Optional[str] = None


class CaptureRecognitionResponse(BaseModel):
    """Réponse du modèle PyTorch (ResNet50/YOLO) suivant les règles de la Fiche 1."""
    status: str = Field(..., description="RECOGNIZED (>90%), UNCERTAIN (50%-90%), REJECTED (<50%)")
    confidence: float
    scientific_name: Optional[str] = None
    common_name: Optional[str] = None
    family: Optional[str] = None
    candidates: List[CaptureCandidateDto] = Field(default_factory=list, description="Top 3 espèces si incertitude (50%-90%)")
    message: str
    generated_stats: Optional[GameCardStatsDto] = None


class FusionParentDto(BaseModel):
    card_id: str
    scientific_name: str
    common_name: str
    family: str
    habitat: str
    rarity: RarityLevel = RarityLevel.RARE
    is_adult: bool = True
    hp: int
    attack: int
    defense: int
    speed: int
    weight_kg: float
    danger_index: float


class FusionRequest(BaseModel):
    """Requête de fusion hybride au Laboratoire (Fiche Use Case 2)."""
    parent_a: FusionParentDto
    parent_b: FusionParentDto
    use_catalyst_boost: bool = Field(default=False, description="Scénario alternatif 2a : Boost +10% sur les statistiques")


class FusionResponse(BaseModel):
    """Résultat de la génération IA et du calcul de stats hybrides."""
    hybrid_name: str = Field(..., example="Renard × Lynx")
    hybrid_scientific_name: str = Field(..., example="Vulpes × Lynx hybridus")
    family: str
    habitat: str
    rarity: RarityLevel
    generated_prompt: str
    image_url: str
    hp: int
    attack: int
    defense: int
    speed: int
    combat_power: float
    catalyst_applied: bool
