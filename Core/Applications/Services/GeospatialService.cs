using AmalaSpotLocator.Data;
using AmalaSpotLocator.Interfaces;
using System.Collections.Generic;
using AmalaSpotLocator.Models.MapModel;
using AmalaSpotLocator.Models.SpotModel;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using System;
using System.Threading.Tasks;
using Location = AmalaSpotLocator.Models.Location;
using AmalaSpotLocator.Models;
using Microsoft.Extensions.Configuration;

namespace AmalaSpotLocator.Core.Applications.Services;

public class GeospatialService : IGeospatialService
{
    private readonly AmalaSpotContext _context;
    private readonly IConfiguration _configuration;
    private readonly string _googleApiKey;

    private const double NigeriaMinLat = 4.0;
    private const double NigeriaMaxLat = 14.0;
    private const double NigeriaMinLng = 2.5;
    private const double NigeriaMaxLng = 15.0;

    public GeospatialService(AmalaSpotContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
        _googleApiKey = _configuration["GoogleMaps:ApiKey"] ?? 
            throw new InvalidOperationException("Google Maps API key not configured");
    }

    public async Task<Location?> GeocodeAddressAsync(string address)
    {
        try
        {

            await Task.Delay(10); // Simulate API call delay

            var normalizedAddress = address.ToLowerInvariant();
            
            if (normalizedAddress.Contains("lagos"))
                return new Location(6.5244, 3.3792);
            if (normalizedAddress.Contains("abuja"))
                return new Location(9.0765, 7.4951);
            if (normalizedAddress.Contains("ibadan"))
                return new Location(7.3775, 3.9470);
            if (normalizedAddress.Contains("kano"))
                return new Location(12.0022, 8.5920);
            if (normalizedAddress.Contains("port harcourt"))
                return new Location(4.8156, 7.0498);

            return null;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to geocode address: {address}", ex);
        }
    }

    public async Task<IEnumerable<AmalaSpot>> FindNearbySpots(Location center, double radiusKm, int limit = 50)
    {
        var centerPoint = LocationToPoint(center);
        var radiusMeters = radiusKm * 1000;

        var spots = await _context.AmalaSpots
            .Where(s => s.Location.Distance(centerPoint) <= radiusMeters)
            .OrderBy(s => s.Location.Distance(centerPoint))
            .Take(limit)
            .ToListAsync();

        return spots;
    }

    public async Task<IEnumerable<AmalaSpot>> FindSpotsInBounds(MapBounds bounds, SpotSearchCriteria? criteria = null)
    {
        var query = _context.AmalaSpots.AsNoTracking().AsQueryable();

        query = query.Where(s =>
            s.Location.Y >= bounds.SouthWestLat && s.Location.Y <= bounds.NorthEastLat &&
            s.Location.X >= bounds.SouthWestLng && s.Location.X <= bounds.NorthEastLng);

        if (criteria != null)
        {
            if (criteria.MinRating.HasValue)
                query = query.Where(s => s.AverageRating >= criteria.MinRating.Value);

            if (criteria.MinPriceRange.HasValue)
                query = query.Where(s => s.PriceRange >= criteria.MinPriceRange.Value);

            if (criteria.MaxPriceRange.HasValue)
                query = query.Where(s => s.PriceRange <= criteria.MaxPriceRange.Value);

            if (criteria.IsVerified.HasValue)
                query = query.Where(s => s.IsVerified == criteria.IsVerified.Value);
            if (criteria.CurrentTime.HasValue)
            {
                var currentTime = criteria.CurrentTime.Value;
                query = query.Where(s =>
                    s.OpeningTime.HasValue && s.ClosingTime.HasValue &&
                    (
                        (s.OpeningTime.Value <= s.ClosingTime.Value &&
                         currentTime >= s.OpeningTime.Value && currentTime <= s.ClosingTime.Value)
                        ||
                        (s.OpeningTime.Value > s.ClosingTime.Value &&
                         (currentTime >= s.OpeningTime.Value || currentTime <= s.ClosingTime.Value))
                    ));
            }
            List<AmalaSpot> spots = await query.ToListAsync();
            
            if (criteria.Specialties?.Any() == true)
            {
                spots = spots.Where(s => criteria.Specialties.Any(specialty => 
                    s.Specialties.Contains(specialty, StringComparer.OrdinalIgnoreCase))).ToList();
            }

            if (criteria.Limit > 0)
                spots = spots.Take(criteria.Limit).ToList();
                
            return spots;
        }

        return await query.ToListAsync();
    }


    public double CalculateDistance(Location point1, Location point2)
    {

        const double earthRadiusKm = 6371.0;

        var lat1Rad = DegreesToRadians(point1.Latitude);
        var lat2Rad = DegreesToRadians(point2.Latitude);
        var deltaLatRad = DegreesToRadians(point2.Latitude - point1.Latitude);
        var deltaLngRad = DegreesToRadians(point2.Longitude - point1.Longitude);

        var a = Math.Sin(deltaLatRad / 2) * Math.Sin(deltaLatRad / 2) +
                Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                Math.Sin(deltaLngRad / 2) * Math.Sin(deltaLngRad / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        
        return earthRadiusKm * c;
    }

    public double CalculateDistanceToSpot(Location location, AmalaSpot spot)
    {
        var spotLocation = PointToLocation(spot.Location);
        return CalculateDistance(location, spotLocation);
    }

    public Task<IEnumerable<SpotCluster>> ClusterSpots(IEnumerable<AmalaSpot> spots, double clusterRadiusKm = 0.5)
    {
        var clusters = new List<SpotCluster>();
        var unclusteredSpots = spots.ToList();

        while (unclusteredSpots.Any())
        {
            var currentSpot = unclusteredSpots.First();
            var currentLocation = PointToLocation(currentSpot.Location);
            var cluster = new SpotCluster(currentLocation);
            
            cluster.AddSpot(currentSpot);
            unclusteredSpots.Remove(currentSpot);

            var nearbySpots = unclusteredSpots
                .Where(s => CalculateDistanceToSpot(currentLocation, s) <= clusterRadiusKm)
                .ToList();

            foreach (var nearbySpot in nearbySpots)
            {
                cluster.AddSpot(nearbySpot);
                unclusteredSpots.Remove(nearbySpot);
            }

            clusters.Add(cluster);
        }

        return Task.FromResult<IEnumerable<SpotCluster>>(clusters);
    }

    public async Task<IEnumerable<SpotCluster>> ClusterSpotsForZoomLevel(IEnumerable<AmalaSpot> spots, int zoomLevel, MapBounds bounds)
    {
        var optimalRadius = CalculateOptimalClusterRadius(zoomLevel, bounds);
        return await ClusterSpots(spots, optimalRadius);
    }

    public double CalculateOptimalClusterRadius(int zoomLevel, MapBounds bounds)
    {

        var boundsDistance = bounds.GetDiagonalDistanceKm();

        var baseRadius = boundsDistance / (zoomLevel * 10);

        return Math.Max(0.1, Math.Min(5.0, baseRadius));
    }

    public int SuggestOptimalZoomLevel(MapBounds bounds, int spotCount)
    {
        var boundsDistance = bounds.GetDiagonalDistanceKm();
        var spotDensity = spotCount / Math.Max(1, boundsDistance);

        if (boundsDistance < 1) return 18; // Very small area
        if (boundsDistance < 5) return 15; // Neighborhood level
        if (boundsDistance < 20) return 13; // City district level
        if (boundsDistance < 50) return 11; // City level
        if (boundsDistance < 200) return 9; // Metropolitan area
        
        return 7; // State/region level
    }

    public bool IsLocationInNigeria(Location location)
    {
        return location.Latitude >= NigeriaMinLat && location.Latitude <= NigeriaMaxLat &&
               location.Longitude >= NigeriaMinLng && location.Longitude <= NigeriaMaxLng;
    }

    public async Task<IEnumerable<PlaceCandidate>> SearchPlacesAsync(Location center, double radiusKm, string query = "amala restaurant")
    {
        try
        {

            await Task.Delay(50); // Simulate API call delay

            var candidates = new List<PlaceCandidate>();

            if (IsLocationInNigeria(center))
            {
                var random = new Random(center.GetHashCode()); // Deterministic based on location
                
                for (int i = 0; i < Math.Min(10, (int)(radiusKm * 2)); i++)
                {
                    var offsetLat = (random.NextDouble() - 0.5) * (radiusKm / 111.0); // Rough km to degree conversion
                    var offsetLng = (random.NextDouble() - 0.5) * (radiusKm / 111.0);
                    
                    var candidateLocation = new Location(
                        center.Latitude + offsetLat,
                        center.Longitude + offsetLng
                    );

                    if (IsLocationInNigeria(candidateLocation))
                    {
                        candidates.Add(new PlaceCandidate
                        {
                            PlaceId = $"place_{Guid.NewGuid():N}",
                            Name = $"Amala Spot {i + 1}",
                            Address = $"Street {i + 1}, Local Area",
                            Location = candidateLocation,
                            Rating = (decimal)(3.0 + random.NextDouble() * 2.0),
                            UserRatingsTotal = random.Next(10, 200),
                            PriceLevel = (PriceRange)random.Next(1, 4),
                            Types = new List<string> { "restaurant", "food", "establishment" },
                            IsOpenNow = random.NextDouble() > 0.3
                        });
                    }
                }
            }

            return candidates;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to search places near {center}", ex);
        }
    }

    public async Task<PlaceDetails?> GetPlaceDetailsAsync(string placeId)
    {
        try
        {

            await Task.Delay(30); // Simulate API call delay

            if (string.IsNullOrEmpty(placeId) || !placeId.StartsWith("place_"))
                return null;

            var random = new Random(placeId.GetHashCode());
            
            return new PlaceDetails
            {
                PlaceId = placeId,
                Name = "Sample Amala Restaurant",
                FormattedAddress = "123 Sample Street, Lagos, Nigeria",
                Location = new Location(6.5244 + (random.NextDouble() - 0.5) * 0.1, 3.3792 + (random.NextDouble() - 0.5) * 0.1),
                FormattedPhoneNumber = "+234 801 234 5678",
                Website = "https://example.com",
                Rating = (decimal)(3.0 + random.NextDouble() * 2.0),
                UserRatingsTotal = random.Next(50, 500),
                PriceLevel = (PriceRange)random.Next(1, 4),
                OpeningHours = new List<PlaceOpeningHours>
                {
                    new PlaceOpeningHours { DayOfWeek = 1, OpenTime = new TimeSpan(8, 0, 0), CloseTime = new TimeSpan(22, 0, 0) },
                    new PlaceOpeningHours { DayOfWeek = 2, OpenTime = new TimeSpan(8, 0, 0), CloseTime = new TimeSpan(22, 0, 0) }
                },
                Reviews = new List<PlaceReview>
                {
                    new PlaceReview { AuthorName = "John D.", Rating = 5, Text = "Great amala!", Time = DateTime.UtcNow.AddDays(-10) }
                },
                Photos = new List<string> { "photo_ref_1", "photo_ref_2" },
                Types = new List<string> { "restaurant", "food", "establishment" }
            };
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to get place details for {placeId}", ex);
        }
    }

    public Location PointToLocation(Point point)
    {
        return new Location(point.Y, point.X);
    }

    public Point LocationToPoint(Location location)
    {
        var geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);
        return geometryFactory.CreatePoint(new Coordinate(location.Longitude, location.Latitude));
    }

    public string GenerateStaticMapUrl(GoogleMapsUrlRequest request)
    {
        var baseUrl = "https://maps.googleapis.com/maps/api/staticmap";
        var parameters = new List<string>
    {
        $"center={request.Center.Latitude},{request.Center.Longitude}",
        $"zoom={request.ZoomLevel}",
        $"size={request.Width}x{request.Height}",
        $"maptype={request.MapType}",
        $"key={_googleApiKey}"
    };

        if (request.Markers != null && request.Markers.Any())
        {
            var markerGroups = request.Markers
                .GroupBy(m => m.MarkerColor)
                .Take(10);

            foreach (var group in markerGroups)
            {
                var markerLocations = group.Select(m => $"{m.Latitude},{m.Longitude}");
                var markerParam = $"markers=color:{group.Key}|{string.Join("|", markerLocations)}";
                parameters.Add(markerParam);
            }
        }

        return $"{baseUrl}?{string.Join("&", parameters)}";
    }


    public string GenerateEmbedMapUrl(Location center, int zoomLevel = 15)
    {
        var baseUrl = "https://www.google.com/maps/embed/v1/view";
        var parameters = new List<string>
        {
            $"key={_googleApiKey}",
            $"center={center.Latitude},{center.Longitude}",
            $"zoom={zoomLevel}"
        };

        return $"{baseUrl}?{string.Join("&", parameters)}";
    }

    public string GenerateDirectMapUrl(Location center, int zoomLevel = 15)
    {
        return $"https://www.google.com/maps/@{center.Latitude},{center.Longitude},{zoomLevel}z";
    }

    private static double DegreesToRadians(double degrees)
    {
        return degrees * Math.PI / 180.0;
    }
}