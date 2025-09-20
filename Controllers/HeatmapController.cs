using AmalaSpotLocator.Core.Applications.Interfaces.Services;
using AmalaSpotLocator.Models;
using Microsoft.AspNetCore.Mvc;

namespace AmalaSpotLocator.Controllers;

[ApiController]
[Route("api/heatmap")]
public class HeatmapController : ControllerBase
{
    private readonly IHeatmapService _heatmapService;
    private readonly ILogger<HeatmapController> _logger;

    public HeatmapController(
        IHeatmapService heatmapService,
        ILogger<HeatmapController> logger)
    {
        _heatmapService = heatmapService;
        _logger = logger;
    }

    [HttpGet("lagos")]
    public async Task<ActionResult<HeatmapData>> GetLagosHeatmap()
    {
        try
        {
            _logger.LogInformation("Generating Lagos amala heatmap");
            var heatmap = await _heatmapService.GenerateLagosAmalaHeatmapAsync();
            return Ok(heatmap);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating Lagos heatmap");
            return StatusCode(500, "Error generating heatmap data");
        }
    }

    [HttpGet("underserved")]
    public async Task<ActionResult<List<UnderservedArea>>> GetUnderservedAreas()
    {
        try
        {
            var areas = await _heatmapService.IdentifyUnderservedAreasAsync();
            return Ok(areas);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting underserved areas");
            return StatusCode(500, "Error retrieving underserved areas");
        }
    }

    [HttpGet("opportunities")]
    public async Task<ActionResult<List<BusinessOpportunity>>> GetBusinessOpportunities()
    {
        try
        {
            var opportunities = await _heatmapService.GetBusinessOpportunitiesAsync();
            return Ok(opportunities);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting business opportunities");
            return StatusCode(500, "Error retrieving business opportunities");
        }
    }

    [HttpPost("analyze")]
    public async Task<ActionResult<GooglePlacesHeatmapResponse>> AnalyzeGooglePlaces([FromBody] GooglePlacesHeatmapRequest request)
    {
        try
        {
            var analysis = await _heatmapService.AnalyzeGooglePlacesAsync(request);
            return Ok(analysis);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing Google Places for heatmap");
            return StatusCode(500, "Error analyzing places");
        }
    }

    [HttpGet("density")]
    public async Task<ActionResult<HeatmapPoint>> GetAreaDensity(
        [FromQuery] double latitude,
        [FromQuery] double longitude,
        [FromQuery] double radiusKm = 2)
    {
        try
        {
            var location = new Location(latitude, longitude);
            var density = await _heatmapService.GetAreaDensityAsync(location, radiusKm);
            return Ok(density);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting area density for {Lat}, {Lng}", latitude, longitude);
            return StatusCode(500, "Error calculating area density");
        }
    }

    [HttpGet("insights")]
    public async Task<ActionResult<object>> GetHeatmapInsights()
    {
        try
        {
            var heatmap = await _heatmapService.GenerateLagosAmalaHeatmapAsync();
            
            var insights = new
            {
                TotalSpots = heatmap.TotalSpots,
                AverageIntensity = heatmap.AverageIntensity,
                HotspotAreas = heatmap.Points
                    .Where(p => p.Category >= HeatmapCategory.High)
                    .OrderByDescending(p => p.Intensity)
                    .Take(5)
                    .Select(p => new { 
                        Location = p.Location, 
                        SpotCount = p.SpotCount, 
                        Intensity = p.Intensity,
                        Description = p.Description
                    }),
                UnderservedCount = heatmap.UnderservedAreas.Count,
                TopOpportunities = heatmap.BusinessOpportunities
                    .Take(3)
                    .Select(o => new { 
                        Area = o.AreaName, 
                        Score = o.OpportunityScore,
                        Investment = o.RecommendedInvestment,
                        ROI = o.EstimatedROI
                    }),
                Summary = heatmap.Summary,
                Recommendations = GenerateRecommendations(heatmap)
            };

            return Ok(insights);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating heatmap insights");
            return StatusCode(500, "Error generating insights");
        }
    }

    private List<string> GenerateRecommendations(HeatmapData heatmap)
    {
        var recommendations = new List<string>();

        if (heatmap.UnderservedAreas.Any(a => a.Severity == UnderservedSeverity.Critical))
        {
            recommendations.Add("🚨 Critical opportunity: Several areas have zero amala spots despite high population");
        }

        var topOpportunity = heatmap.BusinessOpportunities.FirstOrDefault();
        if (topOpportunity != null)
        {
            recommendations.Add($"💰 Best investment opportunity: {topOpportunity.AreaName} with {topOpportunity.OpportunityScore:F0}/100 score");
        }

        var hotspots = heatmap.Points.Count(p => p.Category >= HeatmapCategory.High);
        if (hotspots > 0)
        {
            recommendations.Add($"🔥 {hotspots} amala hotspots identified - high competition but proven demand");
        }

        if (heatmap.AverageIntensity < 30)
        {
            recommendations.Add("📈 Overall low density suggests room for market expansion across Lagos");
        }

        return recommendations;
    }
}