using AmalaSpotLocator.Core.Domain.Entities;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AmalaSpotLocator.Core.Applications.Interfaces.Services;

public interface IWebScrapingService
{

    Task<List<SpotCandidate>> ScrapeWebsiteAsync(string url, CancellationToken cancellationToken = default);

    Task<List<SpotCandidate>> ScrapeConfiguredWebsitesAsync(CancellationToken cancellationToken = default);

    Task<List<SpotCandidate>> ExtractSpotsFromHtmlAsync(string html, string sourceUrl, CancellationToken cancellationToken = default);

    Task<bool> ValidateAmalaSpotCandidateAsync(SpotCandidate candidate, CancellationToken cancellationToken = default);
}

public class ScrapedData
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Address { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Website { get; set; }
    public List<string> Images { get; set; } = new();
    public Dictionary<string, string> AdditionalData { get; set; } = new();
    public double ConfidenceScore { get; set; }
}