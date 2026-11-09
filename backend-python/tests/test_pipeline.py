"""Tests unitaires du pipeline Data & IA Python (Pandas, DBSCAN, Gamification, Fusion)."""

from app.models.schemas import (
    BiologicalMetricsInput,
    CaptureRecognitionRequest,
    FusionParentDto,
    FusionRequest,
    RarityLevel,
)
from app.services.clustering import SpatioTemporalClusteringService
from app.services.gamification import GamificationService
from app.services.preprocessing import DataPreprocessingService
from app.services.vision_fusion import VisionAndFusionService


def test_pandas_cleaning_removes_missing_gps_and_paris_polar_bear_outlier():
    service = DataPreprocessingService()
    raw_obs = service.get_seed_raw_observations("Ursus maritimus")
    df_cleaned, report = service.clean_observations_dataframe(raw_obs, "Ursus maritimus")

    assert report.raw_count == 8
    assert report.dropped_missing_gps_or_date == 2
    assert report.dropped_geographic_outliers == 1  # Ours polaire à Paris (48.85°N) supprimé !
    assert report.cleaned_count == 5
    assert all(df_cleaned["latitude"] >= 58.0)


def test_dbscan_clustering_generates_hotspots_and_seasonality():
    prep = DataPreprocessingService()
    cluster_svc = SpatioTemporalClusteringService(eps_km=120.0, min_samples=3)
    raw_obs = prep.get_seed_raw_observations("Alcedo atthis")
    df_cleaned, _ = prep.clean_observations_dataframe(raw_obs, "Alcedo atthis")

    result = cluster_svc.compute_expedition_hotspots(
        df_cleaned, "Alcedo atthis", "Martin-pêcheur d'Europe", current_month=5
    )
    assert len(result.hotspots) >= 2
    assert any("Loire" in h.region_name for h in result.hotspots)


def test_gamification_math_formulas_and_fusion_catalyst():
    gamification = GamificationService()
    vision_fusion = VisionAndFusionService(gamification)

    bio = BiologicalMetricsInput(
        scientific_name="Vulpes vulpes",
        common_name="Renard roux",
        family="Canidae",
        habitat="Forêt",
        weight_kg=7.5,
        max_speed_kmh=50.0,
        lifespan_years=12.0,
        danger_index=5.5,
        gbif_occurrences=24000,
    )
    stats = gamification.compute_card_statistics(bio)
    assert 40 <= stats.hp <= 320
    assert stats.rarity == RarityLevel.UNCOMMON

    # Test Fusion +10% Catalyst Boost
    parent_a = FusionParentDto(
        card_id="11111111-1111-1111-1111-111111111111",
        scientific_name="Vulpes vulpes",
        common_name="Renard roux",
        family="Canidae",
        habitat="Forêt",
        rarity=RarityLevel.UNCOMMON,
        is_adult=True,
        hp=120,
        attack=110,
        defense=85,
        speed=50,
        weight_kg=7.5,
        danger_index=5.5,
    )
    parent_b = FusionParentDto(
        card_id="22222222-2222-2222-2222-222222222222",
        scientific_name="Lynx lynx",
        common_name="Lynx roux",
        family="Felidae",
        habitat="Forêt",
        rarity=RarityLevel.UNCOMMON,
        is_adult=True,
        hp=150,
        attack=145,
        defense=105,
        speed=65,
        weight_kg=22.0,
        danger_index=7.8,
    )
    res_normal = vision_fusion.fuse_animals(FusionRequest(parent_a=parent_a, parent_b=parent_b, use_catalyst_boost=False))
    res_boosted = vision_fusion.fuse_animals(FusionRequest(parent_a=parent_a, parent_b=parent_b, use_catalyst_boost=True))

    assert res_boosted.hp > res_normal.hp
    assert res_boosted.hybrid_name == "Renard × Lynx"
