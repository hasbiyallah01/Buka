using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using NetTopologySuite.Geometries;
using AmalaSpotLocator.Models;

namespace AmalaSpotLocator.Core.Domain.Entities;

public class SpotCandidate
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

    public Point? Location { get; set; }

    [MaxLength(20)]
    public string? PhoneNumber { get; set; }

    public TimeSpan? OpeningTime { get; set; }
    public TimeSpan? ClosingTime { get; set; }

    [Range(0.0, 1.0)]
    public double ConfidenceScore { get; set; }

    [Range(0.0, 1.0)]
    public double QualityScore { get; set; }

    public PriceRange EstimatedPriceRange { get; set; } = PriceRange.Budget;

    public List<string> Specialties { get; set; } = new();

    [Required]
    public DiscoverySource Source { get; set; }

    [MaxLength(1000)]
    public string? SourceUrl { get; set; }

    public Dictionary<string, object> SourceData { get; set; } = new();

    public CandidateStatus Status { get; set; } = CandidateStatus.Discovered;

    public string? VerificationNotes { get; set; }

    public Guid? ExistingSpotId { get; set; }

    public DateTime DiscoveredAt { get; set; } = DateTime.UtcNow;
    public DateTime? VerifiedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }

    public AmalaSpot? ExistingSpot { get; set; }
}

public enum DiscoverySource
{
    GooglePlaces = 1,
    WebScraping = 2,
    SocialMedia = 3,
    DirectoryListing = 4,
    UserSubmission = 5,
    ManualEntry = 6
}

public enum CandidateStatus
{
    Discovered = 1,
    Enriching = 2,
    Enriched = 3,
    Verifying = 4,
    Verified = 5,
    Approved = 6,
    Rejected = 7,
    Duplicate = 8,
    Processed = 9
}