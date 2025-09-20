using AmalaSpotLocator.Models;
using AmalaSpotLocator.Models.SpotModel;
using AmalaSpotLocator.Core.Applications.Interfaces.Services;
using AmalaSpotLocator.Interfaces;

namespace AmalaSpotLocator.Core.Applications.Services;

public interface ISpotMappingService
{
    Task<SpotResponse> MapToResponseAsync(AmalaSpot spot, string? placeId = null, bool includeBusyness = true);
    Task<List<SpotResponse>> MapToResponsesAsync(IEnumerable<AmalaSpot> spots, bool includeBusyness = true);
}

public class SpotMappingService : ISpotMappingService
{
    private readonly IBusynessService _busynessService;
    private readonly IGeospatialService _geospatialService;
    private readonly ILogger<SpotMappingService> _logger;

    public SpotMappingService(
        IBusynessService busynessService,
        IGeospatialService geospatialService,
        ILogger<SpotMappingService> logger)
    {
        _busynessService = busynessService;
        _geospatialService = geospatialService;
        _logger = logger;
    }

    public async Task<SpotResponse> MapToResponseAsync(AmalaSpot spot, string? placeId = null, bool includeBusyness = true)
    {
        try
        {
            var location = _geospatialService.PointToLocation(spot.Location);
            
            var response = new SpotResponse
            {
                Id = spot.Id,
                Name = spot.Name,
                Description = spot.Description,
                Address = spot.Address,
                Latitude = location.Latitude,
                Longitude = location.Longitude,
                PhoneNumber = spot.PhoneNumber,
                OpeningTime = spot.OpeningTime,
                ClosingTime = spot.ClosingTime,
                AverageRating = spot.AverageRating,
                ReviewCount = spot.ReviewCount,
                PriceRange = spot.PriceRange,
                Specialties = spot.Specialties ?? new List<string>(),
                CreatedAt = spot.CreatedAt,
                UpdatedAt = spot.UpdatedAt,
                IsVerified = spot.IsVerified,
                DistanceKm = spot.DistanceKm,
                Reviews = spot.Reviews?.Select(r => new ReviewResponse
                {
                    Id = r.Id,
                    Rating = r.Rating,
                    Comment = r.Comment,
                    CreatedAt = r.CreatedAt,
                    UserName = r.User?.UserName ?? "Anonymous"
                }).ToList()
            };
            if (includeBusyness)
            {
                try
                {
                    response.BusynessInfo = await _busynessService.GetCurrentBusynessAsync(spot.Id, placeId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get busyness info for spot {SpotId}", spot.Id);
                }
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error mapping spot {SpotId} to response", spot.Id);
            throw;
        }
    }

    public async Task<List<SpotResponse>> MapToResponsesAsync(IEnumerable<AmalaSpot> spots, bool includeBusyness = true)
    {
        var responses = new List<SpotResponse>();
        
        foreach (var spot in spots)
        {
            try
            {
                var response = await MapToResponseAsync(spot, null, includeBusyness);
                responses.Add(response);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to map spot {SpotId}, skipping", spot.Id);
            }
        }

        return responses;
    }
}