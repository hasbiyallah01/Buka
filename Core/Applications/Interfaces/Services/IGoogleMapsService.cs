using AmalaSpotLocator.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AmalaSpotLocator.Interfaces;

public interface IGoogleMapsService
{

    Task<Location?> GeocodeAddressAsync(string address);

    Task<string?> ReverseGeocodeAsync(Location location);

    Task<IEnumerable<PlaceCandidate>> SearchPlacesAsync(Location center, double radiusMeters, string query = "amala restaurant");

    Task<PlaceDetails?> GetPlaceDetailsAsync(string placeId);

    Task<RouteInfo?> GetDirectionsAsync(Location origin, Location destination, TravelMode travelMode = TravelMode.Driving);

    string GenerateStaticMapUrl(Location center, IEnumerable<AmalaSpot> spots, int width = 600, int height = 400, int zoom = 14);

    string GenerateStaticMapUrl(Location location, int width = 600, int height = 400, int zoom = 14, string? label = null);

    Task<bool> ValidatePlaceIdAsync(string placeId);

    Task<IEnumerable<PlaceCandidate>> FindNearbyPlacesAsync(Location center, double radiusMeters, string placeType = "restaurant");
}

public enum TravelMode
{
    Driving,
    Walking,
    Transit,
    Bicycling
}

public class RouteInfo
{
    public string Duration { get; set; } = string.Empty;
    public string Distance { get; set; } = string.Empty;
    public int DurationInSeconds { get; set; }
    public int DistanceInMeters { get; set; }
    public List<Location> RoutePoints { get; set; } = new();
    public string EncodedPolyline { get; set; } = string.Empty;
}