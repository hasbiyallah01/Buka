using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AmalaSpotLocator.Models;

public class User
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string PasswordHash { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? PhoneNumber { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastLoginAt { get; set; }

    public bool IsActive { get; set; } = true;

    [Required]
    public UserRole Role { get; set; } = UserRole.User;

    [MaxLength(10)]
    public string PreferredLanguage { get; set; } = "en";

    [Column(TypeName = "decimal(10,2)")]
    public decimal? PreferredMaxBudget { get; set; }

    [Column(TypeName = "decimal(3,2)")]
    [Range(0, 5)]
    public decimal? PreferredMinRating { get; set; }

    public ICollection<Review> Reviews { get; set; } = new List<Review>();
    public ICollection<AmalaSpot> CreatedSpots { get; set; } = new List<AmalaSpot>();
}

public enum UserRole
{
    User = 1,
    Moderator = 2,
    Admin = 3
}