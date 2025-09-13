namespace AmalaSpotLocator.Models;

public class SpotCluster
{
    public Location Center { get; set; } = null!;
    public List<AmalaSpot> Spots { get; set; } = new();
    public int Count => Spots.Count;
    public decimal AverageRating => Spots.Any() ? Spots.Average(s => s.AverageRating) : 0;
    public PriceRange? DominantPriceRange => Spots.GroupBy(s => s.PriceRange)
        .OrderByDescending(g => g.Count())
        .FirstOrDefault()?.Key;

    public SpotCluster(Location center)
    {
        Center = center;
    }

    public void AddSpot(AmalaSpot spot)
    {
        Spots.Add(spot);

        if (Spots.Count > 1)
        {
            var avgLat = Spots.Average(s => s.Location.Y);
            var avgLng = Spots.Average(s => s.Location.X);
            Center = new Location(avgLat, avgLng);
        }
    }
}