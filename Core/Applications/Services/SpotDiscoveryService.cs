using AmalaSpotLocator.Configuration;
using AmalaSpotLocator.Core.Applications.Interfaces.Services;
using AmalaSpotLocator.Core.Domain.Entities;
using AmalaSpotLocator.Data;
using AmalaSpotLocator.Interfaces;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NetTopologySuite.Geometries;
using AmalaSpotLocator.Models;

namespace AmalaSpotLocator.Core.Applications.Services;

public class SpotDiscoveryService : ISpotDiscoveryService
{
    private readonly AmalaSpotContext _context;
    private readonly DiscoverySettings _settings;
    private readonly IWebScrapingService _webScrapingService;
    private readonly ICandidateExtractionService _candidateExtractionService;
    private readonly ISpotService _spotService;
    private readonly IGeospatialService _geospatialService;
    private readonly ILogger<SpotDiscoveryService> _logger;

    public SpotDiscoveryService(
        AmalaSpotContext context,
        IOptions<DiscoverySettings> settings,
        IWebScrapingService webScrapingService,
        ICandidateExtractionService candidateExtractionService,
        ISpotService spotService,
        IGeospatialService geospatialService,
        ILogger<SpotDiscoveryService> logger)
    {
        _context = context;
        _settings = settings.Value;
        _webScrapingService = webScrapingService;
        _candidateExtractionService = candidateExtractionService;
        _spotService = spotService;
        _geospatialService = geospatialService;
        _logger = logger;
    }

    public async Task<DiscoveryResult> RunDiscoveryAsync(CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var result = new DiscoveryResult();

        try
        {
            _logger.LogInformation("Starting autonomous spot discovery pipeline");

            if (!_settings.Enabled)
            {
                _logger.LogInformation("Autonomous discovery is disabled");
                return result;
            }

            var allCandidates = new List<SpotCandidate>();

            try
            {
                var webCandidates = await DiscoverFromWebScrapingAsync(cancellationToken);
                allCandidates.AddRange(webCandidates);
                result.SourceBreakdown[DiscoverySource.WebScraping] = webCandidates.Count;
                _logger.LogInformation("Discovered {Count} candidates from web scraping", webCandidates.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in web scraping discovery: {Error}", ex.Message);
                result.Errors.Add($"Web scraping error: {ex.Message}");
            }

            try
            {
                var googleCandidates = await DiscoverFromGooglePlacesAsync(cancellationToken);
                allCandidates.AddRange(googleCandidates);
                result.SourceBreakdown[DiscoverySource.GooglePlaces] = googleCandidates.Count;
                _logger.LogInformation("Discovered {Count} candidates from Google Places", googleCandidates.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Google Places discovery: {Error}", ex.Message);
                result.Errors.Add($"Google Places error: {ex.Message}");
            }

            try
            {
                var socialCandidates = await DiscoverFromSocialMediaAsync(cancellationToken);
                allCandidates.AddRange(socialCandidates);
                result.SourceBreakdown[DiscoverySource.SocialMedia] = socialCandidates.Count;
                _logger.LogInformation("Discovered {Count} candidates from social media", socialCandidates.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in social media discovery: {Error}", ex.Message);
                result.Errors.Add($"Social media error: {ex.Message}");
            }

            result.TotalCandidatesFound = allCandidates.Count;

            _logger.LogInformation("Total candidates before filtering: {Total}. Confidence scores: {Scores}", 
                allCandidates.Count, 
                string.Join(", ", allCandidates.Select(c => $"{c.Name}:{c.ConfidenceScore:F2}")));

            var filteredCandidates = allCandidates
                .Where(c => c.ConfidenceScore >= _settings.MinConfidenceScore)
                .Take(_settings.MaxCandidatesPerRun)
                .ToList();

            _logger.LogInformation("Filtered to {Count} candidates above confidence threshold {Threshold}", 
                filteredCandidates.Count, _settings.MinConfidenceScore);

            foreach (var candidate in filteredCandidates)
            {
                try
                {

                    var enrichedCandidate = await EnrichCandidateAsync(candidate, cancellationToken);
                    result.CandidatesEnriched++;

                    var verifiedCandidate = await VerifyCandidateAsync(enrichedCandidate, cancellationToken);
                    result.CandidatesVerified++;

                    var scoredCandidate = await ScoreCandidateAsync(verifiedCandidate, cancellationToken);

                    await SaveCandidateAsync(scoredCandidate, cancellationToken);

                    if (scoredCandidate.QualityScore >= _settings.AutoApprovalQualityThreshold &&
                        scoredCandidate.Status == CandidateStatus.Verified)
                    {
                        try
                        {
                            await ApproveCandidateAsync(scoredCandidate.Id, null, cancellationToken);
                            result.CandidatesApproved++;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to auto-approve candidate {CandidateName}: {Error}", 
                                scoredCandidate.Name, ex.Message);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing candidate {CandidateName}: {Error}", 
                        candidate.Name, ex.Message);
                    result.Errors.Add($"Processing error for {candidate.Name}: {ex.Message}");
                }
            }

            result.Duration = DateTime.UtcNow - startTime;
            _logger.LogInformation("Discovery pipeline completed in {Duration}. Found {Total}, Enriched {Enriched}, Verified {Verified}, Approved {Approved}",
                result.Duration, result.TotalCandidatesFound, result.CandidatesEnriched, result.CandidatesVerified, result.CandidatesApproved);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical error in discovery pipeline: {Error}", ex.Message);
            result.Errors.Add($"Critical error: {ex.Message}");
            result.Duration = DateTime.UtcNow - startTime;
            return result;
        }
    }

    public async Task<List<SpotCandidate>> DiscoverFromWebScrapingAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting web scraping discovery");
            return await _webScrapingService.ScrapeConfiguredWebsitesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in web scraping discovery: {Error}", ex.Message);
            return new List<SpotCandidate>();
        }
    }

    public async Task<List<SpotCandidate>> DiscoverFromGooglePlacesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting Google Places discovery");
            return await _candidateExtractionService.ExtractFromGooglePlacesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Google Places discovery: {Error}", ex.Message);
            return new List<SpotCandidate>();
        }
    }

    public async Task<List<SpotCandidate>> DiscoverFromSocialMediaAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting social media discovery");
            return await _candidateExtractionService.ExtractFromSocialMediaAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in social media discovery: {Error}", ex.Message);
            return new List<SpotCandidate>();
        }
    }

    public async Task<SpotCandidate> EnrichCandidateAsync(SpotCandidate candidate, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Enriching candidate: {CandidateName}", candidate.Name);
            
            candidate.Status = CandidateStatus.Enriching;
            var enrichedCandidate = await _candidateExtractionService.EnrichCandidateDataAsync(candidate, cancellationToken);
            
            enrichedCandidate.Status = CandidateStatus.Enriched;
            return enrichedCandidate;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enriching candidate {CandidateName}: {Error}", candidate.Name, ex.Message);
            candidate.Status = CandidateStatus.Discovered; // Reset status on error
            throw;
        }
    }

    public async Task<SpotCandidate> VerifyCandidateAsync(SpotCandidate candidate, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Verifying candidate: {CandidateName}", candidate.Name);
            
            candidate.Status = CandidateStatus.Verifying;

            var validationResult = await _candidateExtractionService.ValidateCandidateAsync(candidate, cancellationToken);
            
            if (!validationResult.IsValid)
            {
                candidate.Status = CandidateStatus.Rejected;
                candidate.VerificationNotes = $"Validation failed: {string.Join(", ", validationResult.Errors)}";
                candidate.VerifiedAt = DateTime.UtcNow;
                return candidate;
            }

            var isDuplicate = await CheckForDuplicateSpotAsync(candidate, cancellationToken);
            if (isDuplicate.HasValue)
            {
                candidate.Status = CandidateStatus.Duplicate;
                candidate.ExistingSpotId = isDuplicate.Value;
                candidate.VerificationNotes = "Duplicate of existing spot";
                candidate.VerifiedAt = DateTime.UtcNow;
                return candidate;
            }

            var duplicateCandidate = await CheckForDuplicateCandidateAsync(candidate, cancellationToken);
            if (duplicateCandidate != null)
            {
                candidate.Status = CandidateStatus.Duplicate;
                candidate.VerificationNotes = $"Duplicate of existing candidate: {duplicateCandidate.Name}";
                candidate.VerifiedAt = DateTime.UtcNow;
                return candidate;
            }

            candidate.Status = CandidateStatus.Verified;
            candidate.VerificationNotes = validationResult.Warnings.Any() 
                ? $"Verified with warnings: {string.Join(", ", validationResult.Warnings)}"
                : "Verified successfully";
            candidate.VerifiedAt = DateTime.UtcNow;

            return candidate;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying candidate {CandidateName}: {Error}", candidate.Name, ex.Message);
            candidate.Status = CandidateStatus.Enriched; // Reset to previous status on error
            throw;
        }
    }

    public async Task<SpotCandidate> ScoreCandidateAsync(SpotCandidate candidate, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Scoring candidate: {CandidateName}", candidate.Name);

            return await _candidateExtractionService.ProcessCandidateAsync(candidate, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scoring candidate {CandidateName}: {Error}", candidate.Name, ex.Message);
            throw;
        }
    }

    public async Task<IEnumerable<SpotCandidate>> GetCandidatesAsync(CandidateFilter? filter = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _context.SpotCandidates.AsQueryable();

            if (filter != null)
            {
                if (filter.Status.HasValue)
                    query = query.Where(c => c.Status == filter.Status.Value);

                if (filter.Source.HasValue)
                    query = query.Where(c => c.Source == filter.Source.Value);

                if (filter.MinConfidenceScore.HasValue)
                    query = query.Where(c => c.ConfidenceScore >= filter.MinConfidenceScore.Value);

                if (filter.MinQualityScore.HasValue)
                    query = query.Where(c => c.QualityScore >= filter.MinQualityScore.Value);

                if (filter.DiscoveredAfter.HasValue)
                    query = query.Where(c => c.DiscoveredAt >= filter.DiscoveredAfter.Value);

                if (filter.DiscoveredBefore.HasValue)
                    query = query.Where(c => c.DiscoveredAt <= filter.DiscoveredBefore.Value);

                query = query.Skip(filter.Offset).Take(filter.Limit);
            }

            return await query
                .OrderByDescending(c => c.QualityScore)
                .ThenByDescending(c => c.ConfidenceScore)
                .ThenByDescending(c => c.DiscoveredAt)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting candidates: {Error}", ex.Message);
            throw;
        }
    }

    public async Task<AmalaSpot> ApproveCandidateAsync(Guid candidateId, Guid? approvedByUserId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var candidate = await _context.SpotCandidates.FindAsync(candidateId);
            if (candidate == null)
                throw new ArgumentException($"Candidate with ID {candidateId} not found");

            if (candidate.Status == CandidateStatus.Approved)
                throw new InvalidOperationException("Candidate is already approved");

            if (candidate.Status == CandidateStatus.Rejected)
                throw new InvalidOperationException("Cannot approve a rejected candidate");

            _logger.LogInformation("Approving candidate: {CandidateName}", candidate.Name);

            var spot = new AmalaSpot
            {
                Name = candidate.Name,
                Description = candidate.Description,
                Address = candidate.Address,
                Location = candidate.Location!,
                PhoneNumber = candidate.PhoneNumber,
                OpeningTime = candidate.OpeningTime,
                ClosingTime = candidate.ClosingTime,
                PriceRange = candidate.EstimatedPriceRange,
                Specialties = candidate.Specialties,
                CreatedByUserId = approvedByUserId,
                IsVerified = candidate.QualityScore >= 0.8, // High quality candidates are auto-verified
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var createdSpot = await _spotService.CreateAsync(spot);

            candidate.Status = CandidateStatus.Approved;
            candidate.ExistingSpotId = createdSpot.Id;
            candidate.ProcessedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Successfully approved candidate {CandidateName} and created spot {SpotId}", 
                candidate.Name, createdSpot.Id);

            return createdSpot;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving candidate {CandidateId}: {Error}", candidateId, ex.Message);
            throw;
        }
    }

    public async Task RejectCandidateAsync(Guid candidateId, string reason, Guid? rejectedByUserId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var candidate = await _context.SpotCandidates.FindAsync(candidateId);
            if (candidate == null)
                throw new ArgumentException($"Candidate with ID {candidateId} not found");

            if (candidate.Status == CandidateStatus.Approved)
                throw new InvalidOperationException("Cannot reject an approved candidate");

            _logger.LogInformation("Rejecting candidate: {CandidateName} with reason: {Reason}", candidate.Name, reason);

            candidate.Status = CandidateStatus.Rejected;
            candidate.VerificationNotes = reason;
            candidate.ProcessedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Successfully rejected candidate {CandidateName}", candidate.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting candidate {CandidateId}: {Error}", candidateId, ex.Message);
            throw;
        }
    }

    public async Task<DiscoveryMetrics> GetDiscoveryMetricsAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _context.SpotCandidates.AsQueryable();

            if (fromDate.HasValue)
                query = query.Where(c => c.DiscoveredAt >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(c => c.DiscoveredAt <= toDate.Value);

            var candidates = await query.ToListAsync(cancellationToken);

            var metrics = new DiscoveryMetrics
            {
                TotalCandidates = candidates.Count,
                ApprovedCandidates = candidates.Count(c => c.Status == CandidateStatus.Approved),
                RejectedCandidates = candidates.Count(c => c.Status == CandidateStatus.Rejected),
                PendingCandidates = candidates.Count(c => c.Status != CandidateStatus.Approved && c.Status != CandidateStatus.Rejected),
                AverageConfidenceScore = candidates.Any() ? candidates.Average(c => c.ConfidenceScore) : 0,
                AverageQualityScore = candidates.Any() ? candidates.Average(c => c.QualityScore) : 0,
                SourceDistribution = candidates.GroupBy(c => c.Source).ToDictionary(g => g.Key, g => g.Count()),
                StatusDistribution = candidates.GroupBy(c => c.Status).ToDictionary(g => g.Key, g => g.Count()),
                LastDiscoveryRun = candidates.Any() ? candidates.Max(c => c.DiscoveredAt) : null,
                DiscoveryRunsToday = candidates.Count(c => c.DiscoveredAt.Date == DateTime.Today)
            };

            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting discovery metrics: {Error}", ex.Message);
            throw;
        }
    }

    private async Task<Guid?> CheckForDuplicateSpotAsync(SpotCandidate candidate, CancellationToken cancellationToken)
    {
        try
        {
            if (candidate.Location == null)
                return null;

            var nearbySpots = await _geospatialService.FindNearbySpots(
                new Models.Location { Latitude = candidate.Location.Y, Longitude = candidate.Location.X },
                0.1, // 100 meters
                10);

            foreach (var spot in nearbySpots)
            {
                if (IsNameSimilar(candidate.Name, spot.Name))
                {
                    return spot.Id;
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking for duplicate spots: {Error}", ex.Message);
            return null;
        }
    }

    private async Task<SpotCandidate?> CheckForDuplicateCandidateAsync(SpotCandidate candidate, CancellationToken cancellationToken)
    {
        try
        {
            if (candidate.Location == null)
                return null;

            var point = candidate.Location;
            const double radiusMeters = 100;

            var nearbyCandidates = await _context.SpotCandidates
                .Where(c => c.Id != candidate.Id && 
                           c.Location != null && 
                           c.Location.Distance(point) <= radiusMeters)
                .ToListAsync(cancellationToken);

            return nearbyCandidates.FirstOrDefault(c => IsNameSimilar(candidate.Name, c.Name));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking for duplicate candidates: {Error}", ex.Message);
            return null;
        }
    }

    private async Task SaveCandidateAsync(SpotCandidate candidate, CancellationToken cancellationToken)
    {
        try
        {
            var existingCandidate = await _context.SpotCandidates
                .FirstOrDefaultAsync(c => c.Id == candidate.Id, cancellationToken);

            if (existingCandidate == null)
            {
                _context.SpotCandidates.Add(candidate);
            }
            else
            {
                _context.Entry(existingCandidate).CurrentValues.SetValues(candidate);
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving candidate {CandidateName}: {Error}", candidate.Name, ex.Message);
            throw;
        }
    }

    private bool IsNameSimilar(string name1, string name2)
    {
        if (string.IsNullOrWhiteSpace(name1) || string.IsNullOrWhiteSpace(name2))
            return false;

        var normalized1 = name1.ToLowerInvariant().Trim();
        var normalized2 = name2.ToLowerInvariant().Trim();

        if (normalized1 == normalized2)
            return true;

        if (normalized1.Contains(normalized2) || normalized2.Contains(normalized1))
            return true;

        var distance = LevenshteinDistance(normalized1, normalized2);
        var maxLength = Math.Max(normalized1.Length, normalized2.Length);
        var similarity = 1.0 - (double)distance / maxLength;

        return similarity >= 0.8; // 80% similarity threshold
    }

    private int LevenshteinDistance(string s1, string s2)
    {
        if (string.IsNullOrEmpty(s1)) return s2?.Length ?? 0;
        if (string.IsNullOrEmpty(s2)) return s1.Length;

        var matrix = new int[s1.Length + 1, s2.Length + 1];

        for (int i = 0; i <= s1.Length; i++)
            matrix[i, 0] = i;
        for (int j = 0; j <= s2.Length; j++)
            matrix[0, j] = j;

        for (int i = 1; i <= s1.Length; i++)
        {
            for (int j = 1; j <= s2.Length; j++)
            {
                int cost = s1[i - 1] == s2[j - 1] ? 0 : 1;
                matrix[i, j] = Math.Min(Math.Min(
                    matrix[i - 1, j] + 1,
                    matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost);
            }
        }

        return matrix[s1.Length, s2.Length];
    }
}