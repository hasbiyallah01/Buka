namespace AmalaSpotLocator.Models;
public class SimpleBusynessRequest
{
    public string Name { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? PlaceId { get; set; } 
    public decimal? Rating { get; set; } 
    public bool? IsOpen { get; set; } 
}
public class SimpleBusynessResponse
{
    public string Name { get; set; } = string.Empty;
    public string BusynessLevel { get; set; } = string.Empty; 
    public int WaitMinutes { get; set; }
    public string Message { get; set; } = string.Empty; 
    public List<string> Tips { get; set; } = new(); 
}
public class BatchBusynessRequest
{
    public List<SimpleBusynessRequest> Places { get; set; } = new();
    public bool IncludeHeatmap { get; set; } = false; 
}
public class BatchBusynessResponse
{
    public List<SimpleBusynessResponse> Places { get; set; } = new();
    public SimpleHeatmapSummary? HeatmapSummary { get; set; }
}
public class SimpleHeatmapSummary
{
    public int TotalPlaces { get; set; }
    public int BusyPlaces { get; set; }
    public string Summary { get; set; } = string.Empty; 
    public List<string> Hotspots { get; set; } = new(); 
    public List<string> Opportunities { get; set; } = new(); 
}