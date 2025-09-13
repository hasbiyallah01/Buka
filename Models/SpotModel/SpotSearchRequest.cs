using System.ComponentModel.DataAnnotations;

namespace AmalaSpotLocator.Models.SpotModel;

public class SpotSearchRequest
{
    [Range(-90, 90)]
    public double? Latitude { get; set; }

    [Range(-180, 180)]
    public double? Longitude { get; set; }

    [Range(0.1, 100)]
    public double? RadiusKm { get; set; }

    public PriceRange? MinPriceRange { get; set; }

    public PriceRange? MaxPriceRange { get; set; }

    [Range(0, 5)]
    public decimal? MinRating { get; set; }

    public bool? IsVerified { get; set; }

    public List<string>? Specialties { get; set; }

    [MaxLength(100)]
    public string? SearchTerm { get; set; }

    public bool? IsCurrentlyOpen { get; set; }

    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}