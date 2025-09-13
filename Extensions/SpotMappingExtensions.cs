using AmalaSpotLocator.Models.DTOs;
using AmalaSpotLocator.Models;
using AmalaSpotLocator.Interfaces;

namespace AmalaSpotLocator.Extensions;

public static class SpotMappingExtensions
{
    public static SpotDto ToDto(this AmalaSpot spot, IGeospatialService? geospatialService = null, Models.Location? userLocation = null)
    {
        var location = geospatialService?.PointToLocation(spot.Location) ?? new Models.Location(0, 0);
        
        double? distanceKm = null;
        if (userLocation != null && geospatialService != null)
        {
            try
            {
                var distance = geospatialService.CalculateDistance(userLocation, location);
                distanceKm = double.IsInfinity(distance) || double.IsNaN(distance) ? null : distance;
            }
            catch
            {
                // Ignore distance calculation errors
            }
        }

        return new SpotDto
        {
            Id = spot.Id,
            Name = spot.Name,
            Description = spot.Description,
            Address = spot.Address,
            Location = new LocationDto
            {
                Latitude = location.Latitude,
                Longitude = location.Longitude
            },
            PhoneNumber = spot.PhoneNumber,
            OpeningTime = spot.OpeningTime?.ToString(@"hh\:mm"),
            ClosingTime = spot.ClosingTime?.ToString(@"hh\:mm"),
            AverageRating = spot.AverageRating,
            ReviewCount = spot.ReviewCount,
            PriceRange = spot.PriceRange,
            Specialties = spot.Specialties?.ToList() ?? new List<string>(),
            CreatedAt = spot.CreatedAt,
            UpdatedAt = spot.UpdatedAt,
            IsVerified = spot.IsVerified,
            CreatedByUserId = spot.CreatedByUserId,
            DistanceKm = distanceKm
        };
    }

    public static List<SpotDto> ToDtoList(this IEnumerable<AmalaSpot> spots, IGeospatialService? geospatialService = null, Models.Location? userLocation = null)
    {
        return spots.Select(spot => spot.ToDto(geospatialService, userLocation)).ToList();
    }
}