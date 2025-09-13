using AmalaSpotLocator.Interfaces;
using System.Collections.Generic;
using AmalaSpotLocator.Models.MapModel;
using AmalaSpotLocator.Models.SpotModel;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;
using AmalaSpotLocator.Models;

namespace AmalaSpotLocator.Core.Applications.Services;

public class MapService : IMapService
{
    private readonly IGeospatialService _geospatialService;
    private readonly ISpotService _spotService;
    private readonly ILogger<MapService> _logger;

    public MapService(
        IGeospatialService geospatialService,
        ISpotService spotService,
        ILogger<MapService> logger)
    {
        _geospatialService = geospatialService;
        _spotService = spotService;
        _logger = logger;
    }

    public async Task<MapDataResponse> GetMapDataAsync(MapDataRequest request)
    {
        try
        {
            _logger.LogInformation("Getting map data for bounds: SW({SWLat},{SWLng}) NE({NELat},{NELng}) Zoom:{Zoom}",
                request.Bounds.SouthWestLat, request.Bounds.SouthWestLng,
                request.Bounds.NorthEastLat, request.Bounds.NorthEastLng, request.ZoomLevel);

            var criteria = new SpotSearchCriteria
            {
                MinRating = request.MinRating,
                MinPriceRange = request.MinPriceRange,
                MaxPriceRange = request.MaxPriceRange,
                IsVerified = request.IsVerified,
                Specialties = request.Specialties,
                Limit = request.MaxResults
            };

            if (request.IsCurrentlyOpen == true)
            {
                criteria.CurrentTime = DateTime.Now.TimeOfDay;
            }

            var spots = await _geospatialService.FindSpotsInBounds(request.Bounds, criteria);
            var spotsList = spots.ToList();

            var response = new MapDataResponse
            {
                TotalSpots = spotsList.Count,
                SuggestedZoomLevel = _geospatialService.SuggestOptimalZoomLevel(request.Bounds, spotsList.Count)
            };

            var shouldCluster = request.EnableClustering && ShouldApplyClustering(spotsList.Count, request.ZoomLevel);
            response.IsClustered = shouldCluster;

            if (shouldCluster)
            {

                var clusters = await _geospatialService.ClusterSpotsForZoomLevel(
                    spotsList, request.ZoomLevel, request.Bounds);
                
                response.Clusters = ConvertClustersToMarkers(clusters);

                var smallClusters = clusters.Where(c => c.Count <= 2);
                var individualSpots = smallClusters.SelectMany(c => c.Spots);
                response.Markers = ConvertSpotsToMarkers(individualSpots);

                response.Clusters = response.Clusters.Where(c => c.SpotCount > 2).ToList();
            }
            else
            {

                response.Markers = ConvertSpotsToMarkers(spotsList);
            }

            var center = request.Bounds.GetCenter();
            var urlRequest = new GoogleMapsUrlRequest
            {
                Center = center,
                ZoomLevel = request.ZoomLevel,
                Markers = response.Markers.Take(10).ToList() // Limit markers for URL generation
            };

            response.StaticMapUrl = _geospatialService.GenerateStaticMapUrl(urlRequest);
            response.EmbedMapUrl = _geospatialService.GenerateEmbedMapUrl(center, request.ZoomLevel);

            _logger.LogInformation("Map data generated: {TotalSpots} spots, {MarkerCount} markers, {ClusterCount} clusters", response.TotalSpots, response.Markers.Count, response.Clusters.Count);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating map data for bounds");
            throw new InvalidOperationException("Failed to generate map data", ex);
        }
    }

    public List<MapMarker> ConvertSpotsToMarkers(IEnumerable<AmalaSpot> spots)
    {
        return spots.Select(spot =>
        {
            var location = _geospatialService.PointToLocation(spot.Location);
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
                IsCurrentlyOpen = IsSpotCurrentlyOpen(spot),
                Address = GetShortAddress(spot.Address),
                MarkerColor = GetMarkerColor(spot),
                MarkerIcon = "restaurant"
            };
        }).ToList();
    }

    public List<ClusterMarker> ConvertClustersToMarkers(IEnumerable<SpotCluster> clusters)
    {
        return clusters.Select(cluster => new ClusterMarker
        {
            Latitude = cluster.Center.Latitude,
            Longitude = cluster.Center.Longitude,
            SpotCount = cluster.Count,
            AverageRating = cluster.AverageRating,
            DominantPriceRange = cluster.DominantPriceRange,
            SpotIds = cluster.Spots.Select(s => s.Id).ToList(),
            RadiusKm = CalculateClusterRadius(cluster),
            MarkerColor = GetClusterMarkerColor(cluster),
            SpotNames = cluster.Spots.Take(3).Select(s => s.Name).ToList()
        }).ToList();
    }

    public async Task<GoogleMapsUrlResponse> GenerateMapUrlsAsync(GoogleMapsUrlRequest request)
    {
        try
        {
            return new GoogleMapsUrlResponse
            {
                StaticMapUrl = _geospatialService.GenerateStaticMapUrl(request),
                EmbedMapUrl = _geospatialService.GenerateEmbedMapUrl(request.Center, request.ZoomLevel),
                DirectMapUrl = _geospatialService.GenerateDirectMapUrl(request.Center, request.ZoomLevel)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating Google Maps URLs");
            throw new InvalidOperationException("Failed to generate Google Maps URLs", ex);
        }
    }

    public string GetMarkerColor(AmalaSpot spot)
    {

        if (!spot.IsVerified) return "gray";
        
        return spot.AverageRating switch
        {
            >= 4.5m => "green",    // Excellent
            >= 4.0m => "blue",     // Very good
            >= 3.5m => "yellow",   // Good
            >= 3.0m => "orange",   // Average
            _ => "red"             // Below average
        };
    }

    public string GetClusterMarkerColor(SpotCluster cluster)
    {

        return cluster.AverageRating switch
        {
            >= 4.5m => "darkgreen",
            >= 4.0m => "blue",
            >= 3.5m => "purple",
            >= 3.0m => "orange",
            _ => "darkred"
        };
    }

    public bool IsSpotCurrentlyOpen(AmalaSpot spot)
    {
        if (!spot.OpeningTime.HasValue || !spot.ClosingTime.HasValue)
            return false;

        var currentTime = DateTime.Now.TimeOfDay;
        var openTime = spot.OpeningTime.Value;
        var closeTime = spot.ClosingTime.Value;

        if (openTime <= closeTime)
        {
            return currentTime >= openTime && currentTime <= closeTime;
        }

        return currentTime >= openTime || currentTime <= closeTime;
    }

    private bool ShouldApplyClustering(int spotCount, int zoomLevel)
    {

        if (spotCount < 5) return false; // Too few spots to cluster
        if (zoomLevel >= 16) return false; // Too zoomed in for clustering

        var clusterThreshold = Math.Max(10, 50 - zoomLevel * 2);
        return spotCount >= clusterThreshold;
    }

    private double CalculateClusterRadius(SpotCluster cluster)
    {
        if (cluster.Spots.Count <= 1) return 0.1;

        var center = cluster.Center;
        var maxDistance = cluster.Spots
            .Select(spot => _geospatialService.CalculateDistanceToSpot(center, spot))
            .DefaultIfEmpty(0.1)
            .Max();

        return Math.Max(0.1, maxDistance);
    }

    private string GetShortAddress(string fullAddress)
    {

        var parts = fullAddress.Split(',');
        return parts.Length > 0 ? parts[0].Trim() : fullAddress;
    }
}