# BIOMA — SAE S5.A.01 (Collection Scientifique & Biodiversité par IA)

Application complète de collection de cartes Polaroïd d'espèces réelles (**GBIF** & **iNaturalist**) développée selon l'architecture tri-applicative exigée :
1. **Back-Office C# (.NET 9 / ASP.NET Core / EF Core / SignalR)** : `backend-csharp/` — Logique métier, persistance relationnelle PostgreSQL (UUID `Guid`, `DateTimeOffset`, verrouillage optimiste `RowVersion`), Marché aux Enchères temps réel via WebSockets (`SignalR`), Quiz chronométré côté serveur (10s) et Batailles 1v1 scientifiques.
2. **Back-Office Python (FastAPI / Pandas / Scikit-Learn DBSCAN / PyTorch)** : `backend-python/` — Pipeline d'acquisition et nettoyage des données scientifiques, clustering spatial DBSCAN pour le Globe 3D, reconnaissance d'images (Capture IRL) et fusion générative au Laboratoire.
3. **Frontend Blazor (.NET 9 Interactive Razor Components)** : `frontend-blazor/Bioma.Client/` — Interface fidèle aux maquettes **Figma** du Cahier des Charges (Refuge, Tirages *Échos du vivant*, Vitrines 3D *Mes biomes*, Collection & Progression XP, Quiz Chronométré, Globe 3D d'Expéditions, Laboratoire IA et Marché aux Enchères).

---

### Déploiement en ligne (Azure Container Apps)

| Service | Lien |
|---|---|
| **Frontend Blazor** | https://bioma-blazor.gentlebay-5dd53ad7.francecentral.azurecontainerapps.io |
| **Backend C# (.NET API — Healthcheck)** | https://bioma-api.gentlebay-5dd53ad7.francecentral.azurecontainerapps.io/health |
| **Backend Python (FastAPI — Swagger)** | https://bioma-python.gentlebay-5dd53ad7.francecentral.azurecontainerapps.io/docs |

> **Déploiement automatique (CI/CD)** : chaque `git push` sur `main` déclenche le workflow [`.github/workflows/deploy-azure.yml`](.github/workflows/deploy-azure.yml) qui reconstruit les 3 images Docker et met à jour automatiquement les 3 services sur Azure.

---


### Lancer l'interface Blazor (avec données  pré-chargées)
```powershell
dotnet run --project w:\bioma\frontend-blazor\Bioma.Client\Bioma.Client.csproj --urls "http://localhost:5000"
```

### Lancer le Back-Office C# (.NET API + SignalR)
```powershell
dotnet run --project w:\bioma\backend-csharp\Bioma.Api\Bioma.Api.csproj --urls "http://localhost:5080"
```

### Lancer le Back-Office Python (FastAPI Data & AI)
```powershell
cd w:\bioma\backend-python
pip install -r requirements.txt
uvicorn app.main:app --reload --port 8000
```

### Exécuter les Tests Unitaires (.NET xUnit)
```powershell
dotnet test w:\bioma\Bioma.sln
```
