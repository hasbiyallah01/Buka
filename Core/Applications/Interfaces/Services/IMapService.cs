using System.Collections.Generic;
using System.Threading.Tasks;
using AmalaSpotLocator.Models;
using AmalaSpotLocator.Models.MapModel;

namespace AmalaSpotLocator.Interfaces;

public interface IMapService
{

    Task<MapDataResponse> GetMapDataAsync(MapDataRequest request);

    List<MapMarker> ConvertSpotsToMarkers(IEnumerable<AmalaSpot> spots);

    List<ClusterMarker> ConvertClustersToMarkers(IEnumerable<SpotCluster> clusters);

    Task<GoogleMapsUrlResponse> GenerateMapUrlsAsync(GoogleMapsUrlRequest request);

    string GetMarkerColor(AmalaSpot spot);

    string GetClusterMarkerColor(SpotCluster cluster);

    bool IsSpotCurrentlyOpen(AmalaSpot spot);
}