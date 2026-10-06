using System.Globalization;
using System.Text.Json;
using AggStudentDiscounts.Domain.Entities;
using AggStudentDiscounts.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AggStudentDiscounts.Api.Services;

public class GeocodingOptions
{
    public const string SectionName = "Geocoding";

    public string BaseUrl { get; set; } = "https://nominatim.openstreetmap.org/";
    public string UserAgent { get; set; } = "AggStudentDiscounts/1.0";
    /// <summary>left,top,right,bottom — по умолчанию г. Москва.</summary>
    public string ViewBox { get; set; } = "36.80,56.00,38.00,55.10";
    public int CacheHours { get; set; } = 24;
    public int Limit { get; set; } = 5;
}

public record GeoPlace(
    string ExternalId,
    string Name,
    string Address,
    double Latitude,
    double Longitude,
    string? Category,
    string? Phone,
    string? Website,
    string? WorkingHours);

public class GeocodingUnavailableException(string message, Exception? inner = null) : Exception(message, inner);

public interface IGeocodingService
{
    Task<IReadOnlyList<GeoPlace>> SuggestAsync(string query, CancellationToken ct = default);
    Task<GeoPlace?> ReverseAsync(double lat, double lng, CancellationToken ct = default);
    Task<GeoPlace?> LookupAsync(string externalId, CancellationToken ct = default);
}

/// <summary>
/// Геокодинг через Nominatim (OpenStreetMap). Все запросы идут с сервера; ответы кэшируются в БД.
/// При недоступности сервиса отдаются кэшированные данные, даже просроченные.
/// </summary>
public class NominatimGeocodingService(
    HttpClient http,
    ApplicationDbContext db,
    IOptions<GeocodingOptions> options,
    ILogger<NominatimGeocodingService> logger) : IGeocodingService
{
    private static readonly HashSet<string> FoodTypes =
        ["cafe", "restaurant", "fast_food", "bar", "pub", "food_court", "ice_cream", "biergarten", "bakery", "coffee_shop"];

    private readonly GeocodingOptions _options = options.Value;

    public Task<IReadOnlyList<GeoPlace>> SuggestAsync(string query, CancellationToken ct = default)
    {
        var q = Uri.EscapeDataString(query.Trim());
        var url = $"search?q={q}&format=jsonv2&addressdetails=1&extratags=1&limit={_options.Limit}" +
                  $"&viewbox={_options.ViewBox}&bounded=1&accept-language=ru";
        return GetCachedAsync($"suggest:{query.Trim().ToLowerInvariant()}", url, false, ct);
    }

    public async Task<GeoPlace?> ReverseAsync(double lat, double lng, CancellationToken ct = default)
    {
        var latS = lat.ToString("F5", CultureInfo.InvariantCulture);
        var lngS = lng.ToString("F5", CultureInfo.InvariantCulture);
        var url = $"reverse?lat={latS}&lon={lngS}&format=jsonv2&addressdetails=1&extratags=1&accept-language=ru";
        var result = await GetCachedAsync($"reverse:{latS},{lngS}", url, true, ct);
        return result.FirstOrDefault();
    }

    public async Task<GeoPlace?> LookupAsync(string externalId, CancellationToken ct = default)
    {
        var url = $"lookup?osm_ids={Uri.EscapeDataString(externalId)}&format=jsonv2&addressdetails=1&extratags=1&accept-language=ru";
        var result = await GetCachedAsync($"lookup:{externalId}", url, false, ct);
        return result.FirstOrDefault();
    }

    private async Task<IReadOnlyList<GeoPlace>> GetCachedAsync(string key, string url, bool single, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var cached = await db.GeocodeCache.FirstOrDefaultAsync(c => c.Key == key, ct);
        if (cached != null && cached.ExpiresAt > now)
        {
            return Deserialize(cached.ResponseJson);
        }

        try
        {
            using var response = await http.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(ct);
            var places = Parse(json, single);

            if (cached == null)
            {
                cached = new GeocodeCacheEntry { Key = key };
                db.GeocodeCache.Add(cached);
            }

            cached.ResponseJson = JsonSerializer.Serialize(places);
            cached.CreatedAt = now;
            cached.ExpiresAt = now.AddHours(_options.CacheHours);
            await db.SaveChangesAsync(ct);
            return places;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "Geocoding service unavailable for key {Key}", key);

            if (cached != null)
            {
                return Deserialize(cached.ResponseJson);
            }

            throw new GeocodingUnavailableException("Геоинформационный сервис временно недоступен.", ex);
        }
    }

    private static IReadOnlyList<GeoPlace> Deserialize(string json) =>
        JsonSerializer.Deserialize<List<GeoPlace>>(json) ?? [];

    internal static List<GeoPlace> Parse(string json, bool single)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        List<JsonElement> items;
        if (root.ValueKind == JsonValueKind.Array)
        {
            items = root.EnumerateArray().ToList();
        }
        else if (single && root.ValueKind == JsonValueKind.Object && !root.TryGetProperty("error", out _))
        {
            items = [root];
        }
        else
        {
            items = [];
        }

        return items.Select(ToPlace).OfType<GeoPlace>().ToList();
    }

    private static GeoPlace? ToPlace(JsonElement e)
    {
        if (!e.TryGetProperty("lat", out var latEl) || !e.TryGetProperty("lon", out var lonEl) ||
            !double.TryParse(latEl.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) ||
            !double.TryParse(lonEl.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var lon))
        {
            return null;
        }

        var osmType = Str(e, "osm_type");
        var osmId = e.TryGetProperty("osm_id", out var idEl) ? idEl.ToString() : null;
        var externalId = osmType is { Length: > 0 } && osmId != null
            ? $"{char.ToUpperInvariant(osmType[0])}{osmId}"
            : string.Empty;

        var type = Str(e, "type");
        var extra = e.TryGetProperty("extratags", out var ex) && ex.ValueKind == JsonValueKind.Object ? ex : default;

        string? Extra(params string[] keys)
        {
            if (extra.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            return keys
                .Select(k => extra.TryGetProperty(k, out var v) ? v.GetString() : null)
                .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
        }

        var display = Str(e, "display_name") ?? string.Empty;
        var name = Str(e, "name");
        if (string.IsNullOrWhiteSpace(name))
        {
            name = display.Split(',')[0].Trim();
        }

        return new GeoPlace(
            externalId,
            name,
            display,
            lat,
            lon,
            type is null ? null : FoodTypes.Contains(type) ? "Общепит" : type,
            Extra("phone", "contact:phone"),
            Extra("website", "contact:website"),
            Extra("opening_hours"));
    }

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
