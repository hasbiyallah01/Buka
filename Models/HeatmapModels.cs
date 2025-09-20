namespace AmalaSpotLocator.Models;

public class HeatmapData
{
    public List<HeatmapPoint> Points { get; set; } = new();
    public List<UnderservedArea> UnderservedAreas { get; set; } = new();
    public List<BusinessOpportunity> BusinessOpportunities { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
    public int TotalSpots { get; set; }
    public double AverageIntensity { get; set; }
    public string Summary => GenerateSummary();

    private string GenerateSummary()
    {
        var hotspots = Points.Count(p => p.Category >= HeatmapCategory.High);
        var underserved = UnderservedAreas.Count;
        
        return $"Lagos has {TotalSpots} amala spots with {hotspots} high-density areas and {underserved} underserved regions offering business opportunities.";
    }
}

public class HeatmapPoint
{
    public Location Location { get; set; } = new();
    public int SpotCount { get; set; }
    public double Intensity { get; set; } // 0-100 scale
    public double AverageBusyness { get; set; }
    public double Radius { get; set; }
    public HeatmapCategory Category { get; set; }
    public string Description => GetDescription();

    private string GetDescription()
    {
        return Category switch
        {
            HeatmapCategory.VeryHigh => $"Amala hotspot! {SpotCount} spots in {Radius}km - very competitive area",
            HeatmapCategory.High => $"Popular area with {SpotCount} amala spots",
            HeatmapCategory.Medium => $"Moderate density - {SpotCount} spots available",
            HeatmapCategory.Low => $"Few options - only {SpotCount} spots nearby",
            HeatmapCategory.VeryLow => $"Limited availability - {SpotCount} spot(s) in area",
            _ => "No amala spots in this area"
        };
    }
}

public enum HeatmapCategory
{
    None = 0,
    VeryLow = 1,
    Low = 2,
    Medium = 3,
    High = 4,
    VeryHigh = 5
}

public class UnderservedArea
{
    public Location Location { get; set; } = new();
    public string AreaName { get; set; } = string.Empty;
    public int Population { get; set; }
    public int CurrentSpotCount { get; set; }
    public double SpotsPerCapita { get; set; } // Per 100k people
    public int RecommendedSpots { get; set; }
    public UnderservedSeverity Severity { get; set; }
    public List<string> Reasons { get; set; } = new();
    public string OpportunityDescription => $"{AreaName}: {Population:N0} people, only {CurrentSpotCount} amala spots ({SpotsPerCapita:F1} per 100k)";
}

public enum UnderservedSeverity
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public class BusinessOpportunity
{
    public Location Location { get; set; } = new();
    public string AreaName { get; set; } = string.Empty;
    public double OpportunityScore { get; set; } // 0-100
    public int EstimatedDemand { get; set; } // Number of potential spots
    public CompetitionLevel CompetitionLevel { get; set; }
    public string RecommendedInvestment { get; set; } = string.Empty;
    public List<string> Reasons { get; set; } = new();
    public List<string> RiskFactors { get; set; } = new();
    public string EstimatedROI { get; set; } = string.Empty;
    public string Summary => $"{AreaName}: Score {OpportunityScore:F0}/100, {CompetitionLevel} competition, ROI {EstimatedROI}";
}

public enum CompetitionLevel
{
    VeryLow,
    Low,
    Medium,
    High,
    VeryHigh
}

public class HeatmapRequest
{
    public Location? Center { get; set; }
    public double RadiusKm { get; set; } = 20; // Default to 20km for Lagos
    public bool IncludeUnderservedAreas { get; set; } = true;
    public bool IncludeBusinessOpportunities { get; set; } = true;
    public HeatmapCategory MinIntensity { get; set; } = HeatmapCategory.VeryLow;
}