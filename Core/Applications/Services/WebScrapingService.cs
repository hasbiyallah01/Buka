using AmalaSpotLocator.Configuration;
using AmalaSpotLocator.Core.Applications.Interfaces.Services;
using AmalaSpotLocator.Core.Domain.Entities;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using HtmlAgilityPack;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;
using IConfiguration = Microsoft.Extensions.Configuration.IConfiguration;

namespace AmalaSpotLocator.Core.Applications.Services;

public class WebScrapingService : IWebScrapingService
{
    private readonly DiscoverySettings _settings;
    private readonly ILogger<WebScrapingService> _logger;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;

    private readonly string[] _amalaKeywords = {
        "amala", "gbegiri", "ewedu", "abula", "yoruba food", "nigerian restaurant",
        "local food", "traditional food", "mama put", "buka"
    };

    private readonly Regex _phoneRegex = new(@"(\+234|0)[789]\d{9}", RegexOptions.Compiled);

    private readonly Regex _addressRegex = new(@"(?i)\b(?:lagos|ibadan|abeokuta|ilorin|ogbomoso|oyo|osogbo|ado ekiti|akure|ile ife)\b.*?(?:state|nigeria)?", RegexOptions.Compiled);

    public WebScrapingService(
        IOptions<DiscoverySettings> settings,
        ILogger<WebScrapingService> logger,
        HttpClient httpClient,
        IConfiguration configuration)
    {
        _settings = settings.Value;
        _logger = logger;
        _httpClient = httpClient;
        _config = configuration;

        _httpClient.DefaultRequestHeaders.Add("User-Agent", _settings.WebScraping.UserAgent);
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<List<SpotCandidate>> ScrapeConfiguredWebsitesAsync(CancellationToken cancellationToken = default)
    {
        var candidates = new List<SpotCandidate>();

        if (!_settings.WebScraping.Enabled)
        {
            _logger.LogInformation("Web scraping is disabled");
            return candidates;
        }

        foreach (var target in _settings.WebScraping.Targets.Where(t => t.Enabled))
        {
            try
            {
                _logger.LogInformation("Scraping target: {TargetName}", target.Name);
                
                var targetCandidates = await ScrapeTargetAsync(target, cancellationToken);
                candidates.AddRange(targetCandidates);

                await Task.Delay(_settings.WebScraping.RequestDelayMs, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scraping target {TargetName}: {Error}", target.Name, ex.Message);
            }
        }

        _logger.LogInformation("Web scraping completed. Found {Count} candidates", candidates.Count);
        return candidates;
    }

    public async Task<List<SpotCandidate>> ScrapeWebsiteAsync(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Scraping website: {Url}", url);

            var html = await FetchHtmlAsync(url, cancellationToken);
            if (string.IsNullOrEmpty(html))
            {
                return new List<SpotCandidate>();
            }

            return await ExtractSpotsFromHtmlAsync(html, url, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scraping website {Url}: {Error}", url, ex.Message);
            return new List<SpotCandidate>();
        }
    }

    public async Task<List<SpotCandidate>> ExtractSpotsFromHtmlAsync(string html, string sourceUrl, CancellationToken cancellationToken = default)
    {
        var candidates = new List<SpotCandidate>();

        try
        {

            var config = AngleSharp.Configuration.Default;
            var context = BrowsingContext.New(config);
            var document = await context.OpenAsync(req => req.Content(html), cancellationToken);

            var restaurantElements = document.QuerySelectorAll(
                "div[class*='restaurant'], div[class*='listing'], div[class*='business'], " +
                "article, .card, .item, [itemtype*='Restaurant'], [itemtype*='LocalBusiness']"
            );

            foreach (var element in restaurantElements)
            {
                var candidate = await ExtractCandidateFromElementAsync(element, sourceUrl);
                if (candidate != null && await ValidateAmalaSpotCandidateAsync(candidate, cancellationToken))
                {
                    candidates.Add(candidate);
                }
            }

            if (candidates.Count == 0)
            {
                candidates.AddRange(await ExtractFromTextPatternsAsync(html, sourceUrl));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting spots from HTML: {Error}", ex.Message);
        }

        return candidates;
    }

    public async Task<bool> ValidateAmalaSpotCandidateAsync(SpotCandidate candidate, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(candidate.Name))
            return false;

        var textToCheck = $"{candidate.Name} {candidate.Description}".ToLowerInvariant();
        
        var hasAmalaKeywords = _amalaKeywords.Any(keyword => textToCheck.Contains(keyword));
        if (!hasAmalaKeywords)
            return false;

        if (candidate.Name.Length < 3 || candidate.Name.Length > 200)
            return false;

        if (IsSpamContent(candidate.Name) || IsSpamContent(candidate.Description))
            return false;

        return true;
    }

    private async Task<List<SpotCandidate>> ScrapeTargetAsync(ScrapingTarget target, CancellationToken cancellationToken)
    {
        var candidates = new List<SpotCandidate>();
        var pagesScraped = 0;

        foreach (var searchUrl in target.SearchUrls)
        {
            if (pagesScraped >= _settings.WebScraping.MaxPagesPerSite)
                break;

            try
            {
                var html = await FetchHtmlAsync(searchUrl, cancellationToken);
                if (!string.IsNullOrEmpty(html))
                {
                    var pageCandidates = await ExtractSpotsFromHtmlAsync(html, searchUrl, cancellationToken);
                    candidates.AddRange(pageCandidates);
                    pagesScraped++;
                }

                await Task.Delay(_settings.WebScraping.RequestDelayMs, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error scraping URL {Url}: {Error}", searchUrl, ex.Message);
            }
        }

        return candidates;
    }

    private async Task<string?> FetchHtmlAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch HTML from {Url}: {Error}", url, ex.Message);
            return null;
        }
    }

    private async Task<SpotCandidate?> ExtractCandidateFromElementAsync(IElement element, string sourceUrl)
    {
        try
        {
            var name = ExtractName(element);
            if (string.IsNullOrWhiteSpace(name))
                return null;

            var candidate = new SpotCandidate
            {
                Name = name.Trim(),
                Description = ExtractDescription(element)?.Trim(),
                Address = ExtractAddress(element)?.Trim(),
                PhoneNumber = ExtractPhoneNumber(element)?.Trim(),
                Source = DiscoverySource.WebScraping,
                SourceUrl = sourceUrl,
                SourceData = new Dictionary<string, object>
                {
                    ["html"] = element.OuterHtml,
                    ["extractedAt"] = DateTime.UtcNow
                }
            };

            candidate.ConfidenceScore = CalculateConfidenceScore(candidate);

            return candidate;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error extracting candidate from element: {Error}", ex.Message);
            return null;
        }
    }

    private string? ExtractName(IElement element)
    {

        var nameSelectors = new[]
        {
            "h1", "h2", "h3", ".name", ".title", ".restaurant-name", 
            "[itemprop='name']", ".business-name", ".listing-title"
        };

        foreach (var selector in nameSelectors)
        {
            var nameElement = element.QuerySelector(selector);
            if (nameElement != null && !string.IsNullOrWhiteSpace(nameElement.TextContent))
            {
                return nameElement.TextContent.Trim();
            }
        }

        var textContent = element.TextContent?.Trim();
        if (!string.IsNullOrEmpty(textContent) && textContent.Length <= 100)
        {
            return textContent;
        }

        return null;
    }

    private string? ExtractDescription(IElement element)
    {
        var descriptionSelectors = new[]
        {
            ".description", ".summary", ".about", "[itemprop='description']",
            ".restaurant-description", ".business-description", "p"
        };

        foreach (var selector in descriptionSelectors)
        {
            var descElement = element.QuerySelector(selector);
            if (descElement != null && !string.IsNullOrWhiteSpace(descElement.TextContent))
            {
                var description = descElement.TextContent.Trim();
                if (description.Length > 20 && description.Length <= 1000)
                {
                    return description;
                }
            }
        }

        return null;
    }

    private string? ExtractAddress(IElement element)
    {
        var addressSelectors = new[]
        {
            ".address", "[itemprop='address']", ".location", ".venue",
            ".restaurant-address", ".business-address"
        };

        foreach (var selector in addressSelectors)
        {
            var addressElement = element.QuerySelector(selector);
            if (addressElement != null && !string.IsNullOrWhiteSpace(addressElement.TextContent))
            {
                return addressElement.TextContent.Trim();
            }
        }

        var allText = element.TextContent ?? "";
        var addressMatch = _addressRegex.Match(allText);
        if (addressMatch.Success)
        {
            return addressMatch.Value.Trim();
        }

        return null;
    }

    private string? ExtractPhoneNumber(IElement element)
    {
        var phoneSelectors = new[]
        {
            ".phone", "[itemprop='telephone']", ".contact", ".tel",
            "a[href^='tel:']", ".phone-number"
        };

        foreach (var selector in phoneSelectors)
        {
            var phoneElement = element.QuerySelector(selector);
            if (phoneElement != null)
            {
                var phoneText = phoneElement.GetAttribute("href")?.Replace("tel:", "") ?? phoneElement.TextContent;
                if (!string.IsNullOrWhiteSpace(phoneText))
                {
                    var phoneMatch = _phoneRegex.Match(phoneText);
                    if (phoneMatch.Success)
                    {
                        return phoneMatch.Value;
                    }
                }
            }
        }

        var allText = element.TextContent ?? "";
        var allTextPhoneMatch = _phoneRegex.Match(allText);
        if (allTextPhoneMatch.Success)
        {
            return allTextPhoneMatch.Value;
        }

        return null;
    }

    private async Task<List<SpotCandidate>> ExtractFromTextPatternsAsync(string html, string sourceUrl)
    {
        var candidates = new List<SpotCandidate>();

        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        var plainText = doc.DocumentNode.InnerText;

        var lines = plainText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var line in lines)
        {
            if (line.Length < 10 || line.Length > 500)
                continue;

            var lowerLine = line.ToLowerInvariant();
            if (!_amalaKeywords.Any(keyword => lowerLine.Contains(keyword)))
                continue;

            var candidate = new SpotCandidate
            {
                Name = ExtractNameFromLine(line),
                Description = line.Trim(),
                Address = ExtractAddressFromLine(line),
                PhoneNumber = ExtractPhoneFromLine(line),
                Source = DiscoverySource.WebScraping,
                SourceUrl = sourceUrl,
                ConfidenceScore = 0.4 
            };

            if (!string.IsNullOrWhiteSpace(candidate.Name))
            {
                candidates.Add(candidate);
            }
        }

        return candidates;
    }

    private string ExtractNameFromLine(string line)
    {

        var parts = line.Split(new[] { " - ", ", ", " | " }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 0)
        {
            var name = parts[0].Trim();
            if (name.Length >= 3 && name.Length <= 100)
            {
                return name;
            }
        }

        return line.Trim();
    }

    private string? ExtractAddressFromLine(string line)
    {
        var addressMatch = _addressRegex.Match(line);
        return addressMatch.Success ? addressMatch.Value.Trim() : null;
    }

    private string? ExtractPhoneFromLine(string line)
    {
        var phoneMatch = _phoneRegex.Match(line);
        return phoneMatch.Success ? phoneMatch.Value : null;
    }

    private double CalculateConfidenceScore(SpotCandidate candidate)
    {
        double score = 0.0;

        if (!string.IsNullOrWhiteSpace(candidate.Name))
            score += 0.3;

        var nameText = candidate.Name.ToLowerInvariant();
        if (_amalaKeywords.Any(keyword => nameText.Contains(keyword)))
            score += 0.4;

        if (!string.IsNullOrWhiteSpace(candidate.Address))
            score += 0.2;

        if (!string.IsNullOrWhiteSpace(candidate.PhoneNumber))
            score += 0.1;

        return Math.Min(1.0, score);
    }

    private bool IsSpamContent(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return false;

        var spamPatterns = new[]
        {
            @"click here", @"buy now", @"free money", @"viagra", @"casino",
            @"lottery", @"winner", @"congratulations", @"urgent"
        };

        var lowerContent = content.ToLowerInvariant();
        return spamPatterns.Any(pattern => Regex.IsMatch(lowerContent, pattern, RegexOptions.IgnoreCase));
    }
}