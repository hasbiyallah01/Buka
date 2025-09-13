namespace AmalaSpotLocator.Models.ClusterModel;

public class ClusteringParamsResponse
{
    public int ZoomLevel { get; set; }
    public double OptimizedClusterRadius { get; set; }
    public bool ShouldEnableClustering { get; set; }
    public int RecommendedMaxResults { get; set; }
}