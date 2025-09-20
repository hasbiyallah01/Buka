using AmalaSpotLocator.Core.Applications.Interfaces.Services;
using AmalaSpotLocator.Models;
using Microsoft.AspNetCore.Mvc;

namespace AmalaSpotLocator.Controllers;

[ApiController]
[Route("api/busyness")]
public class BusynessController : ControllerBase
{
    private readonly IBusynessService _busynessService;
    private readonly ILogger<BusynessController> _logger;

    public BusynessController(
        IBusynessService busynessService,
        ILogger<BusynessController> logger)
    {
        _busynessService = busynessService;
        _logger = logger;
    }
    
    [HttpPost("check")]
    public async Task<ActionResult<BusynessInfo>> GetBusynessFromGooglePlace([FromBody] GooglePlaceBusynessRequest request)
    {
        try
        {
            var busyness = await _busynessService.GetBusynessFromGooglePlaceAsync(request);
            return Ok(busyness);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting busyness for Google Place {PlaceId}", request.PlaceId);
            return StatusCode(500, "Error retrieving busyness information");
        }
    }


    [HttpGet("{spotId}")]
    public async Task<ActionResult<BusynessInfo>> GetBusyness(Guid spotId, [FromQuery] string? placeId = null)
    {
        try
        {
            var busyness = await _busynessService.GetCurrentBusynessAsync(spotId, placeId);
            return Ok(busyness);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting busyness for spot {SpotId}", spotId);
            return StatusCode(500, "Error retrieving busyness information");
        }
    }

    [HttpPost("checkin")]
    public async Task<ActionResult<BusynessInfo>> SubmitCheckIn([FromBody] CheckInRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var updatedBusyness = await _busynessService.SubmitCheckInAsync(request);
            return Ok(updatedBusyness);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting check-in for spot {SpotId}", request.SpotId);
            return StatusCode(500, "Error submitting check-in");
        }
    }

    [HttpGet("pattern/{placeId}")]
    public async Task<ActionResult<List<HourlyBusyness>>> GetWeeklyPattern(string placeId)
    {
        try
        {
            var pattern = await _busynessService.GetWeeklyPatternAsync(placeId);
            return Ok(pattern);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting weekly pattern for place {PlaceId}", placeId);
            return StatusCode(500, "Error retrieving weekly pattern");
        }
    }

    [HttpGet("{spotId}/alternatives")]
    public async Task<ActionResult<List<string>>> GetAlternatives(Guid spotId, [FromQuery] BusynessLevel currentLevel = BusynessLevel.Busy)
    {
        try
        {
            var recommendations = await _busynessService.GetAlternativeRecommendationsAsync(spotId, currentLevel);
            return Ok(recommendations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting alternatives for spot {SpotId}", spotId);
            return StatusCode(500, "Error retrieving alternatives");
        }
    }

    
    [HttpPost("simple")]
    public async Task<ActionResult<SimpleBusynessResponse>> GetSimpleBusyness([FromBody] SimpleBusynessRequest request)
    {
        try
        {
            var response = await _busynessService.GetSimpleBusynessAsync(request);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting simple busyness for {Name}", request.Name);
            return StatusCode(500, "Error retrieving busyness information");
        }
    }

    [HttpPost("batch")]
    public async Task<ActionResult<BatchBusynessResponse>> GetBatchBusyness([FromBody] BatchBusynessRequest request)
    {
        try
        {
            var response = await _busynessService.GetBatchBusynessAsync(request);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting batch busyness for {Count} places", request.Places.Count);
            return StatusCode(500, "Error retrieving busyness information");
        }
    }

    [HttpPost("quick")]
    public async Task<ActionResult<QuickBusynessResponse>> GetQuickBusyness([FromBody] QuickBusynessRequest request)
    {
        try
        {
            var response = await _busynessService.GetQuickBusynessAsync(request);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting quick busyness for place {PlaceId}", request.PlaceId);
            return StatusCode(500, "Error retrieving busyness information");
        }
    }


    [HttpGet("levels")]
    public ActionResult<Dictionary<int, string>> GetBusynessLevels()
    {
        var levels = Enum.GetValues<BusynessLevel>()
            .ToDictionary(
                level => (int)level,
                level => _busynessService.GetBusynessDescription(level)
            );
        
        return Ok(levels);
    }
}