"""
Étape 2 du Pipeline Data : Analyse Spatio-Temporelle (Le Moteur des Expéditions)
--------------------------------------------------------------------------------
1. Clustering Spatial (Scikit-Learn DBSCAN) :
   - Prend les coordonnées GPS nettoyées d'une espèce et les convertit en radians.
   - Applique l'algorithme DBSCAN avec la métrique sphérique 'haversine' pour regrouper
     les points en zones de forte densité ("Hotspots") tout en écartant le bruit isolé (label = -1).
   - Ces clusters deviennent les zones cliquables sur le Globe 3D interactif de BIOMA.

2. Analyse Temporelle (Saisonnalité) :
   - Analyse la distribution statistique des mois d'observation ('month' issu de la date ISO 8601)
     au sein de chaque cluster.
   - Détermine la saison de présence maximale (Printemps, Été, Automne, Hiver) et vérifie
     si la fenêtre temporelle d'expédition est ouverte à la date actuelle (User Story #19).
"""

from datetime import datetime, timezone
from typing import Dict, List
import numpy as np
import pandas as pd
from sklearn.cluster import DBSCAN

from app.models.schemas import ExpeditionAnalysisResponse, HotspotClusterDto, Season

EARTH_RADIUS_KM = 6371.0088

# Dictionnaire de nommage géographique pour les principaux hotspots européens/mondiaux
KNOWN_REGIONS = [
    ("Vallée de la Loire", 47.39, 0.69),
    ("Réserve de Camargue", 43.53, 4.63),
    ("Bassin Alpin · Lac d'Annecy", 45.89, 6.13),
    ("Massif du Jura", 46.56, 5.94),
    ("Forêt de Białowieża", 52.71, 23.85),
    ("Forêt de Fontainebleau", 48.40, 2.70),
    ("Archipel du Svalbard (Arctique)", 78.22, 15.63),
]


class SpatioTemporalClusteringService:
    """Service d'analyse spatio-temporelle combinant DBSCAN (Scikit-Learn) et statistiques saisonnières Pandas."""

    def __init__(self, eps_km: float = 120.0, min_samples: int = 3):
        self.eps_km = eps_km
        self.min_samples = min_samples

    def compute_expedition_hotspots(
        self,
        df_cleaned: pd.DataFrame,
        scientific_name: str,
        common_name: str,
        current_month: int | None = None,
    ) -> ExpeditionAnalysisResponse:
        """
        Exécute DBSCAN sur les coordonnées GPS nettoyées et calcule la saisonnalité de chaque cluster.
        """
        if current_month is None:
            current_month = datetime.now(timezone.utc).month

        if df_cleaned.empty or len(df_cleaned) < self.min_samples:
            return ExpeditionAnalysisResponse(
                scientific_name=scientific_name,
                common_name=common_name,
                total_valid_observations=len(df_cleaned),
                hotspots=[],
            )

        # 1. Conversion des coordonnées (Latitude, Longitude) en radians pour la distance Haversine
        coords_deg = df_cleaned[["latitude", "longitude"]].to_numpy(dtype=np.float64)
        coords_rad = np.radians(coords_deg)

        eps_rad = self.eps_km / EARTH_RADIUS_KM
        dbscan = DBSCAN(eps=eps_rad, min_samples=self.min_samples, metric="haversine", algorithm="ball_tree")
        labels = dbscan.fit_predict(coords_rad)

        df_clustered = df_cleaned.copy()
        df_clustered["cluster"] = labels

        # Filtrer le bruit (label == -1)
        valid_clusters = [c for c in sorted(df_clustered["cluster"].unique()) if c != -1]
        total_clustered_obs = int((df_clustered["cluster"] != -1).sum()) or 1

        hotspots: List[HotspotClusterDto] = []
        for cluster_id in valid_clusters:
            sub_df = df_clustered[df_clustered["cluster"] == cluster_id]
            center_lat = float(sub_df["latitude"].mean())
            center_lon = float(sub_df["longitude"].mean())
            obs_count = int(len(sub_df))

            # Calcul du rayon moyen du cluster en km
            lat_diffs = np.radians(sub_df["latitude"].to_numpy() - center_lat)
            lon_diffs = np.radians(sub_df["longitude"].to_numpy() - center_lon)
            a = (
                np.sin(lat_diffs / 2.0) ** 2
                + np.cos(np.radians(center_lat))
                * np.cos(np.radians(sub_df["latitude"].to_numpy()))
                * np.sin(lon_diffs / 2.0) ** 2
            )
            distances_km = 2.0 * EARTH_RADIUS_KM * np.arcsin(np.sqrt(np.clip(a, 0.0, 1.0)))
            radius_km = round(float(max(25.0, np.max(distances_km) * 1.25)), 1)

            # 2. Analyse Temporelle (Saisonnalité via Pandas value_counts sur la colonne 'month')
            month_counts = sub_df["month"].value_counts()
            peak_months = [int(m) for m in month_counts.head(3).index.tolist()]
            optimal_season = self._determine_season_from_months(peak_months)

            # Une zone est ouverte si le mois courant est dans les mois pics (±1 mois de tolérance)
            expanded_window = set()
            for m in peak_months:
                expanded_window.update([((m - 2) % 12) + 1, m, (m % 12) + 1])
            is_open = current_month in expanded_window

            density_score = round(min(1.0, obs_count / max(5.0, total_clustered_obs * 0.6)), 2)
            region_name = self._resolve_region_name(center_lat, center_lon, int(cluster_id))

            hotspots.append(
                HotspotClusterDto(
                    cluster_id=int(cluster_id),
                    region_name=region_name,
                    center_latitude=round(center_lat, 4),
                    center_longitude=round(center_lon, 4),
                    radius_km=radius_km,
                    observation_count=obs_count,
                    density_score=density_score,
                    peak_months=sorted(peak_months),
                    optimal_season=optimal_season,
                    is_currently_open=is_open,
                )
            )

        return ExpeditionAnalysisResponse(
            scientific_name=scientific_name,
            common_name=common_name,
            total_valid_observations=len(df_cleaned),
            algorithm=f"DBSCAN (metric=haversine, eps_km={self.eps_km}, min_samples={self.min_samples})",
            hotspots=hotspots,
        )

    @staticmethod
    def _determine_season_from_months(months: List[int]) -> Season:
        if not months:
            return Season.ALL_YEAR
        season_votes: Dict[Season, int] = {
            Season.SPRING: 0,
            Season.SUMMER: 0,
            Season.AUTUMN: 0,
            Season.WINTER: 0,
        }
        for m in months:
            if m in (3, 4, 5):
                season_votes[Season.SPRING] += 1
            elif m in (6, 7, 8):
                season_votes[Season.SUMMER] += 1
            elif m in (9, 10, 11):
                season_votes[Season.AUTUMN] += 1
            else:
                season_votes[Season.WINTER] += 1
        return max(season_votes, key=season_votes.get)

    @staticmethod
    def _resolve_region_name(lat: float, lon: float, cluster_id: int) -> str:
        for name, k_lat, k_lon in KNOWN_REGIONS:
            if abs(lat - k_lat) < 1.5 and abs(lon - k_lon) < 1.5:
                return name
        hemisphere = "Nord" if lat >= 0 else "Sud"
        return f"Zone Bio-Cluster #{cluster_id + 1} ({lat:.1f}° {hemisphere})"
