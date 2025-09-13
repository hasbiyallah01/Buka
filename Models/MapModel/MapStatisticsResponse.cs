namespace AmalaSpotLocator.Models.MapModel;

public class MapStatisticsResponse
{
    public int TotalSpots { get; set; }
    public decimal AverageRating { get; set; }
    public Dictionary<PriceRange, int> PriceRangeDistribution { get; set; } = new();
    public int VerifiedSpots { get; set; }
    public int OpenSpots { get; set; }
    public int ClusterCount { get; set; }
}