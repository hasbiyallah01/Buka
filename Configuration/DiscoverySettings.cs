using System.Collections.Generic;

namespace AmalaSpotLocator.Configuration;

public class DiscoverySettings
{
    public const string SectionName = "Discovery";

    public bool Enabled { get; set; } = true;

    public int IntervalMinutes { get; set; } = 60;

    public int MaxCandidatesPerRun { get; set; } = 50;

    public double MinConfidenceScore { get; set; } = 0.6;

    public double AutoApprovalQualityThreshold { get; set; } = 0.8;

    public WebScrapingSettings WebScraping { get; set; } = new();

    public GooglePlacesDiscoverySettings GooglePlaces { get; set; } = new();

    public SocialMediaSettings SocialMedia { get; set; } = new();
}

public class WebScrapingSettings
{

    public bool Enabled { get; set; } = true;

    public int RequestDelayMs { get; set; } = 1000;

    public int MaxPagesPerSite { get; set; } = 10;

    public string UserAgent { get; set; } = "AmalaSpotLocator/1.0 (+https://amalaspotlocator.com)";

    public List<ScrapingTarget> Targets { get; set; } = new();
}

public class ScrapingTarget
{
    public string Name { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public List<string> SearchUrls { get; set; } = new();
    public Dictionary<string, string> Selectors { get; set; } = new();
    public bool Enabled { get; set; } = true;
}

public class GooglePlacesDiscoverySettings
{

    public bool Enabled { get; set; } = true;

    public int SearchRadiusMeters { get; set; } = 5000;

    public List<string> SearchKeywords { get; set; } = new()
    {
        "amala restaurant",
        "amala spot",
        "yoruba restaurant",
        "nigerian restaurant amala",
        "local amala joint"
    };

    public List<string> TargetCities { get; set; } = new()
    {
        "Lagos, Nigeria",
        "Ibadan, Nigeria",
        "Abeokuta, Nigeria",
        "Ilorin, Nigeria",
        "Ogbomoso, Nigeria"
    };
}

public class SocialMediaSettings
{

    public bool Enabled { get; set; } = false;

    public List<string> Hashtags { get; set; } = new()
    {
        "#amala",
        "#amalaspot",
        "#yorubafood",
        "#nigerianfood",
        "#lagosrestaurant"
    };

    public List<string> Keywords { get; set; } = new()
    {
        "amala spot",
        "best amala",
        "amala restaurant",
        "where to eat amala"
    };
}