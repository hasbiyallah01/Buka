namespace AmalaSpotLocator.Models;

public class PlaceCandidate
{
    public string PlaceId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public Location Location { get; set; } = null!;
    public decimal? Rating { get; set; }
    public int? UserRatingsTotal { get; set; }
    public PriceRange? PriceLevel { get; set; }
    public List<string> Types { get; set; } = new();
    public bool IsOpenNow { get; set; }
    public string? PhotoReference { get; set; }
}

public class PlaceDetails
{
    public string PlaceId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string FormattedAddress { get; set; } = string.Empty;
    public Location Location { get; set; } = null!;
    public string? FormattedPhoneNumber { get; set; }
    public string? Website { get; set; }
    public decimal? Rating { get; set; }
    public int? UserRatingsTotal { get; set; }
    public PriceRange? PriceLevel { get; set; }
    public List<PlaceOpeningHours> OpeningHours { get; set; } = new();
    public List<PlaceReview> Reviews { get; set; } = new();
    public List<string> Photos { get; set; } = new();
    public List<string> Types { get; set; } = new();
}

public class PlaceOpeningHours
{
    public int DayOfWeek { get; set; } 
    public TimeSpan OpenTime { get; set; }
    public TimeSpan CloseTime { get; set; }
}

public class PlaceReview
{
    public string AuthorName { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime Time { get; set; }
}