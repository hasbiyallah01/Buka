using AmalaSpotLocator.Core.Domain.Entities;
using System.Collections.Generic;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AmalaSpotLocator.Models;

namespace AmalaSpotLocator.Core.Applications.Interfaces.Services;

public interface ISpotDiscoveryService
{

    Task<DiscoveryResult> RunDiscoveryAsync(CancellationToken cancellationToken = default);

    Task<List<SpotCandidate>> DiscoverFromWebScrapingAsync(CancellationToken cancellationToken = default);

    Task<List<SpotCandidate>> DiscoverFromGooglePlacesAsync(CancellationToken cancellationToken = default);

    Task<List<SpotCandidate>> DiscoverFromSocialMediaAsync(CancellationToken cancellationToken = default);

    Task<SpotCandidate> EnrichCandidateAsync(SpotCandidate candidate, CancellationToken cancellationToken = default);

    Task<SpotCandidate> VerifyCandidateAsync(SpotCandidate candidate, CancellationToken cancellationToken = default);

    Task<SpotCandidate> ScoreCandidateAsync(SpotCandidate candidate, CancellationToken cancellationToken = default);

    Task<IEnumerable<SpotCandidate>> GetCandidatesAsync(CandidateFilter? filter = null, CancellationToken cancellationToken = default);

    Task<AmalaSpot> ApproveCandidateAsync(Guid candidateId, Guid? approvedByUserId = null, CancellationToken cancellationToken = default);

    Task RejectCandidateAsync(Guid candidateId, string reason, Guid? rejectedByUserId = null, CancellationToken cancellationToken = default);

    Task<DiscoveryMetrics> GetDiscoveryMetricsAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default);
}

public class CandidateFilter
{
    public CandidateStatus? Status { get; set; }
    public DiscoverySource? Source { get; set; }
    public double? MinConfidenceScore { get; set; }
    public double? MinQualityScore { get; set; }
    public DateTime? DiscoveredAfter { get; set; }
    public DateTime? DiscoveredBefore { get; set; }
    public int Limit { get; set; } = 50;
    public int Offset { get; set; } = 0;
}

public class DiscoveryResult
{
    public int TotalCandidatesFound { get; set; }
    public int CandidatesEnriched { get; set; }
    public int CandidatesVerified { get; set; }
    public int CandidatesApproved { get; set; }
    public int CandidatesRejected { get; set; }
    public int DuplicatesFound { get; set; }
    public TimeSpan Duration { get; set; }
    public List<string> Errors { get; set; } = new();
    public Dictionary<DiscoverySource, int> SourceBreakdown { get; set; } = new();
}

public class DiscoveryMetrics
{
    public int TotalCandidates { get; set; }
    public int ApprovedCandidates { get; set; }
    public int RejectedCandidates { get; set; }
    public int PendingCandidates { get; set; }
    public double AverageConfidenceScore { get; set; }
    public double AverageQualityScore { get; set; }
    public Dictionary<DiscoverySource, int> SourceDistribution { get; set; } = new();
    public Dictionary<CandidateStatus, int> StatusDistribution { get; set; } = new();
    public DateTime? LastDiscoveryRun { get; set; }
    public int DiscoveryRunsToday { get; set; }
}