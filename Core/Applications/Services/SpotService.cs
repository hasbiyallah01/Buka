using AmalaSpotLocator.Infrastructure;
using AmalaSpotLocator.Interfaces;
using System.Collections.Generic;
using AmalaSpotLocator.Models.SpotModel;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using System.Text.RegularExpressions;
using AmalaSpotLocator.Models;
using AmalaSpotLocator.Core.Applications.Interfaces.Services;

namespace AmalaSpotLocator.Core.Applications.Services;

public class SpotService : ISpotService
{
    private readonly AmalaSpotContext _context;
    private readonly IGeospatialService _geospatialService;
    private readonly IGoogleMapsService _googleMapsService;
    private readonly IBusynessService _busynessService;
    private readonly ILogger<SpotService> _logger;

    public SpotService(
        AmalaSpotContext context, 
        IGeospatialService geospatialService,
        IGoogleMapsService googleMapsService,
        IBusynessService busynessService,
        ILogger<SpotService> logger)
    {
        _context = context;
        _geospatialService = geospatialService;
        _googleMapsService = googleMapsService;
        _busynessService = busynessService;
        _logger = logger;
    }

    #region CRUD Operations

    public async Task<AmalaSpot?> GetByIdAsync(Guid id)
    {
        try
        {
            return await _context.AmalaSpots
                .Include(s => s.Reviews)
                .Include(s => s.CreatedByUser)
                .FirstOrDefaultAsync(s => s.Id == id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving spot with ID {SpotId}", id);
            throw;
        }
    }

    public async Task<AmalaSpot> CreateAsync(AmalaSpot spot)
    {
        try
        {

            if (!await ValidateSpotAsync(spot))
            {
                throw new ArgumentException("Invalid spot data provided");
            }

            spot = await SanitizeSpotDataAsync(spot);

            spot.CreatedAt = DateTime.UtcNow;
            spot.UpdatedAt = DateTime.UtcNow;

            _context.AmalaSpots.Add(spot);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created new spot: {SpotName} with ID {SpotId}", spot.Name, spot.Id);
            return spot;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating spot: {SpotName}", spot.Name);
            throw;
        }
    }

    public async Task<AmalaSpot> UpdateAsync(AmalaSpot spot)
    {
        try
        {
            var existingSpot = await _context.AmalaSpots.FindAsync(spot.Id);
            if (existingSpot == null)
            {
                throw new ArgumentException($"Spot with ID {spot.Id} not found");
            }

            if (!await ValidateSpotAsync(spot))
            {
                throw new ArgumentException("Invalid spot data provided");
            }

            spot = await SanitizeSpotDataAsync(spot);

            existingSpot.Name = spot.Name;
            existingSpot.Description = spot.Description;
            existingSpot.Address = spot.Address;
            existingSpot.Location = spot.Location;
            existingSpot.PhoneNumber = spot.PhoneNumber;
            existingSpot.OpeningTime = spot.OpeningTime;
            existingSpot.ClosingTime = spot.ClosingTime;
            existingSpot.PriceRange = spot.PriceRange;
            existingSpot.Specialties = spot.Specialties;
            existingSpot.IsVerified = spot.IsVerified;
            existingSpot.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated spot: {SpotName} with ID {SpotId}", spot.Name, spot.Id);
            return existingSpot;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating spot with ID {SpotId}", spot.Id);
            throw;
        }
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        try
        {
            var spot = await _context.AmalaSpots.FindAsync(id);
            if (spot == null)
            {
                return false;
            }

            _context.AmalaSpots.Remove(spot);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Deleted spot with ID {SpotId}", id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting spot with ID {SpotId}", id);
            throw;
        }
    }

    public async Task<IEnumerable<AmalaSpot>> GetAllAsync()
    {
        try
        {
            return await _context.AmalaSpots
                .Include(s => s.Reviews)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all spots");
            throw;
        }
    }

    #endregion

    #region Filtering and Search Operations

    public async Task<IEnumerable<AmalaSpot>> GetNearbyAsync(Models.Location location, double radiusKm, int limit = 50)
    {
        try
        {
            return await _geospatialService.FindNearbySpots(location, radiusKm, limit);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding nearby spots for location {Latitude}, {Longitude}", 
                location.Latitude, location.Longitude);
            throw;
        }
    }

    public async Task<IEnumerable<AmalaSpot>> FilterByPriceRangeAsync(PriceRange minPrice, PriceRange maxPrice)
    {
        try
        {
            return await _context.AmalaSpots
                .Where(s => s.PriceRange >= minPrice && s.PriceRange <= maxPrice)
                .OrderBy(s => s.PriceRange)
                .ThenByDescending(s => s.AverageRating)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error filtering spots by price range {MinPrice} to {MaxPrice}", minPrice, maxPrice);
            throw;
        }
    }

    public async Task<IEnumerable<AmalaSpot>> FilterByRatingAsync(decimal minRating)
    {
        try
        {
            return await _context.AmalaSpots
                .Where(s => s.AverageRating >= minRating)
                .OrderByDescending(s => s.AverageRating)
                .ThenByDescending(s => s.ReviewCount)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error filtering spots by minimum rating {MinRating}", minRating);
            throw;
        }
    }

    public async Task<IEnumerable<AmalaSpot>> FilterByOpenHoursAsync(TimeSpan currentTime)
    {
        try
        {
            return await _context.AmalaSpots
                .Where(s => s.OpeningTime.HasValue && s.ClosingTime.HasValue &&
                           s.OpeningTime <= currentTime && s.ClosingTime >= currentTime)
                .OrderByDescending(s => s.AverageRating)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error filtering spots by open hours at {CurrentTime}", currentTime);
            throw;
        }
    }

    public async Task<IEnumerable<AmalaSpot>> SearchAsync(SpotSearchCriteria criteria)
    {
        try
        {
            var allSpots = new List<AmalaSpot>();
            if (criteria.Location != null)
            {
                try
                {
                    _logger.LogInformation("Searching Google Maps for amala spots near {Lat}, {Lng} within {Radius}km", 
                        criteria.Location.Latitude, criteria.Location.Longitude, criteria.RadiusKm ?? 5);

                    var radiusMeters = (criteria.RadiusKm ?? 5) * 1000;

                    var searchQueries = new[]
                    {
                        "amala restaurant Nigerian food",
                        "buka Nigerian restaurant amala",
                        "mama put amala gbegiri ewedu",
                        "Nigerian restaurant yoruba food",
                        "African restaurant amala abula",
                        "local restaurant Nigerian cuisine",
                        "traditional Nigerian food restaurant"
                    };

                    var googlePlaces = await _googleMapsService.SearchPlacesWithMultipleQueriesAsync(
                        criteria.Location,
                        radiusMeters,
                        searchQueries
                    );

                    _logger.LogInformation("Found {Count} places from Google Maps", googlePlaces.Count());

                    foreach (var place in googlePlaces.Take(50)) 
                    {
                        var distanceKm = _geospatialService.CalculateDistance(criteria.Location, place.Location);
                        if (distanceKm > (criteria.RadiusKm ?? 5)) continue;

                        var placeDetails = await _googleMapsService.GetPlaceDetailsAsync(place.PlaceId);
                        if (placeDetails == null || !IsRelevantAmalaRestaurant(placeDetails)) continue;

                        var googleSpot = new AmalaSpot
                        {
                            Id = Guid.NewGuid(),
                            Name = placeDetails.Name,
                            Address = placeDetails.FormattedAddress,
                            Location = new Point(placeDetails.Location.Longitude, placeDetails.Location.Latitude) { SRID = 4326 },
                            PhoneNumber = placeDetails.FormattedPhoneNumber,
                            AverageRating = (decimal)(placeDetails.Rating ?? 0),
                            ReviewCount = placeDetails.UserRatingsTotal ?? 0,
                            PriceRange = placeDetails.PriceLevel ?? PriceRange.Budget,
                            Specialties = ExtractSpecialtiesFromGooglePlace(placeDetails),
                            IsVerified = false,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };
                        if (!allSpots.Any(s => s.Name.Equals(googleSpot.Name, StringComparison.OrdinalIgnoreCase) &&
                                                 s.Location.Distance(googleSpot.Location) < 100))
                        {
                            allSpots.Add(googleSpot);
                            _logger.LogDebug("Added Google Place to results: {Name} with specialties: {Specialties}", 
                                googleSpot.Name, string.Join(", ", googleSpot.Specialties));
                        }
                    }

                    _logger.LogInformation("Added {Count} relevant spots from Google Maps", allSpots.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to search Google Places: {Error}", ex.Message);
                    _logger.LogInformation("Falling back to database search due to Google Maps error");
                    var query = _context.AmalaSpots.AsQueryable();
                    if (criteria.Location != null && criteria.RadiusKm.HasValue)
                    {
                        var centerPoint = _geospatialService.LocationToPoint(criteria.Location);
                        var radiusMeters = criteria.RadiusKm.Value * 1000;
                        query = query.Where(s => s.Location.Distance(centerPoint) <= radiusMeters);
                    }
                    allSpots.AddRange(await query.Take(20).ToListAsync());
                }
            }
            else
            {
                _logger.LogInformation("No location provided, getting spots from database");
                allSpots.AddRange(await _context.AmalaSpots.Take(20).ToListAsync());
            }
            foreach (var spot in allSpots)
            {
                spot.Specialties = spot.Specialties
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s.Trim().ToLowerInvariant())
                    .Distinct()
                    .ToList();
            }
            var finalResults = allSpots.AsEnumerable();
            if (criteria.Specialties != null && criteria.Specialties.Any())
            {
                var searchTerms = criteria.Specialties
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s.Trim().ToLowerInvariant())
                    .ToList();

                _logger.LogDebug("Filtering by search terms: {Terms}", string.Join(", ", searchTerms));
                finalResults = finalResults.Where(spot =>
                {
                    var spotSpecialties = spot.Specialties.Select(s => s.ToLowerInvariant()).ToList();
                    if (searchTerms.Any(term => spotSpecialties.Any(specialty => specialty.Contains(term))))
                    {
                        return true;
                    }
                    if (searchTerms.Contains("amala") && 
                        spotSpecialties.Any(s => s.Contains("nigerian") || s.Contains("yoruba") || s.Contains("local") || s.Contains("traditional")))
                    {
                        return true;
                    }
                    var nigerianFoodTerms = new[] { "amala", "gbegiri", "ewedu", "abula", "jollof", "suya", "pepper soup" };
                    if (searchTerms.Any(term => nigerianFoodTerms.Contains(term)) &&
                        spotSpecialties.Any(s => s.Contains("nigerian") || s.Contains("african") || s.Contains("amala")))
                    {
                        return true;
                    }
                    
                    return false;
                });
                
                _logger.LogDebug("After specialty filtering: {Count} spots remain", finalResults.Count());
            }
            if (criteria.MinRating.HasValue && criteria.MinRating.Value > 0)
            {
                var adjustedMinRating = Math.Max(0, criteria.MinRating.Value - 1);
                finalResults = finalResults.Where(s => s.AverageRating >= adjustedMinRating);
                _logger.LogDebug("Applied rating filter: original {Original}, adjusted {Adjusted}", 
                    criteria.MinRating.Value, adjustedMinRating);
            }
            if (criteria.MaxPriceRange.HasValue)
                finalResults = finalResults.Where(s => s.PriceRange <= criteria.MaxPriceRange.Value);
            if (criteria.Location != null)
            {
                var centerPoint = _geospatialService.LocationToPoint(criteria.Location);
                finalResults = finalResults
                    .OrderBy(s => s.Location.Distance(centerPoint))
                    .ThenByDescending(s => s.AverageRating);
            }
            else
            {
                finalResults = finalResults
                    .OrderByDescending(s => s.AverageRating)
                    .ThenByDescending(s => s.ReviewCount);
            }
            var finalList = finalResults.Skip(criteria.Offset).Take(criteria.Limit).ToList();
            _logger.LogInformation("Returning {Count} spots after all filtering and pagination", finalList.Count);
            return finalList;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching spots with criteria");
            throw;
        }
    }


    private bool IsRelevantAmalaRestaurant(PlaceDetails place)
    {
        try
        {
            var relevantTerms = new[] { 
                "amala", "gbegiri", "ewedu", "abula", "yoruba", "nigerian", "african", "lagos", "nigeria",
                "buka", "mama put", "mama's place", "local food", "traditional", "indigenous",
                "ewa agoyin", "pounded yam", "fufu", "jollof", "suya", "pepper soup", "kitchen"
            };
            var irrelevantTerms = new[] { 
                "indian", "gujarati", "ahmedabad", "mumbai", "delhi", "bangalore", "punjabi", "hindi",
                "chinese", "japanese", "korean", "thai", "vietnamese", "italian", "french", "mexican",
                "pizza", "burger", "kfc", "mcdonald"
            };
            var textToCheck = $"{place.Name} {place.FormattedAddress} {string.Join(" ", place.Reviews?.Select(r => r.Text) ?? new string[0])}".ToLowerInvariant();
            
            _logger.LogDebug("Checking restaurant: {Name} at {Address}", place.Name, place.FormattedAddress);
            _logger.LogDebug("Text to check: {Text}", textToCheck.Substring(0, Math.Min(200, textToCheck.Length)));
            if (irrelevantTerms.Any(term => textToCheck.Contains(term)))
            {
                _logger.LogDebug("Rejected {Name} - contains irrelevant terms", place.Name);
                return false;
            }
            var isRestaurant = place.Types?.Any(type => 
                type.Contains("restaurant") || 
                type.Contains("food") || 
                type.Contains("meal_takeaway") ||
                type.Contains("establishment")) ?? true; 
                
            if (!isRestaurant)
            {
                _logger.LogDebug("Rejected {Name} - not a restaurant type", place.Name);
                return false;
            }
            var hasRelevantTerms = relevantTerms.Any(term => textToCheck.Contains(term));
            var isInNigeriaArea = textToCheck.Contains("nigeria") || textToCheck.Contains("lagos") || 
                                 textToCheck.Contains("abuja") || textToCheck.Contains("ibadan") ||
                                 textToCheck.Contains("kano") || textToCheck.Contains("port harcourt");
            
            var isRelevant = hasRelevantTerms || isInNigeriaArea;
            
            if (isRelevant)
            {
                _logger.LogDebug("Accepted {Name} - hasRelevantTerms: {HasTerms}, isInNigeriaArea: {IsInArea}", 
                    place.Name, hasRelevantTerms, isInNigeriaArea);
            }
            else
            {
                _logger.LogDebug("Rejected {Name} - no relevant terms or Nigeria location", place.Name);
            }
            
            return isRelevant;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking restaurant relevance for {Name}", place.Name);
            return true; 
        }
    }

    private IEnumerable<string> ExtractFoodTermsFromCriteria(string criteria)
    {
        var priceWords = new[] { 
            "cheap", "expensive", "budget", "affordable", "costly", "pricey", "inexpensive",
            "cheap", "pass", "cost", "much", "small", "money", "big", "well",
            "owo", "kekere", "gbowo", "nla", "pupọ", "die"
        };
        
        var foodTerms = new List<string>();
        var words = criteria.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var word in words)
        {
            var cleanWord = word.Trim().ToLowerInvariant();
            if (!priceWords.Contains(cleanWord) && cleanWord.Length > 2)
            {
                var mappedTerm = MapFoodTerm(cleanWord);
                if (!string.IsNullOrEmpty(mappedTerm))
                {
                    foodTerms.Add(mappedTerm);
                }
            }
        }
        if (!foodTerms.Any())
        {
            var mappedCriteria = MapFoodTerm(criteria.ToLowerInvariant());
            return !string.IsNullOrEmpty(mappedCriteria) ? new[] { mappedCriteria } : new[] { criteria };
        }
        
        return foodTerms.Distinct();
    }

    private string MapFoodTerm(string term)
    {
        var foodMappings = new Dictionary<string, string>
        {
            { "amala", "amala" },
            { "gbegiri", "gbegiri" },
            { "ewedu", "ewedu" },
            { "abula", "abula" },
            { "obe", "stew" },
            { "stew", "stew" },
            { "soup", "soup" },
            { "buka", "amala" },
            { "mama", "amala" }, 
            { "put", "amala" },
            { "local", "amala" },
            { "traditional", "amala" },
            { "nigerian", "amala" },
            { "yoruba", "amala" },
            { "ounje", "amala" }, 
            { "ile", "amala" }, 
            { "dun", "amala" }, 
            { "dara", "amala" }, 
            { "food", "amala" },
            { "restaurant", "amala" },
            { "spot", "amala" },
            { "place", "amala" },
            { "joint", "amala" }
        };

        return foodMappings.TryGetValue(term, out var mapped) ? mapped : 
               (term.Length > 2 && !IsCommonWord(term) ? term : string.Empty);
    }

    private bool IsCommonWord(string word)
    {
        var commonWords = new[] { 
            "the", "and", "for", "are", "but", "not", "you", "all", "can", "had", "her", "was", "one", "our", "out", "day", "get", "has", "him", "his", "how", "its", "may", "new", "now", "old", "see", "two", "way", "who", "boy", "did", "man", "men", "put", "say", "she", "too", "use",
            "dey", "wey", "for", "dis", "dat", "dem", "una", "abi", "sha", "sef",
            "ni", "ti", "si", "ko", "lo", "wa", "ri", "se", "le", "mo", "to", "bi"
        };
        return commonWords.Contains(word);
    }

    private List<string> ExtractSpecialtiesFromGooglePlace(PlaceDetails place)
    {
        var specialties = new List<string>();
        var foodTerms = new Dictionary<string, string>
        {
            { "amala", "Amala" },
            { "gbegiri", "Gbegiri" },
            { "ewedu", "Ewedu" },
            { "abula", "Abula" },
            { "ewa agoyin", "Ewa Agoyin" },
            { "pounded yam", "Pounded Yam" },
            { "fufu", "Fufu" },
            { "jollof", "Jollof Rice" },
            { "pepper soup", "Pepper Soup" },
            { "suya", "Suya" },
            { "moi moi", "Moi Moi" },
            { "akara", "Akara" },
            { "plantain", "Plantain" },
            { "rice and stew", "Rice and Stew" },
            { "nigerian", "Nigerian Cuisine" },
            { "yoruba", "Yoruba Cuisine" },
            { "local food", "Local Nigerian Food" },
            { "traditional", "Traditional Nigerian" }
        };
        var textToCheck = $"{place.Name} {string.Join(" ", place.Reviews.Select(r => r.Text))}".ToLowerInvariant();
        
        foreach (var (searchTerm, displayName) in foodTerms)
        {
            if (textToCheck.Contains(searchTerm) && !specialties.Contains(displayName, StringComparer.OrdinalIgnoreCase))
            {
                specialties.Add(displayName);
            }
        }
        if (!specialties.Any())
        {
            if (textToCheck.Contains("buka") || textToCheck.Contains("mama put"))
            {
                specialties.AddRange(new[] { "Amala", "Rice and Stew", "Local Nigerian Food" });
            }
            else
            {
                specialties.Add("Amala"); 
            }
        }

        return specialties;
    }

    #endregion

    #region Validation and Business Logic

    public async Task<bool> ValidateSpotAsync(AmalaSpot spot)
    {
        try
        {

            if (string.IsNullOrWhiteSpace(spot.Name) || spot.Name.Length > 200)
            {
                _logger.LogDebug("Validation failed: Invalid name");
                return false;
            }

            if (string.IsNullOrWhiteSpace(spot.Address) || spot.Address.Length > 500)
            {
                _logger.LogDebug("Validation failed: Invalid address");
                return false;
            }

            if (spot.Location == null)
            {
                _logger.LogDebug("Validation failed: Location is null");
                return false;
            }

            try
            {
                var location = _geospatialService.PointToLocation(spot.Location);
                if (!_geospatialService.IsLocationInNigeria(location))
                {
                    _logger.LogDebug("Validation failed: Location not in Nigeria - Lat: {Lat}, Lng: {Lng}", location.Latitude, location.Longitude);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Skipping Nigeria location check due to error: {Error}", ex.Message);

            }

            if (!Enum.IsDefined(typeof(PriceRange), spot.PriceRange))
            {
                _logger.LogDebug("Validation failed: Invalid price range");
                return false;
            }

            if (spot.AverageRating < 0 || spot.AverageRating > 5)
            {
                _logger.LogDebug("Validation failed: Invalid rating: {Rating}", spot.AverageRating);
                return false;
            }

            if (spot.OpeningTime.HasValue && spot.ClosingTime.HasValue)
            {
                if (spot.OpeningTime >= spot.ClosingTime)
                {
                    _logger.LogDebug("Validation failed: Invalid opening hours");
                    return false;
                }
            }

            try
            {
                var location = _geospatialService.PointToLocation(spot.Location);
                var excludeId = spot.Id == Guid.Empty ? (Guid?)null : spot.Id;
                if (!await IsLocationUniqueAsync(location, excludeId))
                {
                    _logger.LogDebug("Validation failed: Location not unique");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Skipping location uniqueness check due to error: {Error}", ex.Message);

            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating spot: {SpotName}", spot.Name);
            return false;
        }
    }

    public async Task<AmalaSpot> SanitizeSpotDataAsync(AmalaSpot spot)
    {
        try
        {

            spot.Name = SanitizeString(spot.Name).Trim();

            if (!string.IsNullOrWhiteSpace(spot.Description))
            {
                spot.Description = SanitizeString(spot.Description).Trim();
            }

            spot.Address = SanitizeString(spot.Address).Trim();

            if (!string.IsNullOrWhiteSpace(spot.PhoneNumber))
            {
                spot.PhoneNumber = SanitizePhoneNumber(spot.PhoneNumber);
            }

            if (spot.Specialties != null)
            {
                spot.Specialties = spot.Specialties
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => SanitizeString(s).Trim())
                    .Distinct()
                    .ToList();
            }

            return await Task.FromResult(spot);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sanitizing spot data: {SpotName}", spot.Name);
            throw;
        }
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        try
        {
            return await _context.AmalaSpots.AnyAsync(s => s.Id == id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if spot exists with ID {SpotId}", id);
            throw;
        }
    }

    public async Task<bool> IsLocationUniqueAsync(Models.Location location, Guid? excludeSpotId = null)
    {
        try
        {
            var point = _geospatialService.LocationToPoint(location);
            const double minDistanceMeters = 50; 

            var query = _context.AmalaSpots
                .Where(s => s.Location.Distance(point) <= minDistanceMeters);

            if (excludeSpotId.HasValue)
            {
                query = query.Where(s => s.Id != excludeSpotId.Value);
            }

            return !await query.AnyAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking location uniqueness for {Latitude}, {Longitude}", 
                location.Latitude, location.Longitude);
            throw;
        }
    }

    #endregion

    #region Statistics and Aggregation

    public async Task<int> GetTotalCountAsync()
    {
        try
        {
            return await _context.AmalaSpots.CountAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting total spot count");
            throw;
        }
    }

    public async Task<decimal> GetAverageRatingAsync()
    {
        try
        {
            var spots = await _context.AmalaSpots
                .Where(s => s.ReviewCount > 0)
                .ToListAsync();

            if (!spots.Any())
                return 0;

            return spots.Average(s => s.AverageRating);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating average rating");
            throw;
        }
    }

    public async Task<IEnumerable<AmalaSpot>> GetTopRatedAsync(int count = 10)
    {
        try
        {
            return await _context.AmalaSpots
                .Where(s => s.ReviewCount > 0)
                .OrderByDescending(s => s.AverageRating)
                .ThenByDescending(s => s.ReviewCount)
                .Take(count)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting top rated spots");
            throw;
        }
    }

    public async Task<IEnumerable<AmalaSpot>> GetRecentlyAddedAsync(int count = 10)
    {
        try
        {
            return await _context.AmalaSpots
                .OrderByDescending(s => s.CreatedAt)
                .Take(count)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recently added spots");
            throw;
        }
    }

    #endregion

    #region Private Helper Methods

    private static string SanitizeString(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var sanitized = Regex.Replace(input, @"<[^>]*>", string.Empty);

        sanitized = Regex.Replace(sanitized, @"[""'&]", string.Empty);

        sanitized = Regex.Replace(sanitized, @"\s+", " ");
        
        return sanitized.Trim();
    }

    private static string SanitizePhoneNumber(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return string.Empty;

        var sanitized = Regex.Replace(phoneNumber, @"[^\d\+\-\(\)\s]", string.Empty);
        
        return sanitized.Trim();
    }

    #endregion
}