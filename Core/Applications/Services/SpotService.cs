using AmalaSpotLocator.Data;
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
    private readonly ILogger<SpotService> _logger;

    public SpotService(
        AmalaSpotContext context, 
        IGeospatialService geospatialService,
        IGoogleMapsService googleMapsService,
        ILogger<SpotService> logger)
    {
        _context = context;
        _geospatialService = geospatialService;
        _googleMapsService = googleMapsService;
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
            var localSpots = new List<AmalaSpot>();
            var query = _context.AmalaSpots.AsQueryable();

            // Apply basic filters to the query
            if (criteria.Location != null && criteria.RadiusKm.HasValue)
            {
                var centerPoint = _geospatialService.LocationToPoint(criteria.Location);
                var radiusMeters = criteria.RadiusKm.Value * 1000;
                query = query.Where(s => s.Location.Distance(centerPoint) <= radiusMeters);
            }

            if (criteria.MinPriceRange.HasValue)
            {
                query = query.Where(s => s.PriceRange >= criteria.MinPriceRange.Value);
            }
            if (criteria.MaxPriceRange.HasValue)
            {
                query = query.Where(s => s.PriceRange <= criteria.MaxPriceRange.Value);
            }

            if (criteria.MinRating.HasValue)
            {
                query = query.Where(s => s.AverageRating >= criteria.MinRating.Value);
            }

            if (criteria.CurrentTime.HasValue)
            {
                query = query.Where(s => s.OpeningTime.HasValue && s.ClosingTime.HasValue &&
                                        s.OpeningTime <= criteria.CurrentTime && s.ClosingTime >= criteria.CurrentTime);
            }

            if (criteria.IsVerified.HasValue)
            {
                query = query.Where(s => s.IsVerified == criteria.IsVerified.Value);
            }

            if (!string.IsNullOrWhiteSpace(criteria.SearchTerm))
            {
                var searchTerm = criteria.SearchTerm.ToLower();
                query = query.Where(s => s.Name.ToLower().Contains(searchTerm) ||
                                        s.Description != null && s.Description.ToLower().Contains(searchTerm) ||
                                        s.Address.ToLower().Contains(searchTerm));
            }

            // Get local spots from database
            localSpots = (await query.ToListAsync()).ToList();

            // Add real-time Google Places search if location is provided
            if (criteria.Location != null)
            {
                try
                {
                    _logger.LogInformation("Searching Google Places for amala spots near {Lat}, {Lng} with radius {RadiusKm}km", 
                        criteria.Location.Latitude, criteria.Location.Longitude, criteria.RadiusKm ?? 5);

                    var radiusMeters = (criteria.RadiusKm ?? 5) * 1000;
                    var googlePlaces = await _googleMapsService.SearchPlacesAsync(
                        criteria.Location, 
                        radiusMeters, 
                        "amala restaurant");

                    _logger.LogInformation("Google Places API returned {Count} results", googlePlaces.Count());

                    foreach (var place in googlePlaces.Take(10)) // Limit to 10 results
                    {
                        _logger.LogDebug("Processing Google Place: {Name} (ID: {PlaceId})", place.Name, place.PlaceId);
                        
                        var placeDetails = await _googleMapsService.GetPlaceDetailsAsync(place.PlaceId);
                        if (placeDetails != null)
                        {
                            // Convert Google Place to AmalaSpot
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

                            // Check if this spot already exists in local results
                            if (!localSpots.Any(s => s.Name.Equals(googleSpot.Name, StringComparison.OrdinalIgnoreCase) &&
                                                    s.Location.Distance(googleSpot.Location) < 100)) // Within 100 meters
                            {
                                localSpots.Add(googleSpot);
                                _logger.LogDebug("Added Google Place to results: {Name}", googleSpot.Name);
                            }
                            else
                            {
                                _logger.LogDebug("Skipped duplicate Google Place: {Name}", googleSpot.Name);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Failed to get details for Google Place ID: {PlaceId}", place.PlaceId);
                        }
                    }

                    var googleSpotsCount = localSpots.Count(s => s.CreatedAt > DateTime.UtcNow.AddMinutes(-1));
                    _logger.LogInformation("Successfully added {Count} new spots from Google Places", googleSpotsCount);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to search Google Places: {Error}", ex.Message);
                }
            }

            // Apply final filtering and sorting
            var finalResults = localSpots.AsEnumerable();

            // Apply specialty filtering if needed
            if (criteria.Specialties != null && criteria.Specialties.Any())
            {
                finalResults = finalResults.Where(s => criteria.Specialties.Any(specialty => 
                    s.Specialties.Any(spotSpecialty => spotSpecialty.Contains(specialty, StringComparison.OrdinalIgnoreCase))));
            }

            // Apply additional filters
            if (criteria.MinRating.HasValue)
            {
                finalResults = finalResults.Where(s => s.AverageRating >= criteria.MinRating.Value);
            }

            if (criteria.MaxPriceRange.HasValue)
            {
                finalResults = finalResults.Where(s => s.PriceRange <= criteria.MaxPriceRange.Value);
            }

            // Sort by distance if location provided, otherwise by rating
            if (criteria.Location != null)
            {
                var centerPoint = _geospatialService.LocationToPoint(criteria.Location);
                finalResults = finalResults.OrderBy(s => {
                    var distance = s.Location.Distance(centerPoint);
                    return double.IsInfinity(distance) || double.IsNaN(distance) ? double.MaxValue : distance;
                }).ThenByDescending(s => s.AverageRating);
            }
            else
            {
                finalResults = finalResults.OrderByDescending(s => s.AverageRating)
                                         .ThenByDescending(s => s.ReviewCount);
            }

            return finalResults.Skip(criteria.Offset).Take(criteria.Limit).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching spots with criteria");
            throw;
        }
    }

    private List<string> ExtractSpecialtiesFromGooglePlace(PlaceDetails place)
    {
        var specialties = new List<string>();
        var amalaTerms = new[] { "amala", "gbegiri", "ewedu", "abula", "stew", "soup", "yoruba", "nigerian" };

        // Check name and reviews for amala-related terms
        var textToCheck = $"{place.Name} {string.Join(" ", place.Reviews.Select(r => r.Text))}".ToLowerInvariant();
        
        foreach (var term in amalaTerms)
        {
            if (textToCheck.Contains(term) && !specialties.Contains(term, StringComparer.OrdinalIgnoreCase))
            {
                specialties.Add(char.ToUpper(term[0]) + term.Substring(1)); // Capitalize first letter
            }
        }

        return specialties.Any() ? specialties : new List<string> { "Amala" }; // Default to Amala if found through search
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
            const double minDistanceMeters = 50; // 50 meters minimum distance

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