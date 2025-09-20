namespace AmalaSpotLocator.Models.UserModel;

public class UserIntent
{
    public IntentType Type { get; set; }
    
    public string OriginalMessage { get; set; } = string.Empty;
    
    public Location? TargetLocation { get; set; }
    
    public decimal? MaxBudget { get; set; }
    
    public decimal? MinRating { get; set; }
    
    public double? MaxDistance { get; set; }
    
    public List<string> Preferences { get; set; } = new();
    
    public string Language { get; set; } = "en";
    
    public string? SessionId { get; set; }
    
    public DateTime ExtractedAt { get; set; } = DateTime.UtcNow;
    
    public Dictionary<string, object> Metadata { get; set; } = new();
    
    public Dictionary<string, string> Parameters { get; set; } = new();
    
    public string Intent { get; set; } = string.Empty;
    
    public Location? Location { get; set; }
}

public enum IntentType
{
    FindNearbySpots,
    GetSpotDetails,
    AddNewSpot,
    AddReview,
    GetDirections,
    FilterSpots,
    CheckBusyness,
    SubmitCheckIn,
    ViewHeatmap,
    BusinessOpportunities,
    UnderservedAreas,
    Unknown
}