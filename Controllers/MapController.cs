using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using AmalaSpotLocator.Interfaces;
using System.Collections.Generic;
using AmalaSpotLocator.Models.MapModel;
using AmalaSpotLocator.Models.SpotModel;
using AmalaSpotLocator.Models.ClusterModel;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Linq;
using System;
using AmalaSpotLocator.Models;

namespace AmalaSpotLocator.Controllers;

[ApiController]
[Route("api/map")]
public class MapController : ControllerBase
{
    private readonly IGeospatialService _geospatialService;
    private readonly ISpotService _spotService;
    private readonly ILogger<MapController> _logger;

    public MapController(
        IGeospatialService geospatialService,
        ISpotService spotService,
        ILogger<MapController> logger)
    {
        _geospatialService = geospatialService;
        _spotService = spotService;
        _logger = logger;
    }

    [HttpPost("data")]
    [ProducesResponseType(typeof(MapDataResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<MapDataResponse>> GetMapData([FromBody] MapDataRequest request)
    {
        try
        {
            _logger.LogInformation("Getting map data for bounds: SW({SWLat},{SWLng}) NE({NELat},{NELng}) Zoom:{Zoom}",
                request.Bounds.SouthWestLat, request.Bounds.SouthWestLng,
                request.Bounds.NorthEastLat, request.Bounds.NorthEastLng, request.ZoomLevel);

            var criteria = BuildSearchCriteria(request);

            var spots = await _geospatialService.FindSpotsInBounds(request.Bounds, criteria);
            var spotsList = spots.ToList();

            _logger.LogInformation("Found {SpotCount} spots in bounds", spotsList.Count);

            var response = new MapDataResponse
            {
                TotalSpots = spotsList.Count,
                SuggestedZoomLevel = _geospatialService.SuggestOptimalZoomLevel(request.Bounds, spotsList.Count)
            };

            if (request.EnableClustering && ShouldCluster(spotsList.Count, request.ZoomLevel))
            {
                var clusters = await _geospatialService.ClusterSpotsForZoomLevel(
                    spotsList, request.ZoomLevel, request.Bounds);
                
                response.IsClustered = true;
                response.Clusters = clusters.Select(MapClusterToDto).ToList();

                _logger.LogInformation($"Found {spotsList.Count} spots in bounds");
            }
            else
            {
                response.IsClustered = false;
                response.Markers = spotsList.Take(request.MaxResults).Select(MapSpotToMarker).ToList();
                _logger.LogInformation($"No clustering applied: {response.Markers.Count()} individual markers");


            }

            var center = request.Bounds.GetCenter();
            response.StaticMapUrl = _geospatialService.GenerateStaticMapUrl(new GoogleMapsUrlRequest
            {
                Center = center,
                ZoomLevel = request.ZoomLevel,
                Markers = response.Markers?.Take(10).ToList() // Limit markers for static map
            });
            response.EmbedMapUrl = _geospatialService.GenerateEmbedMapUrl(center, request.ZoomLevel);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting map data for bounds");
            return StatusCode(500, "An error occurred while retrieving map data");
        }
    }

    [HttpGet("cluster/{clusterId}")]
    [ProducesResponseType(typeof(ClusterDetailsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ClusterDetailsResponse>> GetClusterDetails(
        string clusterId,
        [FromQuery] [Required] double latitude,
        [FromQuery] [Required] double longitude,
        [FromQuery] [Range(0.1, 10)] double radiusKm = 0.5)
    {
        try
        {
            _logger.LogInformation("Getting cluster details for {ClusterId} at ({Lat},{Lng}) radius {Radius}km",
                clusterId, latitude, longitude, radiusKm);

            var center = new Location(latitude, longitude);
            var spots = await _geospatialService.FindNearbySpots(center, radiusKm);
            var spotsList = spots.ToList();

            if (!spotsList.Any())
            {
                _logger.LogWarning("No spots found for cluster {ClusterId}", clusterId);
                return NotFound($"Cluster {clusterId} not found or contains no spots");
            }

            var response = new ClusterDetailsResponse
            {
                ClusterId = clusterId,
                Latitude = latitude,
                Longitude = longitude,
                SpotCount = spotsList.Count,
                AverageRating = spotsList.Any() ? spotsList.Average(s => s.AverageRating) : 0,
                DominantPriceRange = spotsList.GroupBy(s => s.PriceRange)
                    .OrderByDescending(g => g.Count())
                    .FirstOrDefault()?.Key,
                Spots = spotsList.Select(MapSpotToResponse).ToList(),
                ClusterColor = GetClusterColor(spotsList.Average(s => s.AverageRating)),
                ClusterSize = CalculateClusterSize(spotsList.Count)
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cluster details for {ClusterId}", clusterId);
            return StatusCode(500, "An error occurred while retrieving cluster details");
        }
    }

    [HttpPost("clustering-params")]
    [ProducesResponseType(typeof(ClusteringParamsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ClusteringParamsResponse>> GetClusteringParams(
        [FromBody] MapBounds bounds,
        [FromQuery] [Range(1, 20)] int zoomLevel = 10,
        [FromQuery] int? spotCount = null)
    {
        try
        {
            _logger.LogInformation("Getting clustering params for zoom level {ZoomLevel}", zoomLevel);

            int actualSpotCount = spotCount ?? 0;
            if (!spotCount.HasValue)
            {
                var spots = await _geospatialService.FindSpotsInBounds(bounds);
                actualSpotCount = spots.Count();
            }

            var response = new ClusteringParamsResponse
            {
                ZoomLevel = zoomLevel,
                OptimizedClusterRadius = _geospatialService.CalculateOptimalClusterRadius(zoomLevel, bounds),
                ShouldEnableClustering = ShouldCluster(actualSpotCount, zoomLevel),
                RecommendedMaxResults = CalculateMaxResults(zoomLevel, actualSpotCount)
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting clustering parameters");
            return StatusCode(500, "An error occurred while calculating clustering parameters");
        }
    }

    [HttpPost("statistics")]
    [ProducesResponseType(typeof(MapStatisticsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<MapStatisticsResponse>> GetMapStatistics([FromBody] MapBounds bounds)
    {
        try
        {
            _logger.LogInformation("Getting map statistics for bounds");

            var spots = await _geospatialService.FindSpotsInBounds(bounds);
            var spotsList = spots.ToList();

            var currentTime = DateTime.Now.TimeOfDay;
            var openSpots = spotsList.Count(s => IsSpotCurrentlyOpen(s, currentTime));

            var response = new MapStatisticsResponse
            {
                TotalSpots = spotsList.Count,
                AverageRating = spotsList.Any() ? spotsList.Average(s => s.AverageRating) : 0,
                PriceRangeDistribution = spotsList.GroupBy(s => s.PriceRange)
                    .ToDictionary(g => g.Key, g => g.Count()),
                VerifiedSpots = spotsList.Count(s => s.IsVerified),
                OpenSpots = openSpots,
                ClusterCount = 0 // Will be calculated if clustering is applied
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting map statistics");
            return StatusCode(500, "An error occurred while retrieving map statistics");
        }
    }

    [HttpPost("google-maps-urls")]
    [ProducesResponseType(typeof(GoogleMapsUrlResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public ActionResult<GoogleMapsUrlResponse> GenerateGoogleMapsUrls([FromBody] GoogleMapsUrlRequest request)
    {
        try
        {
            _logger.LogInformation("Generating Google Maps URLs for center ({Lat},{Lng})",
                request.Center.Latitude, request.Center.Longitude);

            var response = new GoogleMapsUrlResponse
            {
                StaticMapUrl = _geospatialService.GenerateStaticMapUrl(request),
                EmbedMapUrl = _geospatialService.GenerateEmbedMapUrl(request.Center, request.ZoomLevel),
                DirectMapUrl = _geospatialService.GenerateDirectMapUrl(request.Center, request.ZoomLevel)
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating Google Maps URLs");
            return StatusCode(500, "An error occurred while generating map URLs");
        }
    }

    #region Private Helper Methods

    private SpotSearchCriteria BuildSearchCriteria(MapDataRequest request)
    {
        var criteria = new SpotSearchCriteria
        {
            MinPriceRange = request.MinPriceRange,
            MaxPriceRange = request.MaxPriceRange,
            MinRating = request.MinRating,
            IsVerified = request.IsVerified,
            Specialties = request.Specialties,
            Limit = request.MaxResults
        };

        if (request.IsCurrentlyOpen == true)
        {
            criteria.CurrentTime = DateTime.Now.TimeOfDay;
        }

        return criteria;
    }

    private bool ShouldCluster(int spotCount, int zoomLevel)
    {

        if (zoomLevel >= 16) return false; // Too zoomed in for clustering
        if (spotCount < 5) return false; // Too few spots to cluster
        if (zoomLevel <= 10 && spotCount > 20) return true; // Zoomed out with many spots
        if (zoomLevel <= 13 && spotCount > 50) return true; // Medium zoom with lots of spots
        
        return spotCount > 100; // Always cluster if too many spots
    }

    private int CalculateMaxResults(int zoomLevel, int spotCount)
    {

        return zoomLevel switch
        {
            >= 16 => Math.Min(50, spotCount),
            >= 13 => Math.Min(100, spotCount),
            >= 10 => Math.Min(200, spotCount),
            _ => Math.Min(500, spotCount)
        };
    }

    private ClusterMarker MapClusterToDto(SpotCluster cluster)
    {
        return new ClusterMarker
        {
            Latitude = cluster.Center.Latitude,
            Longitude = cluster.Center.Longitude,
            SpotCount = cluster.Count,
            AverageRating = cluster.AverageRating,
            DominantPriceRange = cluster.DominantPriceRange,
            SpotIds = cluster.Spots.Select(s => s.Id).ToList(),
            RadiusKm = 0.5, // Default cluster radius
            MarkerColor = GetClusterColor(cluster.AverageRating),
            SpotNames = cluster.Spots.Take(3).Select(s => s.Name).ToList()
        };
    }

    private MapMarker MapSpotToMarker(AmalaSpot spot)
    {
        var location = _geospatialService.PointToLocation(spot.Location);
        var currentTime = DateTime.Now.TimeOfDay;
        
        return new MapMarker
        {
            Id = spot.Id,
            Name = spot.Name,
            Latitude = location.Latitude,
            Longitude = location.Longitude,
            Rating = spot.AverageRating,
            ReviewCount = spot.ReviewCount,
            PriceRange = spot.PriceRange,
            IsVerified = spot.IsVerified,
            IsCurrentlyOpen = IsSpotCurrentlyOpen(spot, currentTime),
            Address = spot.Address,
            MarkerColor = GetMarkerColor(spot.AverageRating, spot.IsVerified),
            MarkerIcon = "restaurant"
        };
    }

    private SpotResponse MapSpotToResponse(AmalaSpot spot)
    {
        var location = _geospatialService.PointToLocation(spot.Location);
        
        return new SpotResponse
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
            Specialties = spot.Specialties,
            CreatedAt = spot.CreatedAt,
            UpdatedAt = spot.UpdatedAt,
            IsVerified = spot.IsVerified
        };
    }

    private bool IsSpotCurrentlyOpen(AmalaSpot spot, TimeSpan currentTime)
    {
        if (!spot.OpeningTime.HasValue || !spot.ClosingTime.HasValue)
            return false;

        var openTime = spot.OpeningTime.Value;
        var closeTime = spot.ClosingTime.Value;

        if (openTime <= closeTime)
        {
            return currentTime >= openTime && currentTime <= closeTime;
        }
        else
        {
            return currentTime >= openTime || currentTime <= closeTime;
        }
    }

    private string GetMarkerColor(decimal rating, bool isVerified)
    {
        if (isVerified)
        {
            return rating switch
            {
                >= 4.5m => "#2ECC71", // Green for excellent verified spots
                >= 4.0m => "#3498DB", // Blue for good verified spots
                >= 3.0m => "#F39C12", // Orange for average verified spots
                _ => "#E74C3C" // Red for poor verified spots
            };
        }
        else
        {
            return rating switch
            {
                >= 4.5m => "#27AE60", // Darker green for excellent unverified spots
                >= 4.0m => "#2980B9", // Darker blue for good unverified spots
                >= 3.0m => "#E67E22", // Darker orange for average unverified spots
                _ => "#C0392B" // Darker red for poor unverified spots
            };
        }
    }

    private string GetClusterColor(decimal averageRating)
    {
        return averageRating switch
        {
            >= 4.5m => "#2ECC71", // Green for excellent clusters
            >= 4.0m => "#3498DB", // Blue for good clusters
            >= 3.0m => "#F39C12", // Orange for average clusters
            _ => "#E74C3C" // Red for poor clusters
        };
    }

    private int CalculateClusterSize(int spotCount)
    {

        return spotCount switch
        {
            >= 50 => 5,
            >= 20 => 4,
            >= 10 => 3,
            >= 5 => 2,
            _ => 1
        };
    }

    #endregion
}