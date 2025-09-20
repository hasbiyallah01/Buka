using AmalaSpotLocator.Configuration;
using AmalaSpotLocator.Core.Applications.Interfaces.Services;
using AmalaSpotLocator.Core.Domain.Entities;
using AmalaSpotLocator.Interfaces;
using System.Collections.Generic;
using Microsoft.Extensions.Options;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AmalaSpotLocator.Models;

namespace AmalaSpotLocator.Core.Applications.Services;

public class CandidateExtractionService : ICandidateExtractionService
{
    private readonly DiscoverySettings _settings;
    private readonly IGoogleMapsService _googleMapsService;
    private readonly ILogger<CandidateExtractionService> _logger;

    private readonly string[] _strongAmalaKeywords = {
        "amala", "gbegiri", "ewedu", "abula", "amala spot", "amala joint"
    };

    private readonly string[] _weakAmalaKeywords = {
        "yoruba food", "nigerian restaurant", "local food", "traditional food", 
        "mama put", "buka", "nigerian cuisine", "west african food"
    };

    public CandidateExtractionService(
        IOptions<DiscoverySettings> settings,
        IGoogleMapsService googleMapsService,
        ILogger<CandidateExtractionService> logger)
    {
        _settings = settings.Value;
        _googleMapsService = googleMapsService;
        _logger = logger;
    }

    public async Task<List<SpotCandidate>> ExtractFromGooglePlacesAsync(CancellationToken cancellationToken = default)
    {
        var candidates = new List<SpotCandidate>();

        if (!_settings.GooglePlaces.Enabled)
        {
            _logger.LogInformation("Google Places extraction is disabled");
            return candidates;
        }

        try
        {
            foreach (var city in _settings.GooglePlaces.TargetCities)
            {
                foreach (var keyword in _settings.GooglePlaces.SearchKeywords)
                {
                    try
                    {
                        _logger.LogInformation("Searching Google Places for '{Keyword}' in {City}", keyword, city);

                        var cityLocation = GetCityLocation(city);
                        if (cityLocation == null) continue;

                        var places = await _googleMapsService.SearchPlacesAsync(
                            cityLocation, 
                            _settings.GooglePlaces.SearchRadiusMeters,
                            keyword);

                        foreach (var place in places)
                        {

                            var placeDetails = await _googleMapsService.GetPlaceDetailsAsync(place.PlaceId);
                            if (placeDetails != null)
                            {
                                var candidate = await ConvertPlaceToCandidateAsync(placeDetails, cancellationToken);
                                if (candidate != null)
                                {
                                    _logger.LogInformation("Created candidate: {Name} with confidence score {Score}", 
                                        candidate.Name, candidate.ConfidenceScore);
                                    candidates.Add(candidate);
                                }
                                else
                                {
                                    _logger.LogWarning("Failed to convert place {PlaceName} to candidate", place.Name);
                                }
                            }
                        }

                        await Task.Delay(1000, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error searching Google Places for '{Keyword}' in {City}: {Error}", 
                            keyword, city, ex.Message);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Google Places extraction: {Error}", ex.Message);
        }

        _logger.LogInformation("Google Places extraction completed. Found {Count} candidates", candidates.Count);
        return candidates;
    }

    public async Task<List<SpotCandidate>> ExtractFromSocialMediaAsync(CancellationToken cancellationToken = default)
    {
        var candidates = new List<SpotCandidate>();

        if (!_settings.SocialMedia.Enabled)
        {
            _logger.LogInformation("Social media extraction is disabled");
            return candidates;
        }

        
        _logger.LogInformation("Social media extraction is not yet implemented");


        return candidates;
    }

    public async Task<SpotCandidate> ProcessCandidateAsync(SpotCandidate candidate, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Processing candidate: {CandidateName}", candidate.Name);

            candidate = await NormalizeCandidateDataAsync(candidate, cancellationToken);

            candidate.ConfidenceScore = CalculateConfidenceScore(candidate);

            candidate.QualityScore = CalculateQualityScore(candidate);

            candidate.Status = CandidateStatus.Enriched;
            candidate.ProcessedAt = DateTime.UtcNow;

            return candidate;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing candidate {CandidateName}: {Error}", candidate.Name, ex.Message);
            throw;
        }
    }

    public async Task<ValidationResult> ValidateCandidateAsync(SpotCandidate candidate, CancellationToken cancellationToken = default)
    {
        var result = new ValidationResult();
        var errors = new List<string>();
        var warnings = new List<string>();

        try
        {

            if (string.IsNullOrWhiteSpace(candidate.Name))
                errors.Add("Name is required");
            else if (candidate.Name.Length < 3)
                errors.Add("Name must be at least 3 characters long");
            else if (candidate.Name.Length > 200)
                errors.Add("Name must be less than 200 characters");

            if (string.IsNullOrWhiteSpace(candidate.Address))
                errors.Add("Address is required");
            else if (candidate.Address.Length > 500)
                errors.Add("Address must be less than 500 characters");

            if (candidate.Location == null)
                warnings.Add("Location coordinates are missing");

            if (candidate.ConfidenceScore < _settings.MinConfidenceScore)
                warnings.Add($"Confidence score ({candidate.ConfidenceScore:F2}) is below minimum threshold ({_settings.MinConfidenceScore:F2})");

            var textToCheck = $"{candidate.Name} {candidate.Description}".ToLowerInvariant();
            var hasStrongKeywords = _strongAmalaKeywords.Any(keyword => textToCheck.Contains(keyword));
            var hasWeakKeywords = _weakAmalaKeywords.Any(keyword => textToCheck.Contains(keyword));

            if (!hasStrongKeywords && !hasWeakKeywords)
                errors.Add("Content does not appear to be related to amala or Nigerian food");
            else if (!hasStrongKeywords)
                warnings.Add("Content may not be specifically about amala");

            if (IsSpamContent(candidate.Name) || IsSpamContent(candidate.Description))
                errors.Add("Content appears to be spam");

            if (!string.IsNullOrWhiteSpace(candidate.PhoneNumber))
            {
                var phoneRegex = new Regex(@"^(\+234|0)[789]\d{9}$");
                if (!phoneRegex.IsMatch(candidate.PhoneNumber.Replace(" ", "").Replace("-", "")))
                    warnings.Add("Phone number format may be invalid");
            }

            result.IsValid = errors.Count == 0;
            result.Errors = errors;
            result.Warnings = warnings;
            result.QualityScore = CalculateQualityScore(candidate);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating candidate {CandidateName}: {Error}", candidate.Name, ex.Message);
            result.IsValid = false;
            result.Errors.Add($"Validation error: {ex.Message}");
            return result;
        }
    }

    public async Task<SpotCandidate> EnrichCandidateDataAsync(SpotCandidate candidate, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Enriching candidate: {CandidateName}", candidate.Name);

            if (candidate.Location == null && !string.IsNullOrWhiteSpace(candidate.Address))
            {
                try
                {
                    var location = await _googleMapsService.GeocodeAddressAsync(candidate.Address);
                    if (location != null)
                    {
                        candidate.Location = new NetTopologySuite.Geometries.Point(location.Longitude, location.Latitude) { SRID = 4326 };
                        _logger.LogDebug("Geocoded address for candidate {CandidateName}", candidate.Name);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to geocode address for candidate {CandidateName}: {Error}", 
                        candidate.Name, ex.Message);
                }
            }

            if (candidate.Location != null && candidate.Source != DiscoverySource.GooglePlaces)
            {
                try
                {
                    var nearbyPlaces = await _googleMapsService.FindNearbyPlacesAsync(
                        new Location 
                        { 
                            Latitude = candidate.Location.Y, 
                            Longitude = candidate.Location.X 
                        },
                        100, // 100 meter radius
                        "restaurant");

                    var matchingPlace = nearbyPlaces.FirstOrDefault(p => 
                        IsNameSimilar(p.Name, candidate.Name));

                    if (matchingPlace != null)
                    {

                        var placeDetails = await _googleMapsService.GetPlaceDetailsAsync(matchingPlace.PlaceId);
                        if (placeDetails != null)
                        {
                            if (string.IsNullOrWhiteSpace(candidate.PhoneNumber) && !string.IsNullOrWhiteSpace(placeDetails.FormattedPhoneNumber))
                                candidate.PhoneNumber = placeDetails.FormattedPhoneNumber;

                            if (placeDetails.Rating.HasValue)
                            {
                                candidate.SourceData["googleRating"] = placeDetails.Rating.Value;
                                candidate.SourceData["googleReviewCount"] = placeDetails.UserRatingsTotal ?? 0;
                            }

                            if (placeDetails.PriceLevel.HasValue)
                                candidate.EstimatedPriceRange = placeDetails.PriceLevel.Value;
                        }

                        _logger.LogDebug("Enriched candidate {CandidateName} with Google Places data", candidate.Name);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to enrich candidate {CandidateName} with Google Places data: {Error}", 
                        candidate.Name, ex.Message);
                }
            }

            candidate.Status = CandidateStatus.Enriched;
            return candidate;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enriching candidate {CandidateName}: {Error}", candidate.Name, ex.Message);
            throw;
        }
    }

    private async Task<SpotCandidate?> ConvertPlaceToCandidateAsync(PlaceDetails place, CancellationToken cancellationToken)
    {
        try
        {
            var candidate = new SpotCandidate
            {
                Name = place.Name,
                Address = place.FormattedAddress,
                Location = new NetTopologySuite.Geometries.Point(place.Location.Longitude, place.Location.Latitude) { SRID = 4326 },
                PhoneNumber = place.FormattedPhoneNumber,
                Source = DiscoverySource.GooglePlaces,
                SourceUrl = $"https://maps.google.com/place?place_id={place.PlaceId}",
                SourceData = new Dictionary<string, object>
                {
                    ["placeId"] = place.PlaceId,
                    ["rating"] = place.Rating ?? 0,
                    ["userRatingsTotal"] = place.UserRatingsTotal ?? 0,
                    ["types"] = place.Types,
                    ["website"] = place.Website ?? ""
                }
            };

            if (place.PriceLevel.HasValue)
                candidate.EstimatedPriceRange = place.PriceLevel.Value;

            if (place.OpeningHours.Any())
            {
                var todayHours = place.OpeningHours.FirstOrDefault(h => h.DayOfWeek == (int)DateTime.Today.DayOfWeek);
                if (todayHours != null)
                {
                    candidate.OpeningTime = todayHours.OpenTime;
                    candidate.ClosingTime = todayHours.CloseTime;
                }
            }

            var specialties = ExtractSpecialtiesFromReviews(place.Reviews);
            candidate.Specialties = specialties;

            return candidate;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting place {PlaceName} to candidate: {Error}", place.Name, ex.Message);
            return null;
        }
    }

    private async Task<SpotCandidate> NormalizeCandidateDataAsync(SpotCandidate candidate, CancellationToken cancellationToken)
    {

        candidate.Name = NormalizeText(candidate.Name);

        if (!string.IsNullOrWhiteSpace(candidate.Description))
            candidate.Description = NormalizeText(candidate.Description);

        if (!string.IsNullOrWhiteSpace(candidate.Address))
            candidate.Address = NormalizeText(candidate.Address);

        if (!string.IsNullOrWhiteSpace(candidate.PhoneNumber))
            candidate.PhoneNumber = NormalizePhoneNumber(candidate.PhoneNumber);

        candidate.Specialties = candidate.Specialties
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => NormalizeText(s))
            .Distinct()
            .ToList();

        return candidate;
    }

    private double CalculateConfidenceScore(SpotCandidate candidate)
    {
        double score = 0.0;

        var textToCheck = $"{candidate.Name} {candidate.Description}".ToLowerInvariant();

        if (_strongAmalaKeywords.Any(keyword => textToCheck.Contains(keyword)))
            score += 0.6;
        else if (_weakAmalaKeywords.Any(keyword => textToCheck.Contains(keyword)))
            score += 0.3;

        if (candidate.Location != null)
            score += 0.2;

        if (!string.IsNullOrWhiteSpace(candidate.PhoneNumber))
            score += 0.1;

        if (!string.IsNullOrWhiteSpace(candidate.Address))
            score += 0.1;

        switch (candidate.Source)
        {
            case DiscoverySource.GooglePlaces:
                score += 0.3;
                break;
            case DiscoverySource.WebScraping:
                score += 0.05;
                break;
            case DiscoverySource.UserSubmission:
                score += 0.15;
                break;
        }

        return Math.Min(1.0, score);
    }

    private double CalculateQualityScore(SpotCandidate candidate)
    {
        double score = 0.0;

        if (!string.IsNullOrWhiteSpace(candidate.Name)) score += 0.2;
        if (!string.IsNullOrWhiteSpace(candidate.Description)) score += 0.2;
        if (!string.IsNullOrWhiteSpace(candidate.Address)) score += 0.2;
        if (candidate.Location != null) score += 0.2;
        if (!string.IsNullOrWhiteSpace(candidate.PhoneNumber)) score += 0.1;
        if (candidate.OpeningTime.HasValue && candidate.ClosingTime.HasValue) score += 0.1;

        if (candidate.SourceData.ContainsKey("googleRating"))
        {
            if (candidate.SourceData["googleRating"] is decimal rating && rating >= 4.0m)
                score += 0.1;
        }

        return Math.Min(1.0, score);
    }

    private List<string> ExtractSpecialtiesFromReviews(List<PlaceReview> reviews)
    {
        var specialties = new List<string>();
        var amalaTerms = new[] { "amala", "gbegiri", "ewedu", "abula", "stew", "soup" };

        foreach (var review in reviews.Take(10)) // Check first 10 reviews
        {
            var reviewText = review.Text.ToLowerInvariant();
            foreach (var term in amalaTerms)
            {
                if (reviewText.Contains(term) && !specialties.Contains(term, StringComparer.OrdinalIgnoreCase))
                {
                    specialties.Add(term);
                }
            }
        }

        return specialties;
    }

    private bool IsNameSimilar(string name1, string name2)
    {
        if (string.IsNullOrWhiteSpace(name1) || string.IsNullOrWhiteSpace(name2))
            return false;

        var normalized1 = NormalizeText(name1).ToLowerInvariant();
        var normalized2 = NormalizeText(name2).ToLowerInvariant();

        return normalized1.Contains(normalized2) || normalized2.Contains(normalized1) ||
               LevenshteinDistance(normalized1, normalized2) <= Math.Min(normalized1.Length, normalized2.Length) * 0.3;
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

    private string NormalizeText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        text = Regex.Replace(text.Trim(), @"\s+", " ");

        text = Regex.Replace(text, @"<[^>]*>", string.Empty);
        
        return text;
    }

    private string NormalizePhoneNumber(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return string.Empty;

        var cleaned = Regex.Replace(phoneNumber, @"[^\d\+]", "");

        if (cleaned.StartsWith("0") && cleaned.Length == 11)
            cleaned = "+234" + cleaned.Substring(1);
        else if (cleaned.StartsWith("234") && cleaned.Length == 13)
            cleaned = "+" + cleaned;

        return cleaned;
    }

    private bool IsSpamContent(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return false;

        var spamPatterns = new[]
        {
            @"click here", @"buy now", @"free money", @"viagra", @"casino",
            @"lottery", @"winner", @"congratulations", @"urgent", @"limited time"
        };

        var lowerContent = content.ToLowerInvariant();
        return spamPatterns.Any(pattern => Regex.IsMatch(lowerContent, pattern, RegexOptions.IgnoreCase));
    }

    private Location? GetCityLocation(string city)
    {

        var cityCoordinates = new Dictionary<string, Location>
        {
            ["Lagos, Nigeria"] = new Location { Latitude = 6.5244, Longitude = 3.3792 },
            ["Ibadan, Nigeria"] = new Location { Latitude = 7.3775, Longitude = 3.9470 },
            ["Abeokuta, Nigeria"] = new Location { Latitude = 7.1475, Longitude = 3.3619 },
            ["Ilorin, Nigeria"] = new Location { Latitude = 8.4966, Longitude = 4.5426 },
            ["Ogbomoso, Nigeria"] = new Location { Latitude = 8.1335, Longitude = 4.2407 }
        };

        return cityCoordinates.GetValueOrDefault(city);
    }
}