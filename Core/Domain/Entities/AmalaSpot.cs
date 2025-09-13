using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NetTopologySuite.Geometries;

namespace AmalaSpotLocator.Models;

public class AmalaSpot
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    [MaxLength(500)]
    public string Address { get; set; } = string.Empty;

    [Required]
    [Column(TypeName = "geography (point)")]
    public Point Location { get; set; } = null!;

    [MaxLength(20)]
    public string? PhoneNumber { get; set; }

    public TimeSpan? OpeningTime { get; set; }

    public TimeSpan? ClosingTime { get; set; }

    [Column(TypeName = "decimal(3,2)")]
    [Range(0, 5)]
    public decimal AverageRating { get; set; } = 0;

    public int ReviewCount { get; set; } = 0;

    [Required]
    public PriceRange PriceRange { get; set; } = PriceRange.Budget;

    [Column(TypeName = "jsonb")]
    public List<string> Specialties { get; set; } = new();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public bool IsVerified { get; set; } = false;

    public Guid? CreatedByUserId { get; set; }

    public User? CreatedByUser { get; set; }
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
}

public enum PriceRange
{
    Free = 0,        // Free
    Budget = 1,      // Under ₦1000
    Moderate = 2,    // ₦1000 - ₦2500
    Expensive = 3,   // ₦2500 - ₦5000
    VeryExpensive = 4 // Above ₦5000
}