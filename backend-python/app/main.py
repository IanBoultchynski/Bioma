"""
BIOMA - Back-Office Python (FastAPI : Data Processing, Spatio-Temporal DBSCAN & AI Engine)
------------------------------------------------------------------------------------------
Architecture en couches (Routers -> Services -> Domain Schemas) assurant :
1. Acquisition & Nettoyage Pandas (GBIF taxonKey=1 & iNaturalist Research Grade)
2. Clustering Spatial DBSCAN (Scikit-Learn) & Analyse de Saisonnalité pour le Globe 3D
3. Reconnaissance d'Images PyTorch (ResNet50/YOLO) & Fusion Générative au Laboratoire
4. Moteur Mathématique de Gamification (Normalisation logarithmique & Min-Max)
"""

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

from app.routers import expeditions, lab

app = FastAPI(
    title="BIOMA - Python AI & Data Processing Back-Office",
    description=(
        "Back-Office dédié au traitement des données biodiversités (GBIF / iNaturalist via Pandas), "
        "au clustering spatio-temporel DBSCAN (Scikit-Learn), à la vision par ordinateur (PyTorch) "
        "et à l'IA générative de fusion animale."
    ),
    version="1.0.0",
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

app.include_router(expeditions.router)
app.include_router(lab.router)


@app.get("/health", tags=["System"])
async def health_check():
    return {
        "status": "healthy",
        "service": "bioma-python-ai-backoffice",
        "pipeline_modules": [
            "1. Pandas Data Preprocessing (GBIF & iNaturalist)",
            "2. Scikit-Learn DBSCAN Spatio-Temporal Clustering",
            "3. PyTorch Vision Classification & Generative Fusion Lab",
            "4. Mathematical Gamification Engine",
        ],
    }
