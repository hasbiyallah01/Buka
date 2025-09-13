namespace AmalaSpotLocator.Models.SpotModel;

public class SpotResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Address { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? PhoneNumber { get; set; }
    public TimeSpan? OpeningTime { get; set; }
    public TimeSpan? ClosingTime { get; set; }
    public decimal AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public PriceRange PriceRange { get; set; }
    public List<string> Specialties { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsVerified { get; set; }
    public double? DistanceKm { get; set; }
    public List<ReviewResponse>? Reviews { get; set; }
}