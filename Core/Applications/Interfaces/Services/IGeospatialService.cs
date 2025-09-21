using System.Collections.Generic;
using System.Threading.Tasks;
using AmalaSpotLocator.Models;
using AmalaSpotLocator.Models.MapModel;
using AmalaSpotLocator.Models.SpotModel;
using NetTopologySuite.Geometries;
using Location = AmalaSpotLocator.Models.Location;

namespace AmalaSpotLocator.Interfaces;

public interface IGeospatialService
{

    Task<Location?> GeocodeAddressAsync(string address);

    Task<IEnumerable<AmalaSpot>> FindNearbySpots(Location center, double radiusKm, int limit = 50);

    Task<IEnumerable<AmalaSpot>> FindSpotsInBounds(MapBounds bounds, SpotSearchCriteria? criteria = null);

    double CalculateDistance(Location point1, Location point2);

    double CalculateDistanceToSpot(Location location, AmalaSpot spot);

    Task<IEnumerable<SpotCluster>> ClusterSpots(IEnumerable<AmalaSpot> spots, double clusterRadiusKm = 0.5);

    Task<IEnumerable<SpotCluster>> ClusterSpotsForZoomLevel(IEnumerable<AmalaSpot> spots, int zoomLevel, MapBounds bounds);

    double CalculateOptimalClusterRadius(int zoomLevel, MapBounds bounds);

    int SuggestOptimalZoomLevel(MapBounds bounds, int spotCount);

    bool IsLocationInNigeria(Location location);

    Task<IEnumerable<PlaceCandidate>> SearchPlacesAsync(Location center, double radiusKm, string query = "amala restaurant");

    string GenerateStaticMapUrl(GoogleMapsUrlRequest request);

    string GenerateEmbedMapUrl(Location center, int zoomLevel = 15);

    string GenerateDirectMapUrl(Location center, int zoomLevel = 15);

    Location PointToLocation(Point point);

    Point LocationToPoint(Location location);
}