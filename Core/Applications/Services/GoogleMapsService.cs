using AmalaSpotLocator.Configuration;
using AmalaSpotLocator.Interfaces;
using Microsoft.Extensions.Options;
using System.Text.Json;
using AmalaSpotLocator.Models;
using AmalaSpotLocator.Infrastructure;
using Microsoft.EntityFrameworkCore;
using GoogleApi.Entities.Places.Search.Find.Response;
using Microsoft.Extensions.Logging;

namespace AmalaSpotLocator.Core.Applications.Services;

public class GoogleMapsService : IGoogleMapsService
{
    private readonly GoogleMapsSettings _settings;
    private readonly ILogger<GoogleMapsService> _logger;
    private readonly HttpClient _httpClient;
    private readonly AmalaSpotContext _context;

    private const string GeocodingBaseUrl = "https://maps.googleapis.com/maps/api/geocode/json";
    private const string PlacesSearchBaseUrl = "https://maps.googleapis.com/maps/api/place/textsearch/json";
    private const string PlacesNearbyBaseUrl = "https://maps.googleapis.com/maps/api/place/nearbysearch/json";
    private const string PlacesDetailsBaseUrl = "https://maps.googleapis.com/maps/api/place/details/json";
    private const string DirectionsBaseUrl = "https://maps.googleapis.com/maps/api/directions/json";
    private const string StaticMapBaseUrl = "https://maps.googleapis.com/maps/api/staticmap";
    public GoogleMapsService( IOptions<GoogleMapsSettings> settings,ILogger<GoogleMapsService> logger,HttpClient httpClient,AmalaSpotContext context)
    {
        _settings = settings.Value ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _context = context;
    }

    public async Task<Location?> GeocodeAddressAsync(string address)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(address))
                return null;

            var url = $"{GeocodingBaseUrl}?address={Uri.EscapeDataString(address)}&key={_settings.ApiKey}";
            var response = await _httpClient.GetStringAsync(url);
            
            using var document = JsonDocument.Parse(response);
            var root = document.RootElement;
            
            if (root.GetProperty("status").GetString() == "OK" && 
                root.GetProperty("results").GetArrayLength() > 0)
            {
                var result = root.GetProperty("results")[0];
                var location = result.GetProperty("geometry").GetProperty("location");
                
                return new Location(
                    location.GetProperty("lat").GetDouble(),
                    location.GetProperty("lng").GetDouble()
                );
            }

            _logger.LogWarning("Geocoding failed for address: {Address}. Status: {Status}", 
                address, root.GetProperty("status").GetString());
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error geocoding address: {Address}", address);
            return null;
        }
    }

    public async Task<string?> ReverseGeocodeAsync(Location location)
    {
        try
        {
            var url = $"{GeocodingBaseUrl}?latlng={location.Latitude},{location.Longitude}&key={_settings.ApiKey}";
            var response = await _httpClient.GetStringAsync(url);
            
            using var document = JsonDocument.Parse(response);
            var root = document.RootElement;
            
            if (root.GetProperty("status").GetString() == "OK" && 
                root.GetProperty("results").GetArrayLength() > 0)
            {
                return root.GetProperty("results")[0].GetProperty("formatted_address").GetString();
            }

            _logger.LogWarning("Reverse geocoding failed for location: {Location}. Status: {Status}", 
                location, root.GetProperty("status").GetString());
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reverse geocoding location: {Location}", location);
            return null;
        }
    }

    public static AmalaSpot ToAmalaSpot(PlaceCandidate candidate)
    {
        return new AmalaSpot
        {
            Name = candidate.Name,
            Address = candidate.Address,
            Location = new NetTopologySuite.Geometries.Point(candidate.Location.Longitude, candidate.Location.Latitude)
            {
                SRID = 4326
            },
            AverageRating = candidate.Rating ?? 0,
            ReviewCount = candidate.UserRatingsTotal ?? 0,
            PriceRange = candidate.PriceLevel ?? PriceRange.Budget,
            Specialties = candidate.Types ?? new List<string>(),
            IsVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Id = Guid.NewGuid()
        };
    }


    public async Task<IEnumerable<PlaceCandidate>> SearchPlacesAsync(Location center, double radiusMeters, string query = "amala restaurant")
    {
        try
        {
            var url = $"{PlacesSearchBaseUrl}?query={Uri.EscapeDataString(query)}" +
                      $"&locationbias=circle:{radiusMeters:F0}@{center.Latitude},{center.Longitude}" +
                      $"&key={_settings.PlacesApiKey}";

            _logger.LogDebug("Making Google Places API request to: {Url}", url.Replace(_settings.PlacesApiKey, "***"));

            var response = await _httpClient.GetStringAsync(url);
            _logger.LogDebug("Google Places API response length: {Length} characters", response.Length);

            using var document = JsonDocument.Parse(response);
            var root = document.RootElement;

            var status = root.GetProperty("status").GetString();
            _logger.LogDebug("Google Places API status: {Status}", status);

            if (status == "OK")
            {
                var results = new List<PlaceCandidate>();
                var resultsArray = root.GetProperty("results");
                _logger.LogDebug("Google Places API returned {Count} results", resultsArray.GetArrayLength());

                foreach (var result in resultsArray.EnumerateArray())
                {
                    var candidate = MapJsonToPlaceCandidate(result);
                    results.Add(candidate);
                    _logger.LogDebug("Found place: {Name} at {Address}", candidate.Name, candidate.Address);
                }

                if (results.Count > 0)
                {
                    var existingData = await _context.AmalaSpots
                        .AsNoTracking()
                        .Select(s => new { s.Name, s.Address })
                        .ToListAsync();

                    var existingNames = existingData
                        .Select(x => x.Name)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    var newResults = results
                        .Where(r => !existingNames.Contains(r.Name))
                        .ToList();

                    if (newResults.Any())
                    {
                        var amalaSpots = new List<AmalaSpot>();

                        foreach (var candidate in newResults)
                        {
                            var spot = ToAmalaSpot(candidate);

                            var details = await GetPlaceDetailsAsync(candidate.PlaceId);

                            if (details != null)
                            {
                                spot.PhoneNumber = details.FormattedPhoneNumber ?? null;

                                var todayHours = details.OpeningHours?.FirstOrDefault();
                                spot.OpeningTime = todayHours?.OpenTime ?? new TimeSpan(10, 0, 0);
                                spot.ClosingTime = todayHours?.CloseTime ?? new TimeSpan(17, 0, 0);
                            }
                            else
                            {
                                spot.PhoneNumber = "N/A";
                                spot.OpeningTime = new TimeSpan(10, 0, 0);
                                spot.ClosingTime = new TimeSpan(17, 0, 0);
                            }

                            amalaSpots.Add(spot);
                        }

                        try
                        {
                            await _context.AmalaSpots.AddRangeAsync(amalaSpots);
                            await _context.SaveChangesAsync();
                            _logger.LogInformation("Saved {Count} new AmalaSpots to the database", amalaSpots.Count);
                        }
                        catch (DbUpdateException dbEx)
                        {
                            _logger.LogError(dbEx, "Failed to save AmalaSpots to database.");
                        }
                    }
                    else
                    {
                        _logger.LogInformation("No new places to save — all results already exist in the database.");
                    }
                }


                return results;
            }

            _logger.LogWarning("Places search failed. Status: {Status}", status);
            if (root.TryGetProperty("error_message", out var errorMessage))
            {
                _logger.LogWarning("Google Places API error message: {ErrorMessage}", errorMessage.GetString());
            }

            return Enumerable.Empty<PlaceCandidate>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching places with query: {Query}", query);
            return Enumerable.Empty<PlaceCandidate>();
        }
    }


    public async Task<IEnumerable<PlaceCandidate>> SearchPlacesWithMultipleQueriesAsync(Location center, double radiusMeters, IEnumerable<string> queries)
    {
        var allResults = new List<PlaceCandidate>();
        var seenPlaceIds = new HashSet<string>();

        foreach (var query in queries)
        {
            try
            {
                var results = await SearchPlacesAsync(center, radiusMeters, query);
                
                foreach (var result in results)
                {
                    if (!seenPlaceIds.Contains(result.PlaceId))
                    {
                        seenPlaceIds.Add(result.PlaceId);
                        allResults.Add(result);
                    }
                }
                await Task.Delay(100);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to search with query: {Query}", query);
            }
        }

        return allResults;
    }

    public async Task<PlaceDetails?> GetPlaceDetailsAsync(string placeId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(placeId))
                return null;

            var fields = "place_id,name,formatted_address,geometry,formatted_phone_number,website,rating,user_ratings_total,price_level,opening_hours,reviews,photos,types";
            var url = $"{PlacesDetailsBaseUrl}?place_id={placeId}&fields={fields}&key={_settings.PlacesApiKey}";
            
            _logger.LogDebug("Getting place details for PlaceId: {PlaceId}", placeId);
            
            var response = await _httpClient.GetStringAsync(url);
            
            using var document = JsonDocument.Parse(response);
            var root = document.RootElement;
            
            var status = root.GetProperty("status").GetString();
            _logger.LogDebug("Place details API status for {PlaceId}: {Status}", placeId, status);
            
            if (status == "OK" && root.TryGetProperty("result", out var result))
            {
                var details = MapJsonToPlaceDetails(result);
                _logger.LogDebug("Successfully retrieved details for place: {Name}", details.Name);
                return details;
            }

            _logger.LogWarning("Place details request failed for PlaceId: {PlaceId}. Status: {Status}", placeId, status);
            if (root.TryGetProperty("error_message", out var errorMessage))
            {
                _logger.LogWarning("Google Places Details API error message: {ErrorMessage}", errorMessage.GetString());
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting place details for PlaceId: {PlaceId}", placeId);
            return null;
        }
    }

    public async Task<RouteInfo?> GetDirectionsAsync(Location origin, Location destination, TravelMode travelMode = TravelMode.Driving)
    {
        try
        {
            var mode = travelMode.ToString().ToLower();
            var url = $"{DirectionsBaseUrl}?origin={origin.Latitude},{origin.Longitude}" +
                     $"&destination={destination.Latitude},{destination.Longitude}" +
                     $"&mode={mode}&key={_settings.ApiKey}";
            
            var response = await _httpClient.GetStringAsync(url);
            
            using var document = JsonDocument.Parse(response);
            var root = document.RootElement;
            
            if (root.GetProperty("status").GetString() == "OK" && 
                root.GetProperty("routes").GetArrayLength() > 0)
            {
                var route = root.GetProperty("routes")[0];
                var leg = route.GetProperty("legs")[0];
                var polyline = route.GetProperty("overview_polyline").GetProperty("points").GetString() ?? "";

                return new RouteInfo
                {
                    Duration = leg.GetProperty("duration").GetProperty("text").GetString() ?? "",
                    Distance = leg.GetProperty("distance").GetProperty("text").GetString() ?? "",
                    DurationInSeconds = leg.GetProperty("duration").GetProperty("value").GetInt32(),
                    DistanceInMeters = leg.GetProperty("distance").GetProperty("value").GetInt32(),
                    EncodedPolyline = polyline,
                    RoutePoints = DecodePolyline(polyline)
                };
            }

            _logger.LogWarning("Directions request failed. Status: {Status}", root.GetProperty("status").GetString());
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting directions from {Origin} to {Destination}", origin, destination);
            return null;
        }
    }

    public string GenerateStaticMapUrl(Location center, IEnumerable<AmalaSpot> spots, int width = 600, int height = 400, int zoom = 14)
    {
        var markers = new List<string>();

        markers.Add($"color:red|label:C|{center.Latitude},{center.Longitude}");

        var spotList = spots.Take(30).ToList(); 
        for (int i = 0; i < spotList.Count; i++)
        {
            var spot = spotList[i];
            var label = (i + 1).ToString();
            markers.Add($"color:blue|label:{label}|{spot.Location.Y},{spot.Location.X}");
        }

        var markersParam = string.Join("&markers=", markers);

        return $"https://maps.googleapis.com/maps/api/staticmap?" +
                $"center={center.Latitude},{center.Longitude}" +
                $"&zoom={zoom}" +
                $"&size={width}x{height}" +
                $"&markers={markersParam}" +
                $"&key={_settings.ApiKey}";
    }

    public string GenerateStaticMapUrl(Location location, int width = 600, int height = 400, int zoom = 14, string? label = null)
    {
        var markerLabel = string.IsNullOrEmpty(label) ? "A" : label;

        return $"https://maps.googleapis.com/maps/api/staticmap?" +
               $"center={location.Latitude},{location.Longitude}" +
               $"&zoom={zoom}" +
               $"&size={width}x{height}" +
               $"&markers=color:red|label:{markerLabel}|{location.Latitude},{location.Longitude}" +
               $"&key={_settings.ApiKey}";
    }

    public async Task<bool> ValidatePlaceIdAsync(string placeId)
    {
        try
        {
            var details = await GetPlaceDetailsAsync(placeId);
            return details != null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating PlaceId: {PlaceId}", placeId);
            return false;
        }
    }

    public async Task<IEnumerable<PlaceCandidate>> FindNearbyPlacesAsync(Location center, double radiusMeters, string placeType = "restaurant")
    {
        try
        {
            var url = $"{PlacesNearbyBaseUrl}?location={center.Latitude},{center.Longitude}" +
                     $"&radius={radiusMeters:F0}&type={placeType}&key={_settings.PlacesApiKey}";
            
            var response = await _httpClient.GetStringAsync(url);
            
            using var document = JsonDocument.Parse(response);
            var root = document.RootElement;
            
            if (root.GetProperty("status").GetString() == "OK")
            {
                var results = new List<PlaceCandidate>();
                foreach (var result in root.GetProperty("results").EnumerateArray())
                {
                    results.Add(MapJsonToPlaceCandidate(result));
                }
                return results;
            }

            _logger.LogWarning("Nearby places search failed. Status: {Status}", root.GetProperty("status").GetString());
            return Enumerable.Empty<PlaceCandidate>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding nearby places of type: {PlaceType}", placeType);
            return Enumerable.Empty<PlaceCandidate>();
        }
    }

    public static PlaceCandidate MapJsonToPlaceCandidate(JsonElement result)
    {
        var geometry = result.GetProperty("geometry").GetProperty("location");
        
        return new PlaceCandidate
        {
            PlaceId = result.GetProperty("place_id").GetString() ?? "",
            Name = result.GetProperty("name").GetString() ?? "",
            Address = result.TryGetProperty("formatted_address", out var addr) ? addr.GetString() ?? "" :
                     result.TryGetProperty("vicinity", out var vic) ? vic.GetString() ?? "" : "",
            Location = new Location(geometry.GetProperty("lat").GetDouble(), geometry.GetProperty("lng").GetDouble()),
            Rating = result.TryGetProperty("rating", out var rating) ? (decimal?)rating.GetDouble() : null,
            UserRatingsTotal = result.TryGetProperty("user_ratings_total", out var total) ? total.GetInt32() : null,
            PriceLevel = result.TryGetProperty("price_level", out var price) ? MapPriceLevel(price.GetInt32()) : null,
            Types = result.TryGetProperty("types", out var types) ? 
                types.EnumerateArray().Select(t => t.GetString() ?? "").ToList() : new List<string>(),
            IsOpenNow = result.TryGetProperty("opening_hours", out var hours) && 
                       hours.TryGetProperty("open_now", out var open) ? open.GetBoolean() : false,
            PhotoReference = result.TryGetProperty("photos", out var photos) && photos.GetArrayLength() > 0 ?
                           photos[0].GetProperty("photo_reference").GetString() : null
        };
    }

    private PlaceDetails MapJsonToPlaceDetails(JsonElement result)
    {
        var geometry = result.GetProperty("geometry").GetProperty("location");
        
        var placeDetails = new PlaceDetails
        {
            PlaceId = result.GetProperty("place_id").GetString() ?? "",
            Name = result.GetProperty("name").GetString() ?? "",
            FormattedAddress = result.TryGetProperty("formatted_address", out var addr) ? addr.GetString() ?? "" : "",
            Location = new Location(geometry.GetProperty("lat").GetDouble(), geometry.GetProperty("lng").GetDouble()),
            FormattedPhoneNumber = result.TryGetProperty("formatted_phone_number", out var phone) ? phone.GetString() : null,
            Website = result.TryGetProperty("website", out var website) ? website.GetString() : null,
            Rating = result.TryGetProperty("rating", out var rating) ? (decimal?)rating.GetDouble() : null,
            UserRatingsTotal = result.TryGetProperty("user_ratings_total", out var total) ? total.GetInt32() : null,
            PriceLevel = result.TryGetProperty("price_level", out var price) ? MapPriceLevel(price.GetInt32()) : null,
            Types = result.TryGetProperty("types", out var types) ? 
                types.EnumerateArray().Select(t => t.GetString() ?? "").ToList() : new List<string>(),
            Photos = result.TryGetProperty("photos", out var photos) ? 
                photos.EnumerateArray().Select(p => p.GetProperty("photo_reference").GetString() ?? "").ToList() : 
                new List<string>()
        };

        if (result.TryGetProperty("opening_hours", out var openingHours) && 
            openingHours.TryGetProperty("periods", out var periods))
        {
            placeDetails.OpeningHours = periods.EnumerateArray()
                .Where(p => p.TryGetProperty("open", out _))
                .Select(p => new PlaceOpeningHours
                {
                    DayOfWeek = p.GetProperty("open").GetProperty("day").GetInt32(),
                    OpenTime = ParseTime(p.GetProperty("open").GetProperty("time").GetString() ?? "0000"),
                    CloseTime = p.TryGetProperty("close", out var close) ? 
                        ParseTime(close.GetProperty("time").GetString() ?? "2359") : 
                        TimeSpan.FromHours(23).Add(TimeSpan.FromMinutes(59))
                })
                .ToList();
        }

        if (result.TryGetProperty("reviews", out var reviews))
        {
            placeDetails.Reviews = reviews.EnumerateArray()
                .Select(r => new PlaceReview
                {
                    AuthorName = r.GetProperty("author_name").GetString() ?? "",
                    Rating = r.GetProperty("rating").GetInt32(),
                    Text = r.GetProperty("text").GetString() ?? "",
                    Time = DateTimeOffset.FromUnixTimeSeconds(r.GetProperty("time").GetInt64()).DateTime
                })
                .ToList();
        }

        return placeDetails;
    }

    private static PriceRange? MapPriceLevel(int? priceLevel)
    {
        return priceLevel switch
        {
            0 => PriceRange.Free,
            1 => PriceRange.Budget,
            2 => PriceRange.Moderate,
            3 => PriceRange.Expensive,
            4 => PriceRange.VeryExpensive,
            _ => null
        };
    }

    private TimeSpan ParseTime(string timeString)
    {
        if (timeString.Length == 4 && int.TryParse(timeString, out var time))
        {
            var hours = time / 100;
            var minutes = time % 100;
            return TimeSpan.FromHours(hours).Add(TimeSpan.FromMinutes(minutes));
        }
        return TimeSpan.Zero;
    }
    private List<Location> DecodePolyline(string encoded)
    {
        var points = new List<Location>();
        if (string.IsNullOrEmpty(encoded))
            return points;

        int index = 0;
        int lat = 0;
        int lng = 0;

        while (index < encoded.Length)
        {
            int shift = 0;
            int result = 0;
            int b;

            do
            {
                b = encoded[index++] - 63;
                result |= (b & 0x1f) << shift;
                shift += 5;
            } while (b >= 0x20);

            int dlat = (result & 1) != 0 ? ~(result >> 1) : result >> 1;
            lat += dlat;

            shift = 0;
            result = 0;

            do
            {
                b = encoded[index++] - 63;
                result |= (b & 0x1f) << shift;
                shift += 5;
            } while (b >= 0x20);

            int dlng = (result & 1) != 0 ? ~(result >> 1) : result >> 1;
            lng += dlng;

            points.Add(new Location(lat / 1E5, lng / 1E5));
        }

        return points;
    }
}