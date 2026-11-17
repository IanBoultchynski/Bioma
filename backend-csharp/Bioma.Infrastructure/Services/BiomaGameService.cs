using System.Collections.Concurrent;
using System.Text.Json;
using Bioma.Application.DTOs;
using Bioma.Application.Interfaces;
using Bioma.Domain.Entities;
using Bioma.Domain.Enums;
using Bioma.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Bioma.Infrastructure.Services;

/// <summary>
/// Back-Office C# :
/// - Interrogation précise des APIs publiques iNaturalist et GBIF par identifiant taxonomique (taxon_id / taxonKey)
/// - Filtrage strict des observations de terrain (quality_grade=research &amp; HUMAN_OBSERVATION)
/// - Calcul dynamique de la rareté naturelle et des statistiques directement depuis le volume d'observations de l'API
/// </summary>
public class BiomaGameService : IBiomaGameService
{
    private const string GbifSpeciesMatchUrl = "https://api.gbif.org/v1/species/match";
    private const string GbifOccurrenceSearchUrl = "https://api.gbif.org/v1/occurrence/search";
    private const string InaturalistTaxaUrl = "https://api.inaturalist.org/v1/taxa";
    private const string InaturalistObservationsUrl = "https://api.inaturalist.org/v1/observations";

    private static readonly ConcurrentDictionary<string, ApiSearchResultDto> SearchResultCache = new(StringComparer.OrdinalIgnoreCase);

    private readonly BiomaDbContext _db;
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public BiomaGameService(BiomaDbContext db, HttpClient httpClient)
    {
        _db = db;
        _httpClient = httpClient;
    }

    private record ResolvedTaxonInfo(
        int? InatTaxonId,
        string ScientificName,
        string CommonNameFr,
        string Family,
        long GlobalObservationsCount,
        string? DefaultPhotoUrl
    );

    public async Task<ApiSearchResultDto> SearchApisAsync(string query, int limitPerSource = 4, CancellationToken ct = default)
    {
        var cleanQuery = string.IsNullOrWhiteSpace(query) ? "Vulpes vulpes" : query.Trim();
        var cacheKey = $"{cleanQuery}:{limitPerSource}";

        if (SearchResultCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var taxonInfo = await ResolveTaxonFromApiAsync(cleanQuery, ct);

        var gbifTask = FetchFromGbifInternalAsync(taxonInfo, limitPerSource, ct);
        var inatTask = FetchFromInaturalistInternalAsync(taxonInfo, limitPerSource, ct);

        await Task.WhenAll(gbifTask, inatTask);

        var (gbifList, gbifLive) = gbifTask.Result;
        var (inatList, inatLive) = inatTask.Result;

        // Déduplication par URL d'image et tri cohérent : observations avec coordonnées GPS complètes et photos vérifiées en premier
        var combined = inatList
            .Concat(gbifList)
            .GroupBy(o => o.ImageUrl, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderByDescending(o => o.Rarity)
            .ThenByDescending(o => o.Latitude.HasValue && o.Longitude.HasValue)
            .ThenByDescending(o => o.ObservedDate)
            .ToList();

        string status = (gbifLive || inatLive)
            ? $"Espèce identifiée : {taxonInfo.CommonNameFr} ({taxonInfo.ScientificName}) · {taxonInfo.GlobalObservationsCount:N0} observations mondiales recensées."
            : $"Mode hors-ligne : données de démonstration pour {taxonInfo.CommonNameFr} ({taxonInfo.ScientificName}).";

        var result = new ApiSearchResultDto(
            cleanQuery,
            gbifLive,
            inatLive,
            gbifList.Count,
            inatList.Count,
            status,
            combined
        );

        SearchResultCache[cacheKey] = result;
        return result;
    }

    public async Task<List<ExternalObservationDto>> FetchFromGbifAsync(string query, int limit = 4, CancellationToken ct = default)
    {
        var taxonInfo = await ResolveTaxonFromApiAsync(query, ct);
        var (results, _) = await FetchFromGbifInternalAsync(taxonInfo, limit, ct);
        return results;
    }

    public async Task<List<ExternalObservationDto>> FetchFromInaturalistAsync(string query, int limit = 4, CancellationToken ct = default)
    {
        var taxonInfo = await ResolveTaxonFromApiAsync(query, ct);
        var (results, _) = await FetchFromInaturalistInternalAsync(taxonInfo, limit, ct);
        return results;
    }

    /// <summary>
    /// Résout le taxon exact via l'API iNaturalist (/v1/taxa) afin d'obtenir l'identifiant d'espèce,
    /// le vrai nom vernaculaire français, la famille taxonomique et le nombre réel d'observations dans la nature.
    /// </summary>
    private async Task<ResolvedTaxonInfo> ResolveTaxonFromApiAsync(string query, CancellationToken ct)
    {
        try
        {
            var url = $"{InaturalistTaxaUrl}?q={Uri.EscapeDataString(query)}&rank=species&locale=fr&per_page=5";
            using var response = await _httpClient.GetAsync(url, ct);
            if (response.IsSuccessStatusCode)
            {
                using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
                if (doc.RootElement.TryGetProperty("results", out var results) &&
                    results.ValueKind == JsonValueKind.Array &&
                    results.GetArrayLength() > 0)
                {
                    JsonElement best = results[0];
                    foreach (var el in results.EnumerateArray())
                    {
                        var name = GetString(el, "name");
                        if (string.Equals(name, query.Trim(), StringComparison.OrdinalIgnoreCase))
                        {
                            best = el;
                            break;
                        }
                    }

                    int? taxonId = best.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.Number
                        ? idProp.GetInt32()
                        : null;
                    string sciName = GetString(best, "name") ?? query.Trim();
                    string commonFr = GetString(best, "preferred_common_name") ?? sciName;
                    if (!string.IsNullOrEmpty(commonFr))
                    {
                        commonFr = char.ToUpper(commonFr[0]) + commonFr[1..];
                    }
                    string family = GetString(best, "iconic_taxon_name") ?? "Animalia";
                    long obsCount = best.TryGetProperty("observations_count", out var oc) && oc.ValueKind == JsonValueKind.Number
                        ? oc.GetInt64()
                        : 25_000;

                    string? defaultPhoto = null;
                    if (best.TryGetProperty("default_photo", out var dp) && dp.ValueKind == JsonValueKind.Object)
                    {
                        defaultPhoto = GetString(dp, "medium_url") ?? GetString(dp, "url")?.Replace("square", "medium");
                    }

                    GachaService.RegisterLiveOccurrenceCount(sciName, obsCount);

                    return new ResolvedTaxonInfo(taxonId, sciName, commonFr, family, obsCount, defaultPhoto);
                }
            }
        }
        catch
        {
            // Continue en mode hors-ligne si pas de réseau
        }

        long fallbackCount = GachaService.GetEstimatedNaturalOccurrences(query, CardRarity.Commune);
        return new ResolvedTaxonInfo(null, query.Trim(), query.Trim(), "Animalia", fallbackCount, null);
    }

    private async Task<(List<ExternalObservationDto> Items, bool IsLive)> FetchFromGbifInternalAsync(
        ResolvedTaxonInfo taxon,
        int limit,
        CancellationToken ct)
    {
        var items = new List<ExternalObservationDto>();
        try
        {
            // 1. Résolution de la clé d'espèce exacte GBIF (usageKey) pour éviter les résultats hors-sujet
            int? usageKey = null;
            var matchUrl = $"{GbifSpeciesMatchUrl}?name={Uri.EscapeDataString(taxon.ScientificName)}&kingdom=Animalia";
            using (var matchResp = await _httpClient.GetAsync(matchUrl, ct))
            {
                if (matchResp.IsSuccessStatusCode)
                {
                    using var matchDoc = await JsonDocument.ParseAsync(await matchResp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
                    if (matchDoc.RootElement.TryGetProperty("usageKey", out var uk) && uk.ValueKind == JsonValueKind.Number)
                    {
                        usageKey = uk.GetInt32();
                    }
                }
            }

            // 2. Recherche d'occurrences humaines avec photographie (HUMAN_OBSERVATION + StillImage)
            string searchParam = usageKey.HasValue
                ? $"taxonKey={usageKey.Value}"
                : $"scientificName={Uri.EscapeDataString(taxon.ScientificName)}";

            var url = $"{GbifOccurrenceSearchUrl}?{searchParam}&basisOfRecord=HUMAN_OBSERVATION&hasCoordinate=true&mediaType=StillImage&limit={Math.Clamp(limit * 3, 4, 24)}";
            using var response = await _httpClient.GetAsync(url, ct);

            if (response.IsSuccessStatusCode)
            {
                using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
                var rarity = GachaService.ComputeRarityFromOccurrences(taxon.GlobalObservationsCount);
                var (atk, def, spd, _) = GachaService.ComputeStatsFromRarity(rarity, taxon.ScientificName);

                if (doc.RootElement.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in results.EnumerateArray())
                    {
                        if (items.Count >= limit) break;

                        string? img = ExtractGbifImage(item);
                        if (string.IsNullOrWhiteSpace(img)) continue;

                        string key = item.TryGetProperty("key", out var k) ? k.ToString() : Guid.NewGuid().ToString("N")[..8];
                        string sciName = GetString(item, "species") ?? taxon.ScientificName;
                        if (!sciName.Equals(taxon.ScientificName, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        string family = GetString(item, "family") ?? taxon.Family;
                        string kingdom = GetString(item, "kingdom") ?? "Animalia";
                        double? lat = GetDouble(item, "decimalLatitude");
                        double? lon = GetDouble(item, "decimalLongitude");
                        string country = GetString(item, "country") ?? GetString(item, "stateProvince") ?? "Observation GBIF";
                        string date = (GetString(item, "eventDate") ?? DateTime.UtcNow.ToString("yyyy-MM-dd")).Split('T')[0];

                        items.Add(new ExternalObservationDto(
                            $"gbif-{key}",
                            "GBIF",
                            taxon.ScientificName,
                            taxon.CommonNameFr,
                            family,
                            kingdom,
                            lat,
                            lon,
                            country,
                            date,
                            img,
                            taxon.GlobalObservationsCount,
                            rarity,
                            GachaService.RarityLabel(rarity),
                            GachaService.DropRateForRarity(rarity),
                            atk,
                            def,
                            spd
                        ));
                    }

                    if (items.Count > 0)
                    {
                        return (items, true);
                    }
                }
            }
        }
        catch
        {
            // Fallback hors-ligne
        }

        items.AddRange(BuildFallbackObservations(taxon, "GBIF", Math.Min(limit, 2)));
        return (items, false);
    }

    private async Task<(List<ExternalObservationDto> Items, bool IsLive)> FetchFromInaturalistInternalAsync(
        ResolvedTaxonInfo taxon,
        int limit,
        CancellationToken ct)
    {
        var items = new List<ExternalObservationDto>();
        try
        {
            string taxonFilter = taxon.InatTaxonId.HasValue
                ? $"taxon_id={taxon.InatTaxonId.Value}"
                : $"taxon_name={Uri.EscapeDataString(taxon.ScientificName)}";

            // Tri par votes ("order_by=votes") sur les observations "research" pour garantir les meilleures photos d'animaux réels
            var url = $"{InaturalistObservationsUrl}?{taxonFilter}&quality_grade=research&photos=true&locale=fr&order_by=votes&per_page={Math.Clamp(limit * 2, 4, 20)}";
            using var response = await _httpClient.GetAsync(url, ct);

            if (response.IsSuccessStatusCode)
            {
                using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
                var rarity = GachaService.ComputeRarityFromOccurrences(taxon.GlobalObservationsCount);
                var (atk, def, spd, _) = GachaService.ComputeStatsFromRarity(rarity, taxon.ScientificName);

                if (doc.RootElement.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
                {
                    foreach (var obs in results.EnumerateArray())
                    {
                        if (items.Count >= limit) break;

                        string? img = ExtractInaturalistImage(obs) ?? taxon.DefaultPhotoUrl;
                        if (string.IsNullOrWhiteSpace(img)) continue;

                        string id = obs.TryGetProperty("id", out var idProp) ? idProp.ToString() : Guid.NewGuid().ToString("N")[..8];

                        double? lat = null;
                        double? lon = null;
                        if (obs.TryGetProperty("geojson", out var geo) &&
                            geo.TryGetProperty("coordinates", out var coords) &&
                            coords.ValueKind == JsonValueKind.Array &&
                            coords.GetArrayLength() >= 2)
                        {
                            lon = coords[0].ValueKind == JsonValueKind.Number ? coords[0].GetDouble() : null;
                            lat = coords[1].ValueKind == JsonValueKind.Number ? coords[1].GetDouble() : null;
                        }

                        string place = GetString(obs, "place_guess") ?? "Observation iNaturalist";
                        string date = GetString(obs, "observed_on") ?? DateTime.UtcNow.ToString("yyyy-MM-dd");

                        items.Add(new ExternalObservationDto(
                            $"inat-{id}",
                            "iNaturalist",
                            taxon.ScientificName,
                            taxon.CommonNameFr,
                            taxon.Family,
                            "Animalia",
                            lat,
                            lon,
                            place,
                            date,
                            img,
                            taxon.GlobalObservationsCount,
                            rarity,
                            GachaService.RarityLabel(rarity),
                            GachaService.DropRateForRarity(rarity),
                            atk,
                            def,
                            spd
                        ));
                    }

                    if (items.Count > 0)
                    {
                        return (items, true);
                    }
                }
            }
        }
        catch
        {
            // Fallback hors-ligne
        }

        items.AddRange(BuildFallbackObservations(taxon, "iNaturalist", Math.Min(limit, 2)));
        return (items, false);
    }

    public async Task<List<CardDto>> GetSavedCardsAsync()
    {
        await _gate.WaitAsync();
        try
        {
            var cards = await _db.Cards
                .AsNoTracking()
                .ToListAsync();

            return cards
                .GroupBy(c => c.ScientificName, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(x => x.CreatedAt).First())
                .OrderByDescending(c => c.Rarity)
                .ThenBy(c => GachaService.GetEstimatedNaturalOccurrences(c.ScientificName, c.Rarity))
                .Select(c => MapToCardDto(c))
                .ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<CardDto> SaveObservationToBackOfficeAsync(ExternalObservationDto observation, Guid? playerId = null)
    {
        var taxon = await ResolveTaxonFromApiAsync(observation.ScientificName, CancellationToken.None);
        long occurrences = observation.NaturalOccurrencesCount > 0
            ? observation.NaturalOccurrencesCount
            : taxon.GlobalObservationsCount;
        var rarity = GachaService.ComputeRarityFromOccurrences(occurrences);
        var (atk, def, spd, weight) = GachaService.ComputeStatsFromRarity(rarity, taxon.ScientificName);

        await _gate.WaitAsync();
        try
        {
            var resolvedBiome = Showcase3DService.ResolveBiomeForSpecies(
                taxon.ScientificName,
                observation.CommonName,
                observation.Latitude);

            var entity = new SpeciesCard
            {
                CommonName = !string.IsNullOrWhiteSpace(observation.CommonName) && !observation.CommonName.Equals(observation.ScientificName, StringComparison.OrdinalIgnoreCase)
                    ? observation.CommonName
                    : taxon.CommonNameFr,
                ScientificName = taxon.ScientificName,
                Family = string.IsNullOrWhiteSpace(observation.Family) ? taxon.Family : observation.Family,
                Kingdom = observation.Kingdom,
                SourceApi = observation.SourceApi,
                ExternalId = observation.ExternalId,
                ImageUrl = !string.IsNullOrWhiteSpace(observation.ImageUrl) ? observation.ImageUrl : (taxon.DefaultPhotoUrl ?? GetFallbackImageForQuery(taxon.ScientificName)),
                Latitude = observation.Latitude,
                Longitude = observation.Longitude,
                CountryOrPlace = observation.CountryOrPlace,
                ObservedDate = observation.ObservedDate,
                Rarity = rarity,
                Biome = resolvedBiome,
                BaseAttack = atk,
                BaseDefense = def,
                BaseSpeed = spd,
                AverageWeightKg = weight,
                CreatedAt = DateTimeOffset.UtcNow
            };

            _db.Cards.Add(entity);

            PlayerProfile? targetPlayer = null;
            if (playerId.HasValue)
            {
                targetPlayer = await _db.Players.FirstOrDefaultAsync(p => p.Id == playerId.Value);
            }
            else
            {
                var allPlayers = await _db.Players.ToListAsync();
                targetPlayer = allPlayers.OrderBy(p => p.CreatedAt).FirstOrDefault();
            }

            if (targetPlayer is not null)
            {
                _db.PlayerCards.Add(new PlayerCard
                {
                    PlayerId = targetPlayer.Id,
                    SpeciesCardId = entity.Id,
                    CustomNickname = entity.CommonName,
                    Level = 1,
                    ExperiencePoints = 0,
                    IsInActiveDeck = false,
                    CurrentAttack = entity.BaseAttack,
                    CurrentDefense = entity.BaseDefense,
                    CurrentSpeed = entity.BaseSpeed,
                    WeightKg = entity.AverageWeightKg,
                    PolaroidFinish = GachaService.PolaroidFinishForRarity(entity.Rarity),
                    ObtainedVia = observation.SourceApi,
                    AcquiredAt = DateTimeOffset.UtcNow
                });
                targetPlayer.LastCloudSyncAt = DateTimeOffset.UtcNow;
            }

            await _db.SaveChangesAsync();

            return MapToCardDto(entity, occurrences);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> DeleteCardAsync(Guid cardId)
    {
        await _gate.WaitAsync();
        try
        {
            var card = await _db.Cards.FirstOrDefaultAsync(c => c.Id == cardId);
            if (card is null)
            {
                return false;
            }

            _db.Cards.Remove(card);
            await _db.SaveChangesAsync();
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static CardDto MapToCardDto(SpeciesCard card)
    {
        long occurrences = GachaService.GetEstimatedNaturalOccurrences(card.ScientificName, card.Rarity);
        return MapToCardDto(card, occurrences);
    }

    private static CardDto MapToCardDto(SpeciesCard card, long occurrences)
    {
        string coords = card.Latitude.HasValue && card.Longitude.HasValue
            ? $"{card.Latitude.Value:F2}°, {card.Longitude.Value:F2}°"
            : "Référentiel API";

        var biome = Showcase3DService.ResolveBiomeForSpecies(card.ScientificName, card.CommonName, card.Latitude);

        return new CardDto(
            card.Id,
            card.CommonName,
            card.ScientificName,
            card.Family,
            card.SourceApi,
            card.ImageUrl,
            coords,
            card.CountryOrPlace,
            card.ObservedDate,
            card.Rarity,
            GachaService.RarityLabel(card.Rarity),
            occurrences,
            GachaService.DropRateForRarity(card.Rarity),
            card.BaseAttack,
            card.BaseDefense,
            card.BaseSpeed,
            biome,
            Showcase3DService.GetBiomeLabel(biome)
        );
    }

    private static string? GetString(JsonElement el, string propName)
    {
        if (el.TryGetProperty(propName, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            return prop.GetString();
        }
        return null;
    }

    private static double? GetDouble(JsonElement el, string propName)
    {
        if (el.TryGetProperty(propName, out var prop) && prop.ValueKind == JsonValueKind.Number)
        {
            return prop.GetDouble();
        }
        return null;
    }

    private static string? ExtractGbifImage(JsonElement item)
    {
        if (item.TryGetProperty("media", out var media) && media.ValueKind == JsonValueKind.Array)
        {
            foreach (var m in media.EnumerateArray())
            {
                var identifier = GetString(m, "identifier");
                if (!string.IsNullOrWhiteSpace(identifier) && identifier.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    return identifier;
                }
            }
        }
        return null;
    }

    private static string? ExtractInaturalistImage(JsonElement obs)
    {
        if (obs.TryGetProperty("photos", out var photos) && photos.ValueKind == JsonValueKind.Array && photos.GetArrayLength() > 0)
        {
            var url = GetString(photos[0], "url");
            if (!string.IsNullOrWhiteSpace(url))
            {
                return url.Replace("square", "medium");
            }
        }
        return null;
    }

    private static string GetFallbackImageForQuery(string query)
    {
        string safeLabel = Uri.EscapeDataString(string.IsNullOrWhiteSpace(query) ? "Specimen" : query);
        return $"data:image/svg+xml;utf8,<svg xmlns='http://www.w3.org/2000/svg' width='600' height='420' viewBox='0 0 600 420'><rect width='600' height='420' fill='%23162D20'/><text x='300' y='205' text-anchor='middle' fill='%23F5F1E6' font-family='serif' font-weight='600' font-size='22'>{safeLabel}</text><text x='300' y='238' text-anchor='middle' fill='%238FA898' font-family='sans-serif' font-size='13'>BIOMA API</text></svg>";
    }

    private static List<ExternalObservationDto> BuildFallbackObservations(ResolvedTaxonInfo taxon, string source, int count)
    {
        var rarity = GachaService.ComputeRarityFromOccurrences(taxon.GlobalObservationsCount);
        var (atk, def, spd, _) = GachaService.ComputeStatsFromRarity(rarity, taxon.ScientificName);
        var img = taxon.DefaultPhotoUrl ?? GetFallbackImageForQuery(taxon.ScientificName);
        var list = new List<ExternalObservationDto>();
        for (int i = 1; i <= count; i++)
        {
            list.Add(new ExternalObservationDto(
                $"{source.ToLowerInvariant()}-demo-{i}",
                source,
                taxon.ScientificName,
                taxon.CommonNameFr,
                taxon.Family,
                "Animalia",
                45.75 + i * 0.4,
                4.85 + i * 0.3,
                source == "GBIF" ? "Europe (Occurrence GBIF)" : "France (Observation iNaturalist)",
                DateTime.UtcNow.AddDays(-i * 3).ToString("yyyy-MM-dd"),
                img,
                taxon.GlobalObservationsCount,
                rarity,
                GachaService.RarityLabel(rarity),
                GachaService.DropRateForRarity(rarity),
                atk,
                def,
                spd
            ));
        }
        return list;
    }
}
