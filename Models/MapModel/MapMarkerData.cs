namespace AmalaSpotLocator.Models.MapModel;

public class MapMarkerData
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public decimal AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public PriceRange PriceRange { get; set; }
    public bool IsVerified { get; set; }
    public string? PhoneNumber { get; set; }
    public TimeSpan? OpeningTime { get; set; }
    public TimeSpan? ClosingTime { get; set; }
    public bool IsCurrentlyOpen { get; set; }
    public string Address { get; set; } = string.Empty;
    public List<string> Specialties { get; set; } = new();
    public string MarkerColor { get; set; } = "#FF6B6B"; // Default red color
    public string MarkerIcon { get; set; } = "restaurant"; // Default icon
}