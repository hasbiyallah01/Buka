using System.ComponentModel.DataAnnotations;

namespace AmalaSpotLocator.Models.MapModel;

public class MapDataResponse
{

    public List<MapMarker> Markers { get; set; } = new();

    public List<ClusterMarker> Clusters { get; set; } = new();

    public int TotalSpots { get; set; }

    public bool IsClustered { get; set; }

    public int SuggestedZoomLevel { get; set; }

    public string? StaticMapUrl { get; set; }

    public string? EmbedMapUrl { get; set; }
}

public class MapMarker
{

    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public decimal Rating { get; set; }

    public int ReviewCount { get; set; }

    public PriceRange PriceRange { get; set; }

    public bool IsVerified { get; set; }

    public bool? IsCurrentlyOpen { get; set; }

    public string Address { get; set; } = string.Empty;

    public string MarkerColor { get; set; } = "red";

    public string MarkerIcon { get; set; } = "restaurant";
}

public class ClusterMarker
{

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public int SpotCount { get; set; }

    public decimal AverageRating { get; set; }

    public PriceRange? DominantPriceRange { get; set; }

    public List<Guid> SpotIds { get; set; } = new();

    public double RadiusKm { get; set; }

    public string MarkerColor { get; set; } = "blue";

    public List<string> SpotNames { get; set; } = new();
}

public class GoogleMapsUrlRequest
{

    [Required]
    public Location Center { get; set; } = null!;

    [Range(1, 20)]
    public int ZoomLevel { get; set; } = 15;

    [Range(100, 2048)]
    public int Width { get; set; } = 600;

    [Range(100, 2048)]
    public int Height { get; set; } = 400;

    public List<MapMarker>? Markers { get; set; }

    public string MapType { get; set; } = "roadmap";
}