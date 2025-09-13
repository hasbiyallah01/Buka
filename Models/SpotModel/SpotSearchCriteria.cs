using System.ComponentModel.DataAnnotations;

namespace AmalaSpotLocator.Models.SpotModel;

public class SpotSearchCriteria
{

    public Location? Location { get; set; }

    [Range(0.1, 1000)]
    public double? RadiusKm { get; set; }

    public TimeSpan? CurrentTime { get; set; }

    public string? SearchTerm { get; set; }

    [Range(1, 1000)]
    public int Limit { get; set; } = 50;

    [Range(0, int.MaxValue)]
    public int Offset { get; set; } = 0;

    [Range(0, 5)]
    public decimal? MinRating { get; set; }

    [Range(0, 5)]
    public decimal? MaxRating { get; set; }

    public PriceRange? MinPriceRange { get; set; }

    public PriceRange? MaxPriceRange { get; set; }

    public bool? IsVerified { get; set; }

    public bool? IsCurrentlyOpen { get; set; }

    public List<string>? Specialties { get; set; }

    public string? Query { get; set; }

    [Range(0.1, 1000)]
    public double? MaxDistanceKm { get; set; }

    public SpotSortOrder SortOrder { get; set; } = SpotSortOrder.Distance;

    [Range(1, 1000)]
    public int MaxResults { get; set; } = 50;
}

public enum SpotSortOrder
{
    Distance = 0,
    Rating = 1,
    ReviewCount = 2,
    Name = 3,
    CreatedDate = 4,
    UpdatedDate = 5
}