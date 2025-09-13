using System.ComponentModel.DataAnnotations;

namespace AmalaSpotLocator.Models.MapModel;

public class MapDataRequest
{

    [Required]
    public MapBounds Bounds { get; set; } = null!;

    [Range(1, 20)]
    public int ZoomLevel { get; set; } = 10;

    public PriceRange? MinPriceRange { get; set; }

    public PriceRange? MaxPriceRange { get; set; }

    [Range(0, 5)]
    public decimal? MinRating { get; set; }

    public bool? IsVerified { get; set; }

    public bool? IsCurrentlyOpen { get; set; }

    public List<string>? Specialties { get; set; }

    [Range(1, 1000)]
    public int MaxResults { get; set; } = 100;

    public bool EnableClustering { get; set; } = true;

    [Range(0.1, 10)]
    public double ClusterRadiusKm { get; set; } = 0.5;
}

public class MapBounds
{

    [Required]
    [Range(-90, 90)]
    public double SouthWestLat { get; set; }

    [Required]
    [Range(-180, 180)]
    public double SouthWestLng { get; set; }

    [Required]
    [Range(-90, 90)]
    public double NorthEastLat { get; set; }

    [Required]
    [Range(-180, 180)]
    public double NorthEastLng { get; set; }

    public Location GetCenter()
    {
        return new Location(
            (SouthWestLat + NorthEastLat) / 2,
            (SouthWestLng + NorthEastLng) / 2
        );
    }

    public double GetDiagonalDistanceKm()
    {
        var sw = new Location(SouthWestLat, SouthWestLng);
        var ne = new Location(NorthEastLat, NorthEastLng);

        const double earthRadiusKm = 6371.0;
        var lat1Rad = sw.Latitude * Math.PI / 180.0;
        var lat2Rad = ne.Latitude * Math.PI / 180.0;
        var deltaLatRad = (ne.Latitude - sw.Latitude) * Math.PI / 180.0;
        var deltaLngRad = (ne.Longitude - sw.Longitude) * Math.PI / 180.0;

        var a = Math.Sin(deltaLatRad / 2) * Math.Sin(deltaLatRad / 2) +
                Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                Math.Sin(deltaLngRad / 2) * Math.Sin(deltaLngRad / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        
        return earthRadiusKm * c;
    }

    public bool Contains(Location location)
    {
        return location.Latitude >= SouthWestLat && location.Latitude <= NorthEastLat &&
               location.Longitude >= SouthWestLng && location.Longitude <= NorthEastLng;
    }
}