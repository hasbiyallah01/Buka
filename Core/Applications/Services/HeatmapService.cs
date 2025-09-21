using AmalaSpotLocator.Models;
using AmalaSpotLocator.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using AmalaSpotLocator.Core.Applications.Interfaces.Services;

namespace AmalaSpotLocator.Core.Applications.Services;

public class HeatmapService : IHeatmapService
{
    private readonly ISpotService _spotService;
    private readonly IGeospatialService _geospatialService;
    private readonly IBusynessService _busynessService;
    private readonly ILogger<HeatmapService> _logger;
    private readonly Location _lagosNorthWest = new(6.7, 3.2);
    private readonly Location _lagosSouthEast = new(6.4, 3.6);

    public HeatmapService(
        ISpotService spotService,
        IGeospatialService geospatialService,
        IBusynessService busynessService,
        ILogger<HeatmapService> logger)
    {
        _spotService = spotService;
        _geospatialService = geospatialService;
        _busynessService = busynessService;
        _logger = logger;
    }

    public async Task<HeatmapData> GenerateLagosAmalaHeatmapAsync()
    {
        try
        {
            _logger.LogInformation("Generating Lagos Amala heatmap");

            var heatmapPoints = new List<HeatmapPoint>();
            var gridSize = 0.02; 
            for (double lat = _lagosNorthWest.Latitude; lat >= _lagosSouthEast.Latitude; lat -= gridSize)
            {
                for (double lng = _lagosNorthWest.Longitude; lng <= _lagosSouthEast.Longitude; lng += gridSize)
                {
                    var gridLocation = new Location(lat, lng);
                    var density = await GetAreaDensityAsync(gridLocation, 1.5); 
                    
                    if (density.Intensity > 0)
                    {
                        heatmapPoints.Add(density);
                    }
                }
            }

            var underservedAreas = await IdentifyUnderservedAreasAsync();
            var businessOpportunities = await GetBusinessOpportunitiesAsync();

            var heatmapData = new HeatmapData
            {
                Points = heatmapPoints,
                UnderservedAreas = underservedAreas,
                BusinessOpportunities = businessOpportunities,
                GeneratedAt = DateTime.UtcNow,
                TotalSpots = heatmapPoints.Sum(p => p.SpotCount),
                AverageIntensity = heatmapPoints.Any() ? heatmapPoints.Average(p => p.Intensity) : 0
            };

            _logger.LogInformation("Generated heatmap with {PointCount} points, {UnderservedCount} underserved areas", 
                heatmapPoints.Count, underservedAreas.Count);

            return heatmapData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating Lagos Amala heatmap");
            throw;
        }
    }

    public async Task<List<UnderservedArea>> IdentifyUnderservedAreasAsync()
    {
        try
        {
            var underservedAreas = new List<UnderservedArea>();
            var gridSize = 0.05; 
            var populationCenters = new[]
            {
                new { Location = new Location(6.5244, 3.3792), Name = "Victoria Island", Population = 500000 },
                new { Location = new Location(6.4541, 3.3947), Name = "Ikoyi", Population = 300000 },
                new { Location = new Location(6.5795, 3.3211), Name = "Surulere", Population = 800000 },
                new { Location = new Location(6.6018, 3.3515), Name = "Ikeja", Population = 600000 },
                new { Location = new Location(6.4698, 3.5852), Name = "Ajah", Population = 400000 },
                new { Location = new Location(6.6269, 3.3424), Name = "Agege", Population = 700000 },
                new { Location = new Location(6.5833, 3.2500), Name = "Mushin", Population = 900000 },
                new { Location = new Location(6.4433, 3.4083), Name = "Lekki", Population = 350000 }
            };

            foreach (var center in populationCenters)
            {
                var density = await GetAreaDensityAsync(center.Location, 3); 
                var spotsPerCapita = density.SpotCount / (double)center.Population * 100000; 

                if (spotsPerCapita < 5) 
                {
                    underservedAreas.Add(new UnderservedArea
                    {
                        Location = center.Location,
                        AreaName = center.Name,
                        Population = center.Population,
                        CurrentSpotCount = density.SpotCount,
                        SpotsPerCapita = spotsPerCapita,
                        RecommendedSpots = Math.Max(1, (int)(center.Population / 50000)), 
                        Severity = CalculateSeverity(spotsPerCapita),
                        Reasons = GenerateUnderservedReasons(center.Name, spotsPerCapita, density.SpotCount)
                    });
                }
            }

            return underservedAreas.OrderByDescending(a => a.Severity).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error identifying underserved areas");
            return new List<UnderservedArea>();
        }
    }

    public async Task<HeatmapPoint> GetAreaDensityAsync(Location location, double radiusKm = 2)
    {
        try
        {
            var nearbySpots = await _spotService.GetNearbyAsync(location, radiusKm);
            var spotList = nearbySpots.ToList();

            var totalBusyness = 0.0;
            var busynessCount = 0;

            foreach (var spot in spotList)
            {
                try
                {
                    var busyness = await _busynessService.GetCurrentBusynessAsync(spot.Id);
                    if (busyness.PopularityScore.HasValue)
                    {
                        totalBusyness += busyness.PopularityScore.Value;
                        busynessCount++;
                    }
                }
                catch
                {
                }
            }

            var averageBusyness = busynessCount > 0 ? totalBusyness / busynessCount : 50;
            var intensity = CalculateIntensity(spotList.Count, averageBusyness, radiusKm);

            return new HeatmapPoint
            {
                Location = location,
                SpotCount = spotList.Count,
                Intensity = intensity,
                AverageBusyness = averageBusyness,
                Radius = radiusKm,
                Category = CategorizeIntensity(intensity)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating area density for location {Lat}, {Lng}", 
                location.Latitude, location.Longitude);
            return new HeatmapPoint
            {
                Location = location,
                SpotCount = 0,
                Intensity = 0,
                AverageBusyness = 0,
                Radius = radiusKm,
                Category = HeatmapCategory.None
            };
        }
    }

    public async Task<List<BusinessOpportunity>> GetBusinessOpportunitiesAsync()
    {
        try
        {
            var opportunities = new List<BusinessOpportunity>();
            var underservedAreas = await IdentifyUnderservedAreasAsync();

            foreach (var area in underservedAreas.Take(10)) 
            {
                var opportunity = new BusinessOpportunity
                {
                    Location = area.Location,
                    AreaName = area.AreaName,
                    OpportunityScore = CalculateOpportunityScore(area),
                    EstimatedDemand = area.Population / 25000, 
                    CompetitionLevel = GetCompetitionLevel(area.CurrentSpotCount, area.Population),
                    RecommendedInvestment = CalculateInvestment(area.Population, area.Severity),
                    Reasons = new List<string>
                    {
                        $"High population density ({area.Population:N0} people)",
                        $"Low amala spot availability ({area.SpotsPerCapita:F1} per 100k people)",
                        $"Potential for {area.RecommendedSpots} new establishments",
                        GetMarketInsight(area.AreaName)
                    },
                    RiskFactors = GetRiskFactors(area.AreaName),
                    EstimatedROI = CalculateROI(area.Population, area.CurrentSpotCount)
                };

                opportunities.Add(opportunity);
            }

            return opportunities.OrderByDescending(o => o.OpportunityScore).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating business opportunities");
            return new List<BusinessOpportunity>();
        }
    }

    private double CalculateIntensity(int spotCount, double averageBusyness, double radiusKm)
    {
        var area = Math.PI * radiusKm * radiusKm;
        var density = spotCount / area;
        var busynessFactor = averageBusyness / 100.0;
        return Math.Min(100, (density * 20) + (busynessFactor * 30));
    }

    private HeatmapCategory CategorizeIntensity(double intensity)
    {
        return intensity switch
        {
            >= 80 => HeatmapCategory.VeryHigh,
            >= 60 => HeatmapCategory.High,
            >= 40 => HeatmapCategory.Medium,
            >= 20 => HeatmapCategory.Low,
            > 0 => HeatmapCategory.VeryLow,
            _ => HeatmapCategory.None
        };
    }

    private UnderservedSeverity CalculateSeverity(double spotsPerCapita)
    {
        return spotsPerCapita switch
        {
            < 1 => UnderservedSeverity.Critical,
            < 2 => UnderservedSeverity.High,
            < 3 => UnderservedSeverity.Medium,
            _ => UnderservedSeverity.Low
        };
    }

    private List<string> GenerateUnderservedReasons(string areaName, double spotsPerCapita, int currentSpots)
    {
        var reasons = new List<string>
        {
            $"Only {spotsPerCapita:F1} amala spots per 100,000 residents",
            $"Current supply: {currentSpots} spots in the area"
        };

        if (spotsPerCapita < 1)
            reasons.Add("Critically underserved - immediate opportunity for new businesses");
        else if (spotsPerCapita < 2)
            reasons.Add("High demand potential for quality amala restaurants");

        reasons.Add($"{areaName} residents likely travel far for authentic amala");
        
        return reasons;
    }

    private double CalculateOpportunityScore(UnderservedArea area)
    {
        var populationScore = Math.Min(50, area.Population / 10000.0); 
        var scarcityScore = Math.Max(0, 30 - area.SpotsPerCapita * 6); 
        var severityScore = (int)area.Severity * 5; 
        
        return populationScore + scarcityScore + severityScore;
    }

    private CompetitionLevel GetCompetitionLevel(int currentSpots, int population)
    {
        var spotsPerCapita = currentSpots / (double)population * 100000;
        return spotsPerCapita switch
        {
            < 1 => CompetitionLevel.VeryLow,
            < 3 => CompetitionLevel.Low,
            < 6 => CompetitionLevel.Medium,
            < 10 => CompetitionLevel.High,
            _ => CompetitionLevel.VeryHigh
        };
    }

    private string CalculateInvestment(int population, UnderservedSeverity severity)
    {
        var baseAmount = severity switch
        {
            UnderservedSeverity.Critical => 2000000, 
            UnderservedSeverity.High => 1500000,     
            UnderservedSeverity.Medium => 1000000,   
            _ => 800000                              
        };

        var populationFactor = population > 500000 ? 1.3 : 1.0;
        var finalAmount = (int)(baseAmount * populationFactor);

        return $"₦{finalAmount:N0} - ₦{finalAmount * 1.5:N0}";
    }

    private string GetMarketInsight(string areaName)
    {
        var insights = new Dictionary<string, string>
        {
            { "Victoria Island", "High-income area with office workers seeking quality lunch options" },
            { "Surulere", "Dense residential area with strong local food culture" },
            { "Ikeja", "Commercial hub with consistent foot traffic" },
            { "Ajah", "Growing residential area with young families" },
            { "Agege", "Traditional Yoruba community with authentic food appreciation" },
            { "Mushin", "High-density area with strong demand for affordable meals" },
            { "Lekki", "Upscale area with residents willing to pay premium for quality" }
        };

        return insights.TryGetValue(areaName, out var insight) ? insight : "Growing area with food service potential";
    }

    private List<string> GetRiskFactors(string areaName)
    {
        return new List<string>
        {
            "Market acceptance of new establishments",
            "Competition from existing local food vendors",
            "Seasonal demand fluctuations",
            "Supply chain reliability for fresh ingredients"
        };
    }

    private string CalculateROI(int population, int currentSpots)
    {
        var marketPenetration = currentSpots / (double)population * 100000;
        var roi = marketPenetration switch
        {
            < 1 => "25-40%",
            < 2 => "20-35%",
            < 3 => "15-30%",
            _ => "10-25%"
        };
        return $"{roi} annually";
    }

    public async Task<GooglePlacesHeatmapResponse> AnalyzeGooglePlacesAsync(GooglePlacesHeatmapRequest request)
    {
        try
        {
            _logger.LogInformation("Analyzing {Count} Google Places for heatmap", request.Places.Count);

            var response = new GooglePlacesHeatmapResponse();
            var placesWithBusyness = new List<GooglePlaceWithBusyness>();
            foreach (var place in request.Places)
            {
                try
                {
                    var busyness = await _busynessService.GetBusynessFromGooglePlaceAsync(place);
                    var distanceFromCenter = request.CenterLocation != null 
                        ? _geospatialService.CalculateDistance(request.CenterLocation, place.Location)
                        : 0;

                    var placeWithBusyness = new GooglePlaceWithBusyness
                    {
                        PlaceId = place.PlaceId,
                        Name = place.Name,
                        Address = place.Address,
                        Location = place.Location,
                        Rating = place.Rating,
                        UserRatingsTotal = place.UserRatingsTotal,
                        PriceLevel = place.PriceLevel,
                        BusynessInfo = busyness,
                        DistanceFromCenter = distanceFromCenter,
                        DensityCategory = CalculateDensityCategory(place, request.Places, request.RadiusKm),
                        Specialties = ExtractSpecialtiesFromGooglePlace(place),
                        RecommendationMessage = GenerateRecommendationMessage(place, busyness)
                    };

                    placesWithBusyness.Add(placeWithBusyness);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to analyze place {PlaceId}", place.PlaceId);
                }
            }

            response.PlacesWithBusyness = placesWithBusyness.OrderBy(p => p.DistanceFromCenter).ToList();
            if (request.IncludeBusynessAnalysis)
            {
                response.HeatmapAnalysis = GenerateHeatmapAnalysis(placesWithBusyness, request);
            }
            if (request.IncludeBusinessOpportunities)
            {
                response.BusinessOpportunities = await GenerateBusinessOpportunitiesFromPlaces(placesWithBusyness, request);
            }
            if (request.IncludeUnderservedAreas)
            {
                response.UnderservedAreas = await IdentifyUnderservedAreasFromPlaces(placesWithBusyness, request);
            }
            response.Insights = GenerateHeatmapInsights(response);

            _logger.LogInformation("Completed heatmap analysis for {Count} places", placesWithBusyness.Count);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing Google Places for heatmap");
            throw;
        }
    }

    private HeatmapCategory CalculateDensityCategory(GooglePlaceBusynessRequest place, List<GooglePlaceBusynessRequest> allPlaces, double radiusKm)
    {
        var nearbyCount = allPlaces.Count(p => 
            p.PlaceId != place.PlaceId &&
            _geospatialService.CalculateDistance(place.Location, p.Location) <= radiusKm);

        return nearbyCount switch
        {
            >= 10 => HeatmapCategory.VeryHigh,
            >= 7 => HeatmapCategory.High,
            >= 4 => HeatmapCategory.Medium,
            >= 2 => HeatmapCategory.Low,
            >= 1 => HeatmapCategory.VeryLow,
            _ => HeatmapCategory.None
        };
    }

    private List<string> ExtractSpecialtiesFromGooglePlace(GooglePlaceBusynessRequest place)
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
            { "jollof", "Jollof Rice" },
            { "pepper soup", "Pepper Soup" },
            { "nigerian", "Nigerian Cuisine" },
            { "yoruba", "Yoruba Cuisine" },
            { "local food", "Local Nigerian Food" }
        };
        var textToCheck = $"{place.Name} {string.Join(" ", place.Reviews.Select(r => r.Text))}".ToLowerInvariant();
        
        foreach (var (searchTerm, displayName) in foodTerms)
        {
            if (textToCheck.Contains(searchTerm) && !specialties.Contains(displayName))
            {
                specialties.Add(displayName);
            }
        }
        if (!specialties.Any())
        {
            if (place.Types.Any(t => t.Contains("restaurant") || t.Contains("food")))
            {
                specialties.Add("Amala");
            }
        }

        return specialties;
    }

    private string GenerateRecommendationMessage(GooglePlaceBusynessRequest place, BusynessInfo busyness)
    {
        var message = $"{place.Name}: {busyness.Description}";
        
        if (busyness.EstimatedWaitMinutes > 0)
        {
            message += $" (~{busyness.EstimatedWaitMinutes} min wait)";
        }

        if (busyness.Recommendations.Any())
        {
            message += $" - {busyness.Recommendations.First()}";
        }

        return message;
    }

    private HeatmapAnalysis GenerateHeatmapAnalysis(List<GooglePlaceWithBusyness> places, GooglePlacesHeatmapRequest request)
    {
        var analysis = new HeatmapAnalysis
        {
            TotalPlaces = places.Count,
            AverageRating = places.Where(p => p.Rating.HasValue).Average(p => (double)p.Rating!.Value),
            AverageBusyness = places.Average(p => (double)p.BusynessInfo.CurrentLevel)
        };
        analysis.DensityDistribution = places
            .GroupBy(p => p.DensityCategory)
            .ToDictionary(g => g.Key, g => g.Count());
        analysis.BusynessDistribution = places
            .GroupBy(p => p.BusynessInfo.CurrentLevel)
            .ToDictionary(g => g.Key, g => g.Count());
        analysis.PriceDistribution = places
            .Where(p => p.PriceLevel.HasValue)
            .GroupBy(p => p.PriceLevel!.Value)
            .ToDictionary(g => g.Key, g => g.Count());
        analysis.Hotspots = IdentifyHotspots(places, request.RadiusKm);
        analysis.Coldspots = IdentifyColdspots(places, request);

        return analysis;
    }

    private List<HeatmapHotspot> IdentifyHotspots(List<GooglePlaceWithBusyness> places, double radiusKm)
    {
        var hotspots = new List<HeatmapHotspot>();
        var processedPlaces = new HashSet<string>();

        foreach (var place in places.Where(p => p.DensityCategory >= HeatmapCategory.High))
        {
            if (processedPlaces.Contains(place.PlaceId)) continue;

            var nearbyPlaces = places.Where(p => 
                _geospatialService.CalculateDistance(place.Location, p.Location) <= radiusKm).ToList();

            if (nearbyPlaces.Count >= 3)
            {
                var hotspot = new HeatmapHotspot
                {
                    Center = place.Location,
                    PlaceCount = nearbyPlaces.Count,
                    AverageRating = nearbyPlaces.Where(p => p.Rating.HasValue).Average(p => (double)p.Rating!.Value),
                    AverageBusyness = nearbyPlaces.Average(p => (double)p.BusynessInfo.CurrentLevel),
                    RadiusKm = radiusKm,
                    Description = $"Amala hotspot with {nearbyPlaces.Count} spots",
                    TopPlaces = nearbyPlaces.OrderByDescending(p => p.Rating ?? 0).Take(3).Select(p => p.Name).ToList()
                };

                hotspots.Add(hotspot);
                foreach (var nearby in nearbyPlaces)
                {
                    processedPlaces.Add(nearby.PlaceId);
                }
            }
        }

        return hotspots.OrderByDescending(h => h.PlaceCount).Take(5).ToList();
    }

    private List<HeatmapColdspot> IdentifyColdspots(List<GooglePlaceWithBusyness> places, GooglePlacesHeatmapRequest request)
    {
        var coldspots = new List<HeatmapColdspot>();
        if (request.CenterLocation != null)
        {
            var gridSize = 0.02; 
            for (double lat = request.CenterLocation.Latitude - 0.1; lat <= request.CenterLocation.Latitude + 0.1; lat += gridSize)
            {
                for (double lng = request.CenterLocation.Longitude - 0.1; lng <= request.CenterLocation.Longitude + 0.1; lng += gridSize)
                {
                    var gridLocation = new Location(lat, lng);
                    var nearbyPlaces = places.Where(p => 
                        _geospatialService.CalculateDistance(gridLocation, p.Location) <= 2).ToList();

                    if (!nearbyPlaces.Any())
                    {
                        coldspots.Add(new HeatmapColdspot
                        {
                            Center = gridLocation,
                            RadiusKm = 2,
                            EstimatedPopulation = EstimatePopulation(gridLocation),
                            AreaName = GetAreaName(gridLocation),
                            OpportunityDescription = "No amala spots in 2km radius",
                            OpportunityScore = CalculateOpportunityScore(gridLocation)
                        });
                    }
                }
            }
        }

        return coldspots.OrderByDescending(c => c.OpportunityScore).Take(5).ToList();
    }

    private async Task<List<BusinessOpportunity>> GenerateBusinessOpportunitiesFromPlaces(
        List<GooglePlaceWithBusyness> places, GooglePlacesHeatmapRequest request)
    {
        var opportunities = new List<BusinessOpportunity>();
        var coldspots = IdentifyColdspots(places, request);
        
        foreach (var coldspot in coldspots.Take(5))
        {
            var opportunity = new BusinessOpportunity
            {
                Location = coldspot.Center,
                AreaName = coldspot.AreaName,
                OpportunityScore = coldspot.OpportunityScore,
                EstimatedDemand = Math.Max(1, coldspot.EstimatedPopulation / 25000),
                CompetitionLevel = CompetitionLevel.VeryLow,
                RecommendedInvestment = CalculateInvestment(coldspot.EstimatedPopulation, UnderservedSeverity.High),
                Reasons = new List<string>
                {
                    coldspot.OpportunityDescription,
                    $"Estimated population: {coldspot.EstimatedPopulation:N0}",
                    "No direct competition in immediate area",
                    "Potential for first-mover advantage"
                },
                RiskFactors = new List<string>
                {
                    "Market acceptance uncertainty",
                    "Need to build customer base from scratch",
                    "Higher marketing costs initially"
                },
                EstimatedROI = "20-35% annually"
            };

            opportunities.Add(opportunity);
        }

        return opportunities;
    }

    private async Task<List<UnderservedArea>> IdentifyUnderservedAreasFromPlaces(
        List<GooglePlaceWithBusyness> places, GooglePlacesHeatmapRequest request)
    {
        var underservedAreas = new List<UnderservedArea>();
        var populationCenters = new[]
        {
            new { Location = new Location(6.5244, 3.3792), Name = "Victoria Island", Population = 500000 },
            new { Location = new Location(6.4541, 3.3947), Name = "Ikoyi", Population = 300000 },
            new { Location = new Location(6.5795, 3.3211), Name = "Surulere", Population = 800000 },
            new { Location = new Location(6.6018, 3.3515), Name = "Ikeja", Population = 600000 },
            new { Location = new Location(6.4698, 3.5852), Name = "Ajah", Population = 400000 }
        };

        foreach (var center in populationCenters)
        {
            var nearbyPlaces = places.Where(p => 
                _geospatialService.CalculateDistance(center.Location, p.Location) <= 3).ToList();

            var spotsPerCapita = nearbyPlaces.Count / (double)center.Population * 100000;

            if (spotsPerCapita < 5) 
            {
                underservedAreas.Add(new UnderservedArea
                {
                    Location = center.Location,
                    AreaName = center.Name,
                    Population = center.Population,
                    CurrentSpotCount = nearbyPlaces.Count,
                    SpotsPerCapita = spotsPerCapita,
                    RecommendedSpots = Math.Max(1, center.Population / 50000),
                    Severity = spotsPerCapita < 1 ? UnderservedSeverity.Critical : 
                              spotsPerCapita < 2 ? UnderservedSeverity.High : UnderservedSeverity.Medium,
                    Reasons = new List<string>
                    {
                        $"Only {spotsPerCapita:F1} amala spots per 100,000 residents",
                        $"Current supply: {nearbyPlaces.Count} spots in 3km radius",
                        $"{center.Name} residents likely travel far for authentic amala"
                    }
                });
            }
        }

        return underservedAreas.OrderByDescending(a => a.Severity).ToList();
    }

    private HeatmapInsights GenerateHeatmapInsights(GooglePlacesHeatmapResponse response)
    {
        var insights = new HeatmapInsights();
        var places = response.PlacesWithBusyness;
        var avgRating = places.Where(p => p.Rating.HasValue).Average(p => (double)p.Rating!.Value);
        var busyPlaces = places.Count(p => p.BusynessInfo.CurrentLevel >= BusynessLevel.Busy);
        
        insights.Summary = $"Analyzed {places.Count} amala spots with average rating of {avgRating:F1}/5. " +
                          $"{busyPlaces} spots are currently busy, indicating strong demand.";
        insights.KeyFindings = new List<string>();
        
        if (response.HeatmapAnalysis.Hotspots.Any())
        {
            insights.KeyFindings.Add($"🔥 {response.HeatmapAnalysis.Hotspots.Count} hotspots identified with high amala density");
        }

        if (response.UnderservedAreas.Any())
        {
            insights.KeyFindings.Add($"🎯 {response.UnderservedAreas.Count} underserved areas found with business potential");
        }

        var packedPlaces = places.Count(p => p.BusynessInfo.CurrentLevel == BusynessLevel.Packed);
        if (packedPlaces > 0)
        {
            insights.KeyFindings.Add($"🚨 {packedPlaces} spots are currently packed - high demand signal");
        }
        insights.Recommendations = new List<string>();
        
        if (busyPlaces > places.Count * 0.6)
        {
            insights.Recommendations.Add("High overall busyness indicates strong market demand");
        }

        if (response.BusinessOpportunities.Any())
        {
            var topOpp = response.BusinessOpportunities.First();
            insights.Recommendations.Add($"Best investment opportunity: {topOpp.AreaName} (Score: {topOpp.OpportunityScore:F0}/100)");
        }
        insights.BusinessOpportunities = response.BusinessOpportunities
            .Take(3)
            .Select(o => $"{o.AreaName}: {o.OpportunityScore:F0}/100 score, {o.EstimatedROI} ROI")
            .ToList();
        insights.Statistics = new Dictionary<string, object>
        {
            { "total_places", places.Count },
            { "average_rating", avgRating },
            { "busy_places", busyPlaces },
            { "hotspots", response.HeatmapAnalysis.Hotspots.Count },
            { "business_opportunities", response.BusinessOpportunities.Count },
            { "underserved_areas", response.UnderservedAreas.Count }
        };

        return insights;
    }

    private int EstimatePopulation(Location location)
    {
        return 50000; 
    }

    private string GetAreaName(Location location)
    {
        return $"Area ({location.Latitude:F3}, {location.Longitude:F3})";
    }

    private double CalculateOpportunityScore(Location location)
    {
        return 60.0; 
    }
}