from typing import List, Optional
from fastapi import APIRouter, Query

from app.models.schemas import (
    ExpeditionAnalysisResponse,
    PreprocessingReportDto,
    RawObservationDto,
)
from app.services.clustering import SpatioTemporalClusteringService
from app.services.preprocessing import DataPreprocessingService

router = APIRouter(prefix="/api/v1/expeditions", tags=["Data Preprocessing & Spatio-Temporal (DBSCAN)"])

preprocessing_service = DataPreprocessingService()
clustering_service = SpatioTemporalClusteringService(eps_km=120.0, min_samples=3)


@router.get("/preprocessing-report/{scientific_name}", response_model=PreprocessingReportDto)
async def get_preprocessing_report(scientific_name: str):
    """
    Étape 1 du Pipeline : Récupère les observations GBIF (taxonKey=1) & iNaturalist (iconic_taxa=Animalia)
    et retourne le rapport de nettoyage Pandas (observations sans GPS, dates invalides, anomalies géographiques).
    """
    raw_obs = await preprocessing_service.fetch_external_observations(scientific_name)
    _, report = preprocessing_service.clean_observations_dataframe(raw_obs, scientific_name)
    return report


@router.get("/hotspots/{scientific_name}", response_model=ExpeditionAnalysisResponse)
async def get_species_expedition_hotspots(
    scientific_name: str,
    month: Optional[int] = Query(default=None, ge=1, le=12, description="Mois d'expédition (1-12) pour tester la saisonnalité"),
):
    """
    Étape 2 du Pipeline : Exécute le nettoyage Pandas puis l'algorithme DBSCAN (Scikit-Learn)
    et l'analyse temporelle pour retourner les zones de forte densité cliquables sur le Globe 3D.
    """
    raw_obs = await preprocessing_service.fetch_external_observations(scientific_name)
    df_cleaned, _ = preprocessing_service.clean_observations_dataframe(raw_obs, scientific_name)
    common_name = raw_obs[0].common_name if raw_obs else scientific_name
    return clustering_service.compute_expedition_hotspots(df_cleaned, scientific_name, common_name, current_month=month)


@router.post("/analyze-custom", response_model=ExpeditionAnalysisResponse)
async def analyze_custom_observations(
    scientific_name: str,
    observations: List[RawObservationDto],
    month: Optional[int] = Query(default=None, ge=1, le=12),
):
    """Permet d'envoyer un lot personnalisé d'observations brutes pour exécuter Pandas + DBSCAN en direct."""
    df_cleaned, _ = preprocessing_service.clean_observations_dataframe(observations, scientific_name)
    common_name = observations[0].common_name if observations else scientific_name
    return clustering_service.compute_expedition_hotspots(df_cleaned, scientific_name, common_name, current_month=month)
