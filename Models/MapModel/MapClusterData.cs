namespace AmalaSpotLocator.Models.MapModel;

public class MapClusterData
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int SpotCount { get; set; }
    public decimal AverageRating { get; set; }
    public PriceRange? DominantPriceRange { get; set; }
    public List<Guid> SpotIds { get; set; } = new();
    public string ClusterColor { get; set; } = "#4ECDC4"; // Default teal color
    public int ClusterSize { get; set; } // For visual sizing
}