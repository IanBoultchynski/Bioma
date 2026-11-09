"""
BIOMA Simplifié - Micro-connecteur Python (Optionnel) vers GBIF & iNaturalist
"""

from fastapi import FastAPI
import httpx

app = FastAPI(title="Bioma Simplified - Connecteur GBIF & iNaturalist", version="1.0.0")

GBIF_URL = "https://api.gbif.org/v1/occurrence/search"
INATURALIST_URL = "https://api.inaturalist.org/v1/observations"


@app.get("/health")
async def health():
    return {"status": "healthy", "connectors": ["GBIF", "iNaturalist"]}


@app.get("/api/search")
async def search_species(query: str = "Vulpes vulpes", limit: int = 3):
    results = []
    async with httpx.AsyncClient(timeout=5.0) as client:
        gbif_resp = await client.get(GBIF_URL, params={"q": query, "hasCoordinate": "true", "limit": limit})
        if gbif_resp.status_code == 200:
            for item in gbif_resp.json().get("results", []):
                results.append({
                    "source": "GBIF",
                    "scientific_name": item.get("species") or item.get("scientificName") or query,
                    "country": item.get("country", "International"),
                    "latitude": item.get("decimalLatitude"),
                    "longitude": item.get("decimalLongitude"),
                })

        inat_resp = await client.get(INATURALIST_URL, params={"q": query, "quality_grade": "research", "per_page": limit})
        if inat_resp.status_code == 200:
            for obs in inat_resp.json().get("results", []):
                taxon = obs.get("taxon") or {}
                results.append({
                    "source": "iNaturalist",
                    "scientific_name": taxon.get("name") or query,
                    "common_name": taxon.get("preferred_common_name") or query,
                    "place": obs.get("place_guess", "Terrain"),
                })
    return {"query": query, "count": len(results), "results": results}
