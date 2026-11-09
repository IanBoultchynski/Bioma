using System.Text.Json;
using Bioma.Application.DTOs;
using Bioma.Application.Interfaces;
using Bioma.Domain.Entities;
using Bioma.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Bioma.Infrastructure.Services;

/// <summary>
/// Début du Back-Office C# :
/// - Connexions HTTP simples aux APIs publiques GBIF et iNaturalist
/// - Sauvegarde et lecture des observations en base de données EF Core
/// </summary>
public class BiomaGameService : IBiomaGameService
{
    private const string GbifOccurrenceSearchUrl = "https://api.gbif.org/v1/occurrence/search";
    private const string InaturalistObservationsUrl = "https://api.inaturalist.org/v1/observations";

    private readonly BiomaDbContext _db;
    private readonly HttpClient _httpClient;

    public BiomaGameService(BiomaDbContext db, HttpClient httpClient)
    {
        _db = db;
        _httpClient = httpClient;
    }

    public async Task<ApiSearchResultDto> SearchApisAsync(string query, int limitPerSource = 4, CancellationToken ct = default)
    {
        var cleanQuery = string.IsNullOrWhiteSpace(query) ? "Vulpes vulpes" : query.Trim();

        var gbifTask = FetchFromGbifInternalAsync(cleanQuery, limitPerSource, ct);
        var inatTask = FetchFromInaturalistInternalAsync(cleanQuery, limitPerSource, ct);

        await Task.WhenAll(gbifTask, inatTask);

        var (gbifList, gbifLive) = gbifTask.Result;
        var (inatList, inatLive) = inatTask.Result;

        var combined = new List<ExternalObservationDto>();
        combined.AddRange(gbifList);
        combined.AddRange(inatList);

        string status = (gbifLive || inatLive)
            ? $"Connexion directe réussie ({gbifList.Count} résultats GBIF, {inatList.Count} résultats iNaturalist)."
            : $"Mode hors-ligne actif : affichage des exemples locaux GBIF ({gbifList.Count}) & iNaturalist ({inatList.Count}).";

        return new ApiSearchResultDto(
            cleanQuery,
            gbifLive,
            inatLive,
            gbifList.Count,
            inatList.Count,
            status,
            combined
        );
    }

    public async Task<List<ExternalObservationDto>> FetchFromGbifAsync(string query, int limit = 4, CancellationToken ct = default)
    {
        var (results, _) = await FetchFromGbifInternalAsync(query, limit, ct);
        return results;
    }

    public async Task<List<ExternalObservationDto>> FetchFromInaturalistAsync(string query, int limit = 4, CancellationToken ct = default)
    {
        var (results, _) = await FetchFromInaturalistInternalAsync(query, limit, ct);
        return results;
    }

    private async Task<(List<ExternalObservationDto> Items, bool IsLive)> FetchFromGbifInternalAsync(string query, int limit, CancellationToken ct)
    {
        var items = new List<ExternalObservationDto>();
        try
        {
            var url = $"{GbifOccurrenceSearchUrl}?q={Uri.EscapeDataString(query)}&hasCoordinate=true&limit={Math.Clamp(limit, 1, 20)}";
            using var response = await _httpClient.GetAsync(url, ct);

            if (response.IsSuccessStatusCode)
            {
                using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
                if (doc.RootElement.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in results.EnumerateArray())
                    {
                        string key = item.TryGetProperty("key", out var k) ? k.ToString() : Guid.NewGuid().ToString("N")[..8];
                        string sciName = GetString(item, "species") ?? GetString(item, "scientificName") ?? query;
                        string common = GetString(item, "vernacularName") ?? sciName;
                        string family = GetString(item, "family") ?? "Animalia";
                        string kingdom = GetString(item, "kingdom") ?? "Animalia";
                        double? lat = GetDouble(item, "decimalLatitude");
                        double? lon = GetDouble(item, "decimalLongitude");
                        string country = GetString(item, "country") ?? "International";
                        string date = (GetString(item, "eventDate") ?? DateTime.UtcNow.ToString("yyyy-MM-dd")).Split('T')[0];
                        string img = ExtractGbifImage(item) ?? GetFallbackImageForQuery(sciName);

                        items.Add(new ExternalObservationDto(
                            $"gbif-{key}",
                            "GBIF",
                            sciName,
                            common,
                            family,
                            kingdom,
                            lat,
                            lon,
                            country,
                            date,
                            img
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
            // En cas d'absence de réseau dans le bac à sable local, on retourne un jeu d'essai GBIF réaliste
        }

        items.AddRange(BuildFallbackObservations(query, "GBIF", Math.Min(limit, 2)));
        return (items, false);
    }

    private async Task<(List<ExternalObservationDto> Items, bool IsLive)> FetchFromInaturalistInternalAsync(string query, int limit, CancellationToken ct)
    {
        var items = new List<ExternalObservationDto>();
        try
        {
            var url = $"{InaturalistObservationsUrl}?q={Uri.EscapeDataString(query)}&quality_grade=research&photos=true&per_page={Math.Clamp(limit, 1, 20)}";
            using var response = await _httpClient.GetAsync(url, ct);

            if (response.IsSuccessStatusCode)
            {
                using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
                if (doc.RootElement.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
                {
                    foreach (var obs in results.EnumerateArray())
                    {
                        string id = obs.TryGetProperty("id", out var idProp) ? idProp.ToString() : Guid.NewGuid().ToString("N")[..8];
                        string sciName = query;
                        string common = query;
                        string family = "Animalia";
                        string kingdom = "Animalia";

                        if (obs.TryGetProperty("taxon", out var taxon) && taxon.ValueKind == JsonValueKind.Object)
                        {
                            sciName = GetString(taxon, "name") ?? query;
                            common = GetString(taxon, "preferred_common_name") ?? sciName;
                            family = GetString(taxon, "iconic_taxon_name") ?? "Animalia";
                        }

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

                        string place = GetString(obs, "place_guess") ?? "Terrain iNaturalist";
                        string date = GetString(obs, "observed_on") ?? DateTime.UtcNow.ToString("yyyy-MM-dd");
                        string img = ExtractInaturalistImage(obs) ?? GetFallbackImageForQuery(sciName);

                        items.Add(new ExternalObservationDto(
                            $"inat-{id}",
                            "iNaturalist",
                            sciName,
                            common,
                            family,
                            kingdom,
                            lat,
                            lon,
                            place,
                            date,
                            img
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
            // En cas d'absence de réseau dans le bac à sable local, on retourne un jeu d'essai iNaturalist réaliste
        }

        items.AddRange(BuildFallbackObservations(query, "iNaturalist", Math.Min(limit, 2)));
        return (items, false);
    }

    public async Task<List<CardDto>> GetSavedCardsAsync()
    {
        var cards = await _db.Cards
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        return cards.Select(MapToCardDto).ToList();
    }

    public async Task<CardDto> SaveObservationToBackOfficeAsync(ExternalObservationDto observation)
    {
        var entity = new SpeciesCard
        {
            CommonName = string.IsNullOrWhiteSpace(observation.CommonName) ? observation.ScientificName : observation.CommonName,
            ScientificName = observation.ScientificName,
            Family = observation.Family,
            Kingdom = observation.Kingdom,
            SourceApi = observation.SourceApi,
            ExternalId = observation.ExternalId,
            ImageUrl = observation.ImageUrl,
            Latitude = observation.Latitude,
            Longitude = observation.Longitude,
            CountryOrPlace = observation.CountryOrPlace,
            ObservedDate = observation.ObservedDate,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.Cards.Add(entity);
        await _db.SaveChangesAsync();

        return MapToCardDto(entity);
    }

    public async Task<bool> DeleteCardAsync(Guid cardId)
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

    private static CardDto MapToCardDto(SpeciesCard card)
    {
        string coords = card.Latitude.HasValue && card.Longitude.HasValue
            ? $"{card.Latitude.Value:F2}°, {card.Longitude.Value:F2}°"
            : "GPS non renseigné";

        return new CardDto(
            card.Id,
            card.CommonName,
            card.ScientificName,
            card.Family,
            card.SourceApi,
            card.ImageUrl,
            coords,
            card.CountryOrPlace,
            card.ObservedDate
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
        var q = query.ToLowerInvariant();
        if (q.Contains("lynx"))
            return "https://images.unsplash.com/photo-1546182990-dffeafbe841d?auto=format&fit=crop&w=600&q=80";
        if (q.Contains("alcedo") || q.Contains("martin"))
            return "https://images.unsplash.com/photo-1444464666168-49d633b86797?auto=format&fit=crop&w=600&q=80";
        if (q.Contains("ursus") || q.Contains("ours"))
            return "https://images.unsplash.com/photo-1530595467537-0b5996c41f2d?auto=format&fit=crop&w=600&q=80";
        return "https://images.unsplash.com/photo-1474511320723-9a56873867b5?auto=format&fit=crop&w=600&q=80";
    }

    private static List<ExternalObservationDto> BuildFallbackObservations(string query, string source, int count)
    {
        var img = GetFallbackImageForQuery(query);
        var list = new List<ExternalObservationDto>();
        for (int i = 1; i <= count; i++)
        {
            list.Add(new ExternalObservationDto(
                $"{source.ToLowerInvariant()}-demo-{i}",
                source,
                query,
                $"Spécimen {query} ({source} #{i})",
                "Animalia",
                "Animalia",
                45.75 + i * 0.4,
                4.85 + i * 0.3,
                source == "GBIF" ? "Europe (Occurrence GBIF)" : "France (Observation iNaturalist)",
                DateTime.UtcNow.AddDays(-i * 3).ToString("yyyy-MM-dd"),
                img
            ));
        }
        return list;
    }
}
