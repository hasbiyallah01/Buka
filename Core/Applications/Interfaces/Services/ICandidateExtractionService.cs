using AmalaSpotLocator.Core.Domain.Entities;

namespace AmalaSpotLocator.Core.Applications.Interfaces.Services;

public interface ICandidateExtractionService
{

    Task<List<SpotCandidate>> ExtractFromGooglePlacesAsync(CancellationToken cancellationToken = default);

    Task<List<SpotCandidate>> ExtractFromSocialMediaAsync(CancellationToken cancellationToken = default);

    Task<SpotCandidate> ProcessCandidateAsync(SpotCandidate candidate, CancellationToken cancellationToken = default);

    Task<ValidationResult> ValidateCandidateAsync(SpotCandidate candidate, CancellationToken cancellationToken = default);

    Task<SpotCandidate> EnrichCandidateDataAsync(SpotCandidate candidate, CancellationToken cancellationToken = default);
}

public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public double QualityScore { get; set; }
}