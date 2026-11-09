# BIOMA — Version Simplifiée (GBIF & iNaturalist + Front-End + Début Back-Office C#)

Application redimensionnée et simplifiée autour de 3 briques essentielles :
1. **Connexions simples aux APIs GBIF & iNaturalist** (`https://api.gbif.org/v1/occurrence/search` & `https://api.inaturalist.org/v1/observations`).
2. **Début du Back-Office C# (.NET 9 / EF Core)** :
   - `Bioma.Domain` : Entité unique `SpeciesCard`.
   - `Bioma.Application` : DTOs (`ExternalObservationDto`, `ApiSearchResultDto`, `CardDto`) et interface `IBiomaGameService`.
   - `Bioma.Infrastructure` : `BiomaDbContext` (EF Core InMemory / PostgreSQL) et `BiomaGameService` (requêtes HTTP vers GBIF/iNaturalist + sauvegarde/suppression).
   - `Bioma.Api` : Contrôleur REST `BiomaControllers` (`/api/biodiversity/search`, `/api/biodiversity/gbif`, `/api/biodiversity/inaturalist`, `/api/biodiversity/cards`).
3. **Une seule page Front-End Blazor** (`Pages/Refuge.razor` sur `/`) :
   - Recherche d'espèces en direct sur GBIF et iNaturalist.
   - Ajout en un clic d'une observation dans le Back-Office C# et affichage sous forme de cartes Polaroïd.

## Démarrage rapide

### 1. Lancer la page Front-End Blazor
```powershell
dotnet run --project frontend-blazor/Bioma.Client/Bioma.Client.csproj --urls "http://localhost:5000"
```

### 2. Lancer l'API REST du Back-Office C#
```powershell
dotnet run --project backend-csharp/Bioma.Api/Bioma.Api.csproj --urls "http://localhost:5080"
```

### 3. Exécuter les tests unitaires
```powershell
dotnet test Bioma.sln
```
