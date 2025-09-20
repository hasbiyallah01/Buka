namespace AmalaSpotLocator.Models;
public class SimpleBusynessRequest
{
    public string Name { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? PlaceId { get; set; } // Optional - for Google Places integration
    public decimal? Rating { get; set; } // Optional - helps with estimation
    public bool? IsOpen { get; set; } // Optional - current open status
}
public class SimpleBusynessResponse
{
    public string Name { get; set; } = string.Empty;
    public string BusynessLevel { get; set; } = string.Empty; // "Quiet", "Busy", "Packed", etc.
    public int WaitMinutes { get; set; }
    public string Message { get; set; } = string.Empty; // "✅ Mama Put is quiet - perfect time to visit!"
    public List<string> Tips { get; set; } = new(); // Quick tips if busy
}
public class BatchBusynessRequest
{
    public List<SimpleBusynessRequest> Places { get; set; } = new();
    public bool IncludeHeatmap { get; set; } = false; // Optional heatmap analysis
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
    public string Summary { get; set; } = string.Empty; // "3 of 5 spots are busy right now"
    public List<string> Hotspots { get; set; } = new(); // ["Ikeja area has 4 spots", "Victoria Island is packed"]
    public List<string> Opportunities { get; set; } = new(); // ["Lekki needs more amala spots"]
}