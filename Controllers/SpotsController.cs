using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using AmalaSpotLocator.Interfaces;
using System.Collections.Generic;
using Swashbuckle.AspNetCore.Annotations;
using AmalaSpotLocator.Models.SpotModel;
using AmalaSpotLocator.Models;
using AmalaSpotLocator.Core.Applications.Services;
using AmalaSpotLocator.Core.Applications.Interfaces;

namespace AmalaSpotLocator.Controllers;

[ApiController]
[Route("api/spot")]
[Produces("application/json")]
[Tags("Spots")]
public class SpotsController : ControllerBase
{
    private readonly ISpotService _spotService;
    private readonly IGeospatialService _geospatialService;
    private readonly ISpotMappingService _spotMappingService;
    private readonly ILogger<SpotsController> _logger;
    private readonly IWeatherService _weatherService;

    public SpotsController(
        ISpotService spotService,
        IGeospatialService geospatialService,
        ISpotMappingService spotMappingService,
        ILogger<SpotsController> logger,
        IWeatherService weatherService)
    {
        _spotService = spotService;
        _geospatialService = geospatialService;
        _spotMappingService = spotMappingService;
        _logger = logger;
        _weatherService = weatherService;
    }

    [HttpGet("nearby")]
    public async Task<ActionResult<PagedResponse<SpotResponse>>> GetNearbySpots([FromQuery] LocationQuery query)
    {
        try
        {
            _logger.LogInformation("Getting nearby spots for location: {Lat}, {Lng} within {Radius}km", 
                query.Latitude, query.Longitude, query.RadiusKm);

            var location = new Models.Location { Latitude = query.Latitude, Longitude = query.Longitude };

            var criteria = new SpotSearchCriteria
            {
                Location = location,
                RadiusKm = query.RadiusKm,
                MinRating = query.MinRating,
                IsVerified = query.IsVerified,
                CurrentTime = DateTime.Now.TimeOfDay,
                Limit = query.PageSize,
                Offset = (query.Page - 1) * query.PageSize
            };

            if (query.PriceRange.HasValue)
            {
                criteria.MinPriceRange = query.PriceRange.Value;
                criteria.MaxPriceRange = query.PriceRange.Value;
            }

            var spots = await _spotService.SearchAsync(criteria);
            var spotsList = spots.ToList();

            var spotResponses = spotsList.Select(spot => MapToSpotResponse(spot, location)).ToList();

            var totalCount = spotsList.Count;
            var totalPages = (int)Math.Ceiling((double)totalCount / query.PageSize);

            var response = new PagedResponse<SpotResponse>
            {
                Data = spotResponses,
                Page = query.Page,
                PageSize = query.PageSize,
                TotalCount = totalCount,
                TotalPages = totalPages,
                HasNextPage = query.Page < totalPages,
                HasPreviousPage = query.Page > 1
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting nearby spots for location: {Lat}, {Lng}", 
                query.Latitude, query.Longitude);
            return StatusCode(500, "An error occurred while retrieving nearby spots");
        }
    }

    [HttpPost("search")]
    public async Task<ActionResult<PagedResponse<SpotResponse>>> SearchSpots([FromBody] SpotSearchRequest request)
    {
        try
        {
            _logger.LogInformation("Searching spots with criteria: {SearchTerm}", request.SearchTerm);

            var criteria = new SpotSearchCriteria
            {
                SearchTerm = request.SearchTerm,
                MinPriceRange = request.MinPriceRange,
                MaxPriceRange = request.MaxPriceRange,
                MinRating = request.MinRating,
                IsVerified = request.IsVerified,
                Specialties = request.Specialties,
                Limit = request.PageSize,
                Offset = (request.Page - 1) * request.PageSize
            };

            if (request.Latitude.HasValue && request.Longitude.HasValue)
            {
                criteria.Location = new Models.Location 
                { 
                    Latitude = request.Latitude.Value, 
                    Longitude = request.Longitude.Value 
                };
                criteria.RadiusKm = request.RadiusKm;
            }

            if (request.IsCurrentlyOpen == true)
            {
                criteria.CurrentTime = DateTime.Now.TimeOfDay;
            }

            var spots = await _spotService.SearchAsync(criteria);
            var spotsList = spots.ToList();

            var spotResponses = spotsList.Select(spot => 
                MapToSpotResponse(spot, criteria.Location)).ToList();

            var totalCount = spotsList.Count;
            var totalPages = (int)Math.Ceiling((double)totalCount / request.PageSize);

            var response = new PagedResponse<SpotResponse>
            {
                Data = spotResponses,
                Page = request.Page,
                PageSize = request.PageSize,
                TotalCount = totalCount,
                TotalPages = totalPages,
                HasNextPage = request.Page < totalPages,
                HasPreviousPage = request.Page > 1
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching spots with term: {SearchTerm}", request.SearchTerm);
            return StatusCode(500, "An error occurred while searching spots");
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SpotResponse>> GetSpotDetails(Guid id)
    {
        try
        {
            _logger.LogInformation("Getting spot details for ID: {SpotId}", id);

            var spot = await _spotService.GetByIdAsync(id);
            if (spot == null)
            {
                _logger.LogWarning("Spot not found with ID: {SpotId}", id);
                return NotFound($"Spot with ID {id} not found");
            }

            var response = MapToSpotResponse(spot, null, includeReviews: true);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting spot details for ID: {SpotId}", id);
            return StatusCode(500, "An error occurred while retrieving spot details");
        }
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<SpotResponse>> CreateSpot([FromBody] CreateSpotRequest request)
    {
        try
        {
            _logger.LogInformation("Creating new spot: {SpotName}", request.Name);

            var location = new Models.Location { Latitude = request.Latitude, Longitude = request.Longitude };
            var point = _geospatialService.LocationToPoint(location);

            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized("Invalid user token");
            }

            var spot = new AmalaSpot
            {
                Name = request.Name,
                Description = request.Description,
                Address = request.Address,
                Location = point,
                PhoneNumber = request.PhoneNumber,
                OpeningTime = request.OpeningTime,
                ClosingTime = request.ClosingTime,
                PriceRange = request.PriceRange,
                Specialties = request.Specialties ?? new List<string>(),
                CreatedByUserId = userId
            };

            var createdSpot = await _spotService.CreateAsync(spot);
            var response = MapToSpotResponse(createdSpot);

            _logger.LogInformation("Successfully created spot: {SpotName} with ID: {SpotId}", 
                createdSpot.Name, createdSpot.Id);

            return CreatedAtAction(nameof(GetSpotDetails), new { id = createdSpot.Id }, response);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid spot data provided for creation: {SpotName}", request.Name);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating spot: {SpotName}", request.Name);
            return StatusCode(500, "An error occurred while creating the spot");
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SpotResponse>> UpdateSpot(Guid id, [FromBody] UpdateSpotRequest request)
    {
        try
        {
            _logger.LogInformation("Updating spot: {SpotId}", id);

            var existingSpot = await _spotService.GetByIdAsync(id);
            if (existingSpot == null)
            {
                _logger.LogWarning("Spot not found for update with ID: {SpotId}", id);
                return NotFound($"Spot with ID {id} not found");
            }

            var location = new Models.Location { Latitude = request.Latitude, Longitude = request.Longitude };
            var point = _geospatialService.LocationToPoint(location);

            var updatedSpot = new AmalaSpot
            {
                Id = id,
                Name = request.Name,
                Description = request.Description,
                Address = request.Address,
                Location = point,
                PhoneNumber = request.PhoneNumber,
                OpeningTime = request.OpeningTime,
                ClosingTime = request.ClosingTime,
                PriceRange = request.PriceRange,
                Specialties = request.Specialties ?? new List<string>(),

                AverageRating = existingSpot.AverageRating,
                ReviewCount = existingSpot.ReviewCount,
                CreatedAt = existingSpot.CreatedAt,
                CreatedByUserId = existingSpot.CreatedByUserId,
                IsVerified = existingSpot.IsVerified
            };

            var result = await _spotService.UpdateAsync(updatedSpot);
            var response = MapToSpotResponse(result);

            _logger.LogInformation("Successfully updated spot: {SpotId}", id);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid spot data provided for update: {SpotId}", id);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating spot: {SpotId}", id);
            return StatusCode(500, "An error occurred while updating the spot");
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteSpot(Guid id)
    {
        try
        {
            _logger.LogInformation("Deleting spot: {SpotId}", id);

            var deleted = await _spotService.DeleteAsync(id);
            if (!deleted)
            {
                _logger.LogWarning("Spot not found for deletion with ID: {SpotId}", id);
                return NotFound($"Spot with ID {id} not found");
            }

            _logger.LogInformation("Successfully deleted spot: {SpotId}", id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting spot: {SpotId}", id);
            return StatusCode(500, "An error occurred while deleting the spot");
        }
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<SpotResponse>>> GetAllSpots(
        [FromQuery] int page = 1, 
        [FromQuery] [Range(1, 100)] int pageSize = 20)
    {
        try
        {
            _logger.LogInformation("Getting all spots - Page: {Page}, PageSize: {PageSize}", page, pageSize);

            var criteria = new SpotSearchCriteria
            {
                Limit = pageSize,
                Offset = (page - 1) * pageSize
            };

            var spots = await _spotService.SearchAsync(criteria);
            var spotsList = spots.ToList();

            var spotResponses = spotsList.Select(spot => MapToSpotResponse(spot)).ToList();

            var totalCount = await _spotService.GetTotalCountAsync();
            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            var response = new PagedResponse<SpotResponse>
            {
                Data = spotResponses,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages,
                HasNextPage = page < totalPages,
                HasPreviousPage = page > 1
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all spots");
            return StatusCode(500, "An error occurred while retrieving spots");
        }
    }

    [HttpGet("top-rated")]
    public async Task<ActionResult<IEnumerable<SpotResponse>>> GetTopRatedSpots(
        [FromQuery] [Range(1, 50)] int count = 10)
    {
        try
        {
            _logger.LogInformation("Getting top {Count} rated spots", count);

            var spots = await _spotService.GetTopRatedAsync(count);
            var response = spots.Select(spot => MapToSpotResponse(spot));

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting top rated spots");
            return StatusCode(500, "An error occurred while retrieving top-rated spots");
        }
    }

    [HttpGet("recent")]
    public async Task<ActionResult<IEnumerable<SpotResponse>>> GetRecentSpots(
        [FromQuery] [Range(1, 50)] int count = 10)
    {
        try
        {
            _logger.LogInformation("Getting {Count} recently added spots", count);

            var spots = await _spotService.GetRecentlyAddedAsync(count);
            var response = spots.Select(spot => MapToSpotResponse(spot));

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recent spots");
            return StatusCode(500, "An error occurred while retrieving recent spots");
        }
    }


    [HttpGet("{spotId}/weather")]
    public async Task<ActionResult<WeatherForecast>> GetSpotWeather(Guid spotId)
    {
        var spot = await _spotService.GetByIdAsync(spotId);
        if (spot == null)
            return NotFound(new { message = "Spot not found" });


        var weather = await _weatherService.GetWeatherAsync(
            spot.Location.Y,
            spot.Location.X
        );

        if (weather == null)
            return StatusCode(500, new { message = "Failed to fetch weather data" });

        return Ok(weather);
    }

    #region Private Helper Methods

    private SpotResponse MapToSpotResponse(AmalaSpot spot, Models.Location? userLocation = null, bool includeReviews = false)
    {
        var spotLocation = _geospatialService.PointToLocation(spot.Location);
        
        var response = new SpotResponse
        {
            Id = spot.Id,
            Name = spot.Name,
            Description = spot.Description,
            Address = spot.Address,
            Latitude = spotLocation.Latitude,
            Longitude = spotLocation.Longitude,
            PhoneNumber = spot.PhoneNumber,
            OpeningTime = spot.OpeningTime,
            ClosingTime = spot.ClosingTime,
            AverageRating = spot.AverageRating,
            ReviewCount = spot.ReviewCount,
            PriceRange = spot.PriceRange,
            Specialties = spot.Specialties,
            CreatedAt = spot.CreatedAt,
            UpdatedAt = spot.UpdatedAt,
            IsVerified = spot.IsVerified
        };

        if (userLocation != null)
        {
            response.DistanceKm = _geospatialService.CalculateDistance(userLocation, spotLocation);
        }

        if (includeReviews && spot.Reviews != null && spot.Reviews.Any())
        {
            response.Reviews = spot.Reviews.Select(review => new ReviewResponse
            {
                Id = review.Id,
                SpotId = review.SpotId,
                UserId = review.UserId,
                UserName = review.User != null ? $"{review.User.FirstName} {review.User.LastName}".Trim() : "Anonymous",
                Rating = review.Rating,
                Comment = review.Comment,
                CreatedAt = review.CreatedAt
            }).OrderByDescending(r => r.CreatedAt).ToList();
        }

        return response;
    }

    #endregion
}