using System.ComponentModel;

namespace AmalaSpotLocator.Models;

public enum BusynessLevel
{
    [Description("Very quiet - no wait time")]
    VeryQuiet = 1,
    
    [Description("Quiet - minimal wait")]
    Quiet = 2,
    
    [Description("Moderate - short wait expected")]
    Moderate = 3,
    
    [Description("Busy - expect some wait time")]
    Busy = 4,
    
    [Description("Very busy - long wait expected")]
    VeryBusy = 5,
    
    [Description("Packed - avoid if possible")]
    Packed = 6
}

public class BusynessInfo
{
    public BusynessLevel CurrentLevel { get; set; }
    public string Description { get; set; } = string.Empty;
    public int EstimatedWaitMinutes { get; set; }
    public DateTime LastUpdated { get; set; }
    public BusynessSource Source { get; set; }
    public int? PopularityScore { get; set; } 
    public List<HourlyBusyness> WeeklyPattern { get; set; } = new();
    public int CheckInCount { get; set; } 
    public List<string> Recommendations { get; set; } = new(); 
}

public enum BusynessSource
{
    GooglePlaces,
    CrowdSourced,
    Hybrid,
    Estimated
}

public class HourlyBusyness
{
    public DayOfWeek DayOfWeek { get; set; }
    public int Hour { get; set; } 
    public int BusynessPercentage { get; set; } 
    public BusynessLevel Level { get; set; }
}

public class CheckInRequest
{
    public Guid SpotId { get; set; }
    public BusynessLevel ReportedLevel { get; set; }
    public int? EstimatedWaitMinutes { get; set; }
    public string? Notes { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class CheckInResponse
{
    public Guid Id { get; set; }
    public Guid SpotId { get; set; }
    public BusynessLevel ReportedLevel { get; set; }
    public int? EstimatedWaitMinutes { get; set; }
    public string? Notes { get; set; }
    public DateTime Timestamp { get; set; }
    public bool IsVerified { get; set; }
}