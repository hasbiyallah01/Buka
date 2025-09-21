namespace AmalaSpotLocator.Models;

public class GooglePlaceBusynessRequest
{
    public string PlaceId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public Location Location { get; set; } = new();
    public decimal? Rating { get; set; }
    public int? UserRatingsTotal { get; set; }
    public PriceRange? PriceLevel { get; set; }
    public List<string> Types { get; set; } = new();
    public bool? IsOpenNow { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Website { get; set; }
    public List<GooglePlaceReview> Reviews { get; set; } = new();
    public List<GooglePlacePhoto> Photos { get; set; } = new();
    public List<GooglePlaceOpeningHours> OpeningHours { get; set; } = new();
}

public class GooglePlaceReview
{
    public string AuthorName { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime Time { get; set; }
}

public class GooglePlacePhoto
{
    public string PhotoReference { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
}

public class GooglePlaceOpeningHours
{
    public int DayOfWeek { get; set; } 
    public TimeSpan OpenTime { get; set; }
    public TimeSpan CloseTime { get; set; }
}

public class GooglePlacesHeatmapRequest
{
    public List<GooglePlaceBusynessRequest> Places { get; set; } = new();
    public Location? CenterLocation { get; set; }
    public double RadiusKm { get; set; } = 10;
    public bool IncludeBusynessAnalysis { get; set; } = true;
    public bool IncludeBusinessOpportunities { get; set; } = true;
    public bool IncludeUnderservedAreas { get; set; } = true;
}

public class GooglePlacesHeatmapResponse
{
    public List<GooglePlaceWithBusyness> PlacesWithBusyness { get; set; } = new();
    public HeatmapAnalysis HeatmapAnalysis { get; set; } = new();
    public List<BusinessOpportunity> BusinessOpportunities { get; set; } = new();
    public List<UnderservedArea> UnderservedAreas { get; set; } = new();
    public HeatmapInsights Insights { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

public class GooglePlaceWithBusyness
{
    public string PlaceId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public Location Location { get; set; } = new();
    public decimal? Rating { get; set; }
    public int? UserRatingsTotal { get; set; }
    public PriceRange? PriceLevel { get; set; }
    public BusynessInfo BusynessInfo { get; set; } = new();
    public double DistanceFromCenter { get; set; }
    public HeatmapCategory DensityCategory { get; set; }
    public List<string> Specialties { get; set; } = new();
    public string RecommendationMessage { get; set; } = string.Empty;
}

public class HeatmapAnalysis
{
    public int TotalPlaces { get; set; }
    public double AverageRating { get; set; }
    public double AverageBusyness { get; set; }
    public Dictionary<HeatmapCategory, int> DensityDistribution { get; set; } = new();
    public Dictionary<BusynessLevel, int> BusynessDistribution { get; set; } = new();
    public Dictionary<PriceRange, int> PriceDistribution { get; set; } = new();
    public List<HeatmapHotspot> Hotspots { get; set; } = new();
    public List<HeatmapColdspot> Coldspots { get; set; } = new();
}

public class HeatmapHotspot
{
    public Location Center { get; set; } = new();
    public int PlaceCount { get; set; }
    public double AverageRating { get; set; }
    public double AverageBusyness { get; set; }
    public double RadiusKm { get; set; }
    public string Description { get; set; } = string.Empty;
    public List<string> TopPlaces { get; set; } = new();
}

public class HeatmapColdspot
{
    public Location Center { get; set; } = new();
    public double RadiusKm { get; set; }
    public int EstimatedPopulation { get; set; }
    public string AreaName { get; set; } = string.Empty;
    public string OpportunityDescription { get; set; } = string.Empty;
    public double OpportunityScore { get; set; }
}

public class HeatmapInsights
{
    public string Summary { get; set; } = string.Empty;
    public List<string> KeyFindings { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
    public List<string> BusinessOpportunities { get; set; } = new();
    public Dictionary<string, object> Statistics { get; set; } = new();
}

public class QuickBusynessRequest
{
    public string PlaceId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Location Location { get; set; } = new();
    public decimal? Rating { get; set; }
    public int? UserRatingsTotal { get; set; }
    public bool? IsOpenNow { get; set; }
}

public class QuickBusynessResponse
{
    public string PlaceId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public BusynessLevel CurrentLevel { get; set; }
    public string Description { get; set; } = string.Empty;
    public int EstimatedWaitMinutes { get; set; }
    public List<string> Recommendations { get; set; } = new();
    public string QuickMessage { get; set; } = string.Empty;
    public DateTime LastUpdated { get; set; }
}