from fastapi import APIRouter

from app.models.schemas import (
    BiologicalMetricsInput,
    CaptureRecognitionRequest,
    CaptureRecognitionResponse,
    FusionRequest,
    FusionResponse,
    GameCardStatsDto,
)
from app.services.gamification import GamificationService
from app.services.vision_fusion import VisionAndFusionService

router = APIRouter(prefix="/api/v1/lab", tags=["AI Vision, Fusion Lab & Gamification"])

gamification_service = GamificationService()
vision_fusion_service = VisionAndFusionService(gamification_service=gamification_service)


@router.post("/capture-identify", response_model=CaptureRecognitionResponse)
async def identify_animal_capture(request: CaptureRecognitionRequest):
    """
    Étape 3a (Capture IRL - L'Instantané) :
    Analyse l'image capturée par le joueur via le modèle PyTorch (ResNet50/YOLO)
    et retourne l'espèce identifiée, le taux de certitude et ses statistiques scientifiques gamifiées.
    """
    return vision_fusion_service.identify_captured_animal(request)


@router.post("/fuse", response_model=FusionResponse)
async def fuse_two_animals(request: FusionRequest):
    """
    Étape 3b (Le Laboratoire - Fusion Animale) :
    Croise deux cartes Adultes distinctes, calcule les statistiques hybrides pondérées
    et génère le prompt ainsi que le visuel de la créature hybride par IA Générative.
    """
    return vision_fusion_service.fuse_animals(request)


@router.post("/gamification/compute-stats", response_model=GameCardStatsDto)
async def compute_gamified_stats(bio_input: BiologicalMetricsInput):
    """
    Étape 4 (Moteur de Gamification) :
    Convertit des données biologiques réelles (Poids, Vitesse, Dangerosité, Occurrences GBIF)
    en statistiques de jeu équilibrées (PV, Attaque, Défense, Puissance, Rareté).
    """
    return gamification_service.compute_card_statistics(bio_input)
