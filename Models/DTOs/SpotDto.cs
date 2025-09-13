using AmalaSpotLocator.Models;

namespace AmalaSpotLocator.Models.DTOs;

public class SpotDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Address { get; set; } = string.Empty;
    public LocationDto Location { get; set; } = new();
    public string? PhoneNumber { get; set; }
    public string? OpeningTime { get; set; }
    public string? ClosingTime { get; set; }
    public decimal AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public PriceRange PriceRange { get; set; }
    public List<string> Specialties { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsVerified { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public double? DistanceKm { get; set; }
}

public class LocationDto
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}