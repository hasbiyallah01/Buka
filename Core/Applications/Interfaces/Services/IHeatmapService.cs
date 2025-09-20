using AmalaSpotLocator.Models;

namespace AmalaSpotLocator.Core.Applications.Interfaces.Services;

public interface IHeatmapService
{
    Task<HeatmapData> GenerateLagosAmalaHeatmapAsync();
    Task<GooglePlacesHeatmapResponse> AnalyzeGooglePlacesAsync(GooglePlacesHeatmapRequest request);
    Task<List<UnderservedArea>> IdentifyUnderservedAreasAsync();
    Task<HeatmapPoint> GetAreaDensityAsync(Location location, double radiusKm = 2);
    Task<List<BusinessOpportunity>> GetBusinessOpportunitiesAsync();
}
