using AmalaSpotLocator.Models.SpotModel;

namespace AmalaSpotLocator.Models.ClusterModel;

public class ClusterDetailsResponse
{
    public string ClusterId { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int SpotCount { get; set; }
    public decimal AverageRating { get; set; }
    public PriceRange? DominantPriceRange { get; set; }
    public List<SpotResponse> Spots { get; set; } = new();
    public string ClusterColor { get; set; } = string.Empty;
    public int ClusterSize { get; set; }
}