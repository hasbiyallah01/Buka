using System.ComponentModel.DataAnnotations;
using AmalaSpotLocator.Attributes;

namespace AmalaSpotLocator.Models.SpotModel;

public class CreateSpotRequest
{
    [Required]
    [MaxLength(200)]
    [SafeName]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    [SafeString]
    public string? Description { get; set; }

    [Required]
    [MaxLength(500)]
    [SafeString]
    public string Address { get; set; } = string.Empty;

    [Required]
    [Latitude]
    public double Latitude { get; set; }

    [Required]
    [Longitude]
    public double Longitude { get; set; }

    [MaxLength(20)]
    [NigerianPhone]
    public string? PhoneNumber { get; set; }

    public TimeSpan? OpeningTime { get; set; }

    public TimeSpan? ClosingTime { get; set; }

    [Required]
    public PriceRange PriceRange { get; set; }

    [MaxItems(10)]
    public List<string> Specialties { get; set; } = new();
}