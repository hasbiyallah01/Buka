/*using AmalaSpotLocator.Core.Applications.Interfaces.Services;
using AmalaSpotLocator.Models;
using Microsoft.AspNetCore.Mvc;

namespace AmalaSpotLocator.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DemoController : ControllerBase
{
    private readonly IBusynessService _busynessService;
    private readonly IHeatmapService _heatmapService;
    private readonly ILogger<DemoController> _logger;

    public DemoController(
        IBusynessService busynessService,
        IHeatmapService heatmapService,
        ILogger<DemoController> logger)
    {
        _busynessService = busynessService;
        _heatmapService = heatmapService;
        _logger = logger;
    }

    [HttpGet("queue-demo")]
    public async Task<ActionResult<object>> GetQueueDemo()
    {
        try
        {
            var demoSpots = new[]
            {
                new { Name = "Mama Put Ikeja", SpotId = Guid.NewGuid(), PlaceId = "demo_1" },
                new { Name = "Buka Mama Surulere", SpotId = Guid.NewGuid(), PlaceId = "demo_2" },
                new { Name = "Mama Sade's Place", SpotId = Guid.NewGuid(), PlaceId = "demo_3" }
            };

            var results = new List<object>();

            foreach (var spot in demoSpots)
            {
                var busyness = await _busynessService.GetCurrentBusynessAsync(spot.SpotId, spot.PlaceId);
                
                results.Add(new
                {
                    SpotName = spot.Name,
                    CurrentLevel = busyness.CurrentLevel.ToString(),
                    Description = busyness.Description,
                    EstimatedWait = $"{busyness.EstimatedWaitMinutes} minutes",
                    Recommendations = busyness.Recommendations,
                    LastUpdated = busyness.LastUpdated.ToString("HH:mm"),
                    DemoMessage = GenerateDemoMessage(spot.Name, busyness)
                });
            }

            return Ok(new
            {
                Title = "🍽️ Queue Estimation Demo - Lagos Amala Spots",
                Description = "Real-time busyness levels help you avoid long queues",
                Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                Spots = results,
                Features = new[]
                {
                    "✅ Real-time crowd-sourced check-ins",
                    "✅ Google Places popular times integration", 
                    "✅ Smart wait time estimation",
                    "✅ Alternative spot recommendations",
                    "✅ Solves Nigerian lunch queue pain point"
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in queue demo");
            return StatusCode(500, "Demo error");
        }
    }

    [HttpGet("heatmap-demo")]
    public async Task<ActionResult<object>> GetHeatmapDemo()
    {
        try
        {
            var heatmap = await _heatmapService.GenerateLagosAmalaHeatmapAsync();
            
            var hotspots = heatmap.Points
                .Where(p => p.Category >= HeatmapCategory.High)
                .OrderByDescending(p => p.Intensity)
                .Take(5)
                .Select(p => new
                {
                    Area = $"Lat: {p.Location.Latitude:F3}, Lng: {p.Location.Longitude:F3}",
                    SpotCount = p.SpotCount,
                    Intensity = $"{p.Intensity:F0}/100",
                    Category = p.Category.ToString(),
                    Description = p.Description
                });

            var topOpportunities = heatmap.BusinessOpportunities
                .Take(3)
                .Select(o => new
                {
                    Area = o.AreaName,
                    Score = $"{o.OpportunityScore:F0}/100",
                    Investment = o.RecommendedInvestment,
                    ROI = o.EstimatedROI,
                    Competition = o.CompetitionLevel.ToString(),
                    Reasons = o.Reasons.Take(2)
                });

            return Ok(new
            {
                Title = "🗺️ Lagos Amala Heatmap Demo",
                Description = "Data storytelling showing where amala dey choke pass in Lagos",
                Summary = heatmap.Summary,
                Statistics = new
                {
                    TotalSpots = heatmap.TotalSpots,
                    AverageIntensity = $"{heatmap.AverageIntensity:F1}/100",
                    HotspotsFound = hotspots.Count(),
                    UnderservedAreas = heatmap.UnderservedAreas.Count,
                    BusinessOpportunities = heatmap.BusinessOpportunities.Count
                },
                Hotspots = hotspots,
                TopBusinessOpportunities = topOpportunities,
                UnderservedAreas = heatmap.UnderservedAreas.Take(3).Select(u => new
                {
                    Area = u.AreaName,
                    Population = $"{u.Population:N0} people",
                    CurrentSpots = u.CurrentSpotCount,
                    SpotsPerCapita = $"{u.SpotsPerCapita:F1} per 100k",
                    Severity = u.Severity.ToString(),
                    Opportunity = u.OpportunityDescription
                }),
                Features = new[]
                {
                    "✅ Live heatmap of amala density across Lagos",
                    "✅ Identifies underserved areas (no amala spots)",
                    "✅ Business opportunity analysis for investors",
                    "✅ Data-driven insights for market expansion",
                    "✅ Great for demo presentations and storytelling"
                },
                DemoInsights = new[]
                {
                    "🔥 Victoria Island and Ikeja are amala hotspots",
                    "🎯 Lekki and Ajah are underserved despite high population",
                    "💰 Best ROI opportunities in growing residential areas",
                    "📈 Market has room for 50+ new quality amala spots"
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in heatmap demo");
            return StatusCode(500, "Demo error");
        }
    }

    [HttpPost("simple-demo")]
    public async Task<ActionResult<object>> SimpleDemo([FromBody] SimpleDemoRequest request)
    {
        try
        {
            var busynessRequest = new BatchBusynessRequest
            {
                Places = request.Places.Select(p => new SimpleBusynessRequest
                {
                    Name = p.Name,
                    Latitude = p.Latitude,
                    Longitude = p.Longitude,
                    Rating = p.Rating,
                    IsOpen = p.IsOpen
                }).ToList(),
                IncludeHeatmap = request.IncludeHeatmap
            };

            var response = await _busynessService.GetBatchBusynessAsync(busynessRequest);

            return Ok(new
            {
                Title = "🚀 Simple Busyness Check Demo",
                Description = "Just send name, lat, lng - get instant busyness levels!",
                Results = response.Places.Select(p => new
                {
                    Name = p.Name,
                    Status = p.BusynessLevel,
                    WaitTime = $"{p.WaitMinutes} minutes",
                    Message = p.Message,
                    Tips = p.Tips
                }),
                HeatmapSummary = response.HeatmapSummary,
                Usage = new
                {
                    Endpoint = "POST /api/demo/simple-demo",
                    MinimalRequest = new
                    {
                        places = new[]
                        {
                            new { name = "Mama Put Ikeja", latitude = 6.6018, longitude = 3.3515 }
                        }
                    },
                    OptionalFields = new
                    {
                        rating = "Helps improve accuracy",
                        isOpen = "Current open status",
                        includeHeatmap = "Get area analysis"
                    }
                },
                Benefits = new[]
                {
                    "✅ Minimal data required - just name and location",
                    "✅ Instant busyness analysis",
                    "✅ Smart wait time estimation",
                    "✅ Helpful tips when busy",
                    "✅ Optional heatmap summary"
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in simple demo");
            return StatusCode(500, "Demo error");
        }
    }

    [HttpPost("google-places-demo")]
    public async Task<ActionResult<object>> AnalyzeGooglePlacesDemo([FromBody] GooglePlacesDemoRequest request)
    {
        try
        {
            var results = new List<object>();

            foreach (var place in request.Places)
            {
                var busynessRequest = new GooglePlaceBusynessRequest
                {
                    PlaceId = place.PlaceId,
                    Name = place.Name,
                    Address = place.Address,
                    Location = place.Location,
                    Rating = place.Rating,
                    UserRatingsTotal = place.UserRatingsTotal,
                    PriceLevel = place.PriceLevel,
                    Types = place.Types,
                    IsOpenNow = place.IsOpenNow
                };

                var busyness = await _busynessService.GetBusynessFromGooglePlaceAsync(busynessRequest);
                
                results.Add(new
                {
                    PlaceId = place.PlaceId,
                    Name = place.Name,
                    Address = place.Address,
                    Rating = place.Rating,
                    BusynessLevel = busyness.CurrentLevel.ToString(),
                    Description = busyness.Description,
                    EstimatedWait = $"{busyness.EstimatedWaitMinutes} minutes",
                    Recommendations = busyness.Recommendations,
                    QuickMessage = GenerateDemoMessage(place.Name, busyness.CurrentLevel, busyness.EstimatedWaitMinutes),
                    PopularityScore = busyness.PopularityScore
                });
            }

            object? heatmapAnalysis = null;
            if (request.IncludeHeatmap && request.Places.Count > 1)
            {
                var heatmapRequest = new GooglePlacesHeatmapRequest
                {
                    Places = request.Places.Select(p => new GooglePlaceBusynessRequest
                    {
                        PlaceId = p.PlaceId,
                        Name = p.Name,
                        Address = p.Address,
                        Location = p.Location,
                        Rating = p.Rating,
                        UserRatingsTotal = p.UserRatingsTotal,
                        PriceLevel = p.PriceLevel,
                        Types = p.Types,
                        IsOpenNow = p.IsOpenNow
                    }).ToList(),
                    CenterLocation = request.CenterLocation,
                    RadiusKm = request.RadiusKm,
                    IncludeBusynessAnalysis = true,
                    IncludeBusinessOpportunities = true,
                    IncludeUnderservedAreas = true
                };

                var heatmapResponse = await _heatmapService.AnalyzeGooglePlacesAsync(heatmapRequest);
                
                heatmapAnalysis = new
                {
                    Summary = heatmapResponse.Insights.Summary,
                    TotalPlaces = heatmapResponse.PlacesWithBusyness.Count,
                    AverageRating = heatmapResponse.HeatmapAnalysis.AverageRating,
                    BusyPlaces = heatmapResponse.PlacesWithBusyness.Count(p => p.BusynessInfo.CurrentLevel >= BusynessLevel.Busy),
                    Hotspots = heatmapResponse.HeatmapAnalysis.Hotspots.Take(3).Select(h => new
                    {
                        Location = h.Center,
                        PlaceCount = h.PlaceCount,
                        Description = h.Description,
                        TopPlaces = h.TopPlaces
                    }),
                    BusinessOpportunities = heatmapResponse.BusinessOpportunities.Take(3).Select(o => new
                    {
                        Area = o.AreaName,
                        Score = $"{o.OpportunityScore:F0}/100",
                        Investment = o.RecommendedInvestment,
                        ROI = o.EstimatedROI
                    }),
                    KeyFindings = heatmapResponse.Insights.KeyFindings,
                    Recommendations = heatmapResponse.Insights.Recommendations
                };
            }

            return Ok(new
            {
                Title = "🚀 Google Places Integration Demo",
                Description = "Frontend sends Google Places data directly to get real-time busyness and heatmap analysis",
                ProcessedPlaces = results,
                HeatmapAnalysis = heatmapAnalysis,
                Features = new[]
                {
                    "✅ Direct Google Places data integration",
                    "✅ Real-time busyness analysis without database storage",
                    "✅ Instant heatmap generation from frontend data",
                    "✅ Business opportunity identification",
                    "✅ Smart recommendations based on current conditions"
                },
                Usage = new
                {
                    Endpoint = "POST /api/demo/google-places-demo",
                    Description = "Send array of Google Places data to get busyness analysis",
                    SampleRequest = "{ \"places\": [{ \"placeId\": \"...\", \"name\": \"...\", \"location\": {...} }], \"includeHeatmap\": true }"
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Google Places demo");
            return StatusCode(500, "Demo error");
        }
    }

    [HttpPost("checkin-demo")]
    public async Task<ActionResult<object>> SubmitDemoCheckIn([FromBody] DemoCheckInRequest request)
    {
        try
        {
            var checkIn = new CheckInRequest
            {
                SpotId = request.SpotId,
                ReportedLevel = request.BusynessLevel,
                EstimatedWaitMinutes = request.WaitMinutes,
                Notes = request.Notes
            };

            var updatedBusyness = await _busynessService.SubmitCheckInAsync(checkIn);

            return Ok(new
            {
                Success = true,
                Message = "✅ Check-in submitted successfully!",
                SpotName = request.SpotName,
                YourReport = new
                {
                    BusynessLevel = request.BusynessLevel.ToString(),
                    WaitTime = $"{request.WaitMinutes} minutes",
                    Notes = request.Notes
                },
                UpdatedInfo = new
                {
                    CurrentLevel = updatedBusyness.CurrentLevel.ToString(),
                    Description = updatedBusyness.Description,
                    EstimatedWait = $"{updatedBusyness.EstimatedWaitMinutes} minutes",
                    TotalCheckIns = updatedBusyness.CheckInCount,
                    LastUpdated = updatedBusyness.LastUpdated.ToString("HH:mm")
                },
                Recommendations = updatedBusyness.Recommendations,
                DemoNote = "This demonstrates how crowd-sourced check-ins improve real-time busyness accuracy"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in check-in demo");
            return StatusCode(500, "Demo error");
        }
    }

    private string GenerateDemoMessage(string spotName, BusynessInfo busyness)
    {
        return busyness.CurrentLevel switch
        {
            BusynessLevel.VeryQuiet => $"✅ {spotName} is very quiet right now - perfect time to visit!",
            BusynessLevel.Quiet => $"😊 {spotName} has minimal wait - good time to go",
            BusynessLevel.Moderate => $"⏰ {spotName} is moderately busy - expect short wait",
            BusynessLevel.Busy => $"🕐 {spotName} is busy - maybe try the alternatives suggested",
            BusynessLevel.VeryBusy => $"⚠️ {spotName} is very busy - long wait expected",
            BusynessLevel.Packed => $"🚨 {spotName} is packed right now — maybe try Mama Sade's place nearby (shorter wait time)",
            _ => $"ℹ️ {spotName} busyness level unknown"
        };
    }

    private string GenerateDemoMessage(string spotName, BusynessLevel level, int waitMinutes)
    {
        return level switch
        {
            BusynessLevel.VeryQuiet => $"✅ {spotName} is very quiet - perfect time to visit!",
            BusynessLevel.Quiet => $"😊 {spotName} has minimal wait ({waitMinutes} min)",
            BusynessLevel.Moderate => $"⏰ {spotName} is moderately busy (~{waitMinutes} min wait)",
            BusynessLevel.Busy => $"🕐 {spotName} is busy - {waitMinutes} min wait expected",
            BusynessLevel.VeryBusy => $"⚠️ {spotName} is very busy - {waitMinutes} min wait",
            BusynessLevel.Packed => $"🚨 {spotName} is packed! {waitMinutes}+ min wait - try alternatives",
            _ => $"ℹ️ {spotName} - wait time unknown"
        };
    }
}

public class DemoCheckInRequest
{
    public Guid SpotId { get; set; }
    public string SpotName { get; set; } = string.Empty;
    public BusynessLevel BusynessLevel { get; set; }
    public int WaitMinutes { get; set; }
    public string? Notes { get; set; }
}

public class GooglePlacesDemoRequest
{
    public List<GooglePlaceDemoData> Places { get; set; } = new();
    public Location? CenterLocation { get; set; }
    public double RadiusKm { get; set; } = 10;
    public bool IncludeHeatmap { get; set; } = true;
}

public class SimpleDemoRequest
{
    public List<SimpleDemoPlace> Places { get; set; } = new();
    public bool IncludeHeatmap { get; set; } = false;
}

public class SimpleDemoPlace
{
    public string Name { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public decimal? Rating { get; set; } // Optional
    public bool? IsOpen { get; set; } // Optional
}

public class GooglePlaceDemoData
{
    public string PlaceId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public Location Location { get; set; } = new();
    public decimal? Rating { get; set; }
    public int? UserRatingsTotal { get; set; }
    public PriceRange? PriceLevel { get; set; }
    public List<string> Types { get; set; } = new();
    public bool? IsOpenNow { get; set; }
}*/