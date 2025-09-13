using System.ComponentModel.DataAnnotations;

namespace AmalaSpotLocator.Models.SpotModel;

public class UpdateSpotRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    [MaxLength(500)]
    public string Address { get; set; } = string.Empty;

    [Required]
    [Range(-90, 90)]
    public double Latitude { get; set; }

    [Required]
    [Range(-180, 180)]
    public double Longitude { get; set; }

    [MaxLength(20)]
    public string? PhoneNumber { get; set; }

    public TimeSpan? OpeningTime { get; set; }

    public TimeSpan? ClosingTime { get; set; }

    [Required]
    public PriceRange PriceRange { get; set; }

    public List<string> Specialties { get; set; } = new();
}