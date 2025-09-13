using System.ComponentModel.DataAnnotations;

namespace AmalaSpotLocator.Models;

public class Location
{
    [Required]
    [Range(-90, 90)]
    public double Latitude { get; set; }

    [Required]
    [Range(-180, 180)]
    public double Longitude { get; set; }

    public Location() { }

    public Location(double latitude, double longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
    }

    public override string ToString()
    {
        return $"{Latitude}, {Longitude}";
    }

    public override bool Equals(object? obj)
    {
        if (obj is Location other)
        {
            return Math.Abs(Latitude - other.Latitude) < 0.000001 && 
                   Math.Abs(Longitude - other.Longitude) < 0.000001;
        }
        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Latitude, Longitude);
    }
}