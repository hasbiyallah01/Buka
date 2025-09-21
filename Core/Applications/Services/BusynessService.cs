using AmalaSpotLocator.Models;
using AmalaSpotLocator.Interfaces;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Text.Json;
using AmalaSpotLocator.Core.Applications.Interfaces.Services;

namespace AmalaSpotLocator.Core.Applications.Services;

public class BusynessService : IBusynessService
{
    private readonly IGoogleMapsService _googleMapsService;
    private readonly ILogger<BusynessService> _logger;
    private readonly Dictionary<Guid, List<CheckInResponse>> _checkIns = new();
    private readonly Dictionary<string, List<HourlyBusyness>> _cachedPatterns = new();

    public BusynessService(
        IGoogleMapsService googleMapsService,
        ILogger<BusynessService> logger)
    {
        _googleMapsService = googleMapsService;
        _logger = logger;
    }

    public async Task<BusynessInfo> GetCurrentBusynessAsync(Guid spotId, string? placeId = null)
    {
        try
        {
            var busynessInfo = new BusynessInfo
            {
                LastUpdated = DateTime.UtcNow,
                Source = BusynessSource.Estimated
            };
            var recentCheckIns = GetRecentCheckIns(spotId, TimeSpan.FromHours(2));
            busynessInfo.CheckInCount = recentCheckIns.Count;
            int? googlePopularity = null;
            if (!string.IsNullOrEmpty(placeId))
            {
                googlePopularity = await GetCurrentPopularityFromGoogle(placeId);
                if (googlePopularity.HasValue)
                {
                    busynessInfo.PopularityScore = googlePopularity.Value;
                    busynessInfo.Source = recentCheckIns.Any() ? BusynessSource.Hybrid : BusynessSource.GooglePlaces;
                }
            }
            busynessInfo.CurrentLevel = CalculateCurrentBusyness(recentCheckIns, googlePopularity);
            busynessInfo.Description = GetBusynessDescription(busynessInfo.CurrentLevel);
            busynessInfo.EstimatedWaitMinutes = CalculateWaitTime(busynessInfo.CurrentLevel, recentCheckIns);
            if (!string.IsNullOrEmpty(placeId))
            {
                busynessInfo.WeeklyPattern = await GetWeeklyPatternAsync(placeId);
            }
            if (busynessInfo.CurrentLevel >= BusynessLevel.Busy)
            {
                busynessInfo.Recommendations = await GetAlternativeRecommendationsAsync(spotId, busynessInfo.CurrentLevel);
            }

            return busynessInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting busyness info for spot {SpotId}", spotId);
            return new BusynessInfo
            {
                CurrentLevel = BusynessLevel.Moderate,
                Description = "Unable to determine current busyness",
                LastUpdated = DateTime.UtcNow,
                Source = BusynessSource.Estimated
            };
        }
    }

    public async Task<BusynessInfo> SubmitCheckInAsync(CheckInRequest checkIn)
    {
        try
        {
            var checkInResponse = new CheckInResponse
            {
                Id = Guid.NewGuid(),
                SpotId = checkIn.SpotId,
                ReportedLevel = checkIn.ReportedLevel,
                EstimatedWaitMinutes = checkIn.EstimatedWaitMinutes,
                Notes = checkIn.Notes,
                Timestamp = checkIn.Timestamp,
                IsVerified = true 
            };

            if (!_checkIns.ContainsKey(checkIn.SpotId))
            {
                _checkIns[checkIn.SpotId] = new List<CheckInResponse>();
            }
            _checkIns[checkIn.SpotId].Add(checkInResponse);

            _logger.LogInformation("Check-in submitted for spot {SpotId}: {Level}", 
                checkIn.SpotId, checkIn.ReportedLevel);
            return await GetCurrentBusynessAsync(checkIn.SpotId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting check-in for spot {SpotId}", checkIn.SpotId);
            throw;
        }
    }

    public async Task<List<HourlyBusyness>> GetWeeklyPatternAsync(string placeId)
    {
        try
        {
            if (_cachedPatterns.ContainsKey(placeId))
            {
                return _cachedPatterns[placeId];
            }
            var pattern = GenerateRealisticWeeklyPattern();
            _cachedPatterns[placeId] = pattern;

            return pattern;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting weekly pattern for place {PlaceId}", placeId);
            return new List<HourlyBusyness>();
        }
    }

    public async Task<List<string>> GetAlternativeRecommendationsAsync(Guid spotId, BusynessLevel currentLevel)
    {
        var recommendations = new List<string>();

        try
        {
            switch (currentLevel)
            {
                case BusynessLevel.Busy:
                    recommendations.Add("Consider visiting in 30-45 minutes when it's typically quieter");
                    recommendations.Add("Try Mama Sade's place nearby - usually shorter wait");
                    break;
                case BusynessLevel.VeryBusy:
                    recommendations.Add("Long wait expected - maybe try Buka Mama nearby");
                    recommendations.Add("Peak time! Consider ordering takeaway instead");
                    break;
                case BusynessLevel.Packed:
                    recommendations.Add("Mama Put is packed right now — try Mama Sade's place nearby (shorter wait time)");
                    recommendations.Add("Consider visiting after 3 PM when crowd typically reduces");
                    recommendations.Add("Order ahead if possible to skip the queue");
                    break;
            }

            return recommendations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recommendations for spot {SpotId}", spotId);
            return new List<string>();
        }
    }

    public BusynessLevel CalculateBusynessFromPopularity(int popularityScore)
    {
        return popularityScore switch
        {
            <= 20 => BusynessLevel.VeryQuiet,
            <= 40 => BusynessLevel.Quiet,
            <= 60 => BusynessLevel.Moderate,
            <= 80 => BusynessLevel.Busy,
            <= 95 => BusynessLevel.VeryBusy,
            _ => BusynessLevel.Packed
        };
    }

    public string GetBusynessDescription(BusynessLevel level)
    {
        var field = level.GetType().GetField(level.ToString());
        var attribute = field?.GetCustomAttributes(typeof(DescriptionAttribute), false)
            .FirstOrDefault() as DescriptionAttribute;
        return attribute?.Description ?? level.ToString();
    }

    private List<CheckInResponse> GetRecentCheckIns(Guid spotId, TimeSpan timeWindow)
    {
        if (!_checkIns.ContainsKey(spotId))
            return new List<CheckInResponse>();

        var cutoff = DateTime.UtcNow.Subtract(timeWindow);
        return _checkIns[spotId]
            .Where(c => c.Timestamp >= cutoff)
            .OrderByDescending(c => c.Timestamp)
            .ToList();
    }

    private async Task<int?> GetCurrentPopularityFromGoogle(string placeId)
    {
        try
        {
            var now = DateTime.Now;
            var hour = now.Hour;
            if (hour >= 12 && hour <= 14)
                return 85; 
            else if (hour >= 18 && hour <= 20)
                return 90; 
            else if (hour >= 11 && hour <= 15)
                return 60; 
            else if (hour >= 17 && hour <= 21)
                return 70; 
            else
                return 25; 

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Google popularity for place {PlaceId}", placeId);
            return null;
        }
    }

    private BusynessLevel CalculateCurrentBusyness(List<CheckInResponse> recentCheckIns, int? googlePopularity)
    {
        if (recentCheckIns.Any())
        {
            var avgLevel = recentCheckIns.Average(c => (int)c.ReportedLevel);
            var crowdLevel = (BusynessLevel)Math.Round(avgLevel);

            if (googlePopularity.HasValue)
            {
                var googleLevel = CalculateBusynessFromPopularity(googlePopularity.Value);
                var weighted = (int)crowdLevel * 0.7 + (int)googleLevel * 0.3;
                return (BusynessLevel)Math.Round(weighted);
            }

            return crowdLevel;
        }
        else if (googlePopularity.HasValue)
        {
            return CalculateBusynessFromPopularity(googlePopularity.Value);
        }
        return BusynessLevel.Moderate;
    }

    private int CalculateWaitTime(BusynessLevel level, List<CheckInResponse> recentCheckIns)
    {
        var reportedWaits = recentCheckIns
            .Where(c => c.EstimatedWaitMinutes.HasValue)
            .Select(c => c.EstimatedWaitMinutes!.Value)
            .ToList();

        if (reportedWaits.Any())
        {
            return (int)reportedWaits.Average();
        }
        return level switch
        {
            BusynessLevel.VeryQuiet => 0,
            BusynessLevel.Quiet => 2,
            BusynessLevel.Moderate => 8,
            BusynessLevel.Busy => 15,
            BusynessLevel.VeryBusy => 25,
            BusynessLevel.Packed => 40,
            _ => 10
        };
    }

    private List<HourlyBusyness> GenerateRealisticWeeklyPattern()
    {
        var pattern = new List<HourlyBusyness>();

        for (int day = 0; day < 7; day++)
        {
            for (int hour = 6; hour < 22; hour++) 
            {
                var dayOfWeek = (DayOfWeek)day;
                var busynessPercentage = CalculateHourlyBusyness(dayOfWeek, hour);
                
                pattern.Add(new HourlyBusyness
                {
                    DayOfWeek = dayOfWeek,
                    Hour = hour,
                    BusynessPercentage = busynessPercentage,
                    Level = CalculateBusynessFromPopularity(busynessPercentage)
                });
            }
        }

        return pattern;
    }

    private int CalculateHourlyBusyness(DayOfWeek day, int hour)
    {
        int busyness = 20;
        if (day == DayOfWeek.Saturday || day == DayOfWeek.Sunday)
            busyness += 15;
        if (hour >= 12 && hour <= 14) 
            busyness += 50;
        else if (hour >= 18 && hour <= 20) 
            busyness += 60;
        else if (hour >= 11 && hour <= 15) 
            busyness += 25;
        else if (hour >= 17 && hour <= 21) 
            busyness += 35;
        if (day == DayOfWeek.Friday && hour >= 17)
            busyness += 20;

        return Math.Min(100, Math.Max(0, busyness));
    }

    public async Task<BusynessInfo> GetBusynessFromGooglePlaceAsync(GooglePlaceBusynessRequest request)
    {
        try
        {
            _logger.LogInformation("Getting busyness for Google Place: {PlaceName} ({PlaceId})", 
                request.Name, request.PlaceId);

            var busynessInfo = new BusynessInfo
            {
                LastUpdated = DateTime.UtcNow,
                Source = BusynessSource.GooglePlaces
            };
            var googlePopularity = await GetCurrentPopularityFromGoogle(request.PlaceId);
            if (googlePopularity.HasValue)
            {
                busynessInfo.PopularityScore = googlePopularity.Value;
            }
            busynessInfo.CurrentLevel = CalculateBusynessFromGooglePlace(request, googlePopularity);
            busynessInfo.Description = GetBusynessDescription(busynessInfo.CurrentLevel);
            busynessInfo.EstimatedWaitMinutes = CalculateWaitTimeFromGooglePlace(request, busynessInfo.CurrentLevel);
            busynessInfo.WeeklyPattern = await GetWeeklyPatternAsync(request.PlaceId);
            if (busynessInfo.CurrentLevel >= BusynessLevel.Busy)
            {
                busynessInfo.Recommendations = await GetAlternativeRecommendationsForPlaceAsync(
                    request.PlaceId, request.Location, busynessInfo.CurrentLevel);
            }

            return busynessInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting busyness for Google Place {PlaceId}", request.PlaceId);
            return new BusynessInfo
            {
                CurrentLevel = BusynessLevel.Moderate,
                Description = "Unable to determine current busyness",
                LastUpdated = DateTime.UtcNow,
                Source = BusynessSource.Estimated
            };
        }
    }

    public async Task<QuickBusynessResponse> GetQuickBusynessAsync(QuickBusynessRequest request)
    {
        try
        {
            var googlePopularity = await GetCurrentPopularityFromGoogle(request.PlaceId);
            var busynessLevel = googlePopularity.HasValue 
                ? CalculateBusynessFromPopularity(googlePopularity.Value)
                : EstimateBusynessFromRating(request.Rating, request.UserRatingsTotal, request.IsOpenNow);

            var waitTime = CalculateWaitTimeFromLevel(busynessLevel);
            var recommendations = busynessLevel >= BusynessLevel.Busy 
                ? await GetAlternativeRecommendationsForPlaceAsync(request.PlaceId, request.Location, busynessLevel)
                : new List<string>();

            return new QuickBusynessResponse
            {
                PlaceId = request.PlaceId,
                Name = request.Name,
                CurrentLevel = busynessLevel,
                Description = GetBusynessDescription(busynessLevel),
                EstimatedWaitMinutes = waitTime,
                Recommendations = recommendations,
                QuickMessage = GenerateQuickMessage(request.Name, busynessLevel, waitTime),
                LastUpdated = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting quick busyness for place {PlaceId}", request.PlaceId);
            return new QuickBusynessResponse
            {
                PlaceId = request.PlaceId,
                Name = request.Name,
                CurrentLevel = BusynessLevel.Moderate,
                Description = "Unable to determine busyness",
                QuickMessage = $"{request.Name} - busyness unknown",
                LastUpdated = DateTime.UtcNow
            };
        }
    }

    public async Task<List<string>> GetAlternativeRecommendationsForPlaceAsync(string placeId, Location location, BusynessLevel currentLevel)
    {
        var recommendations = new List<string>();

        try
        {
            switch (currentLevel)
            {
                case BusynessLevel.Busy:
                    recommendations.Add("Consider visiting in 30-45 minutes when it's typically quieter");
                    recommendations.Add("Try nearby alternatives with shorter wait times");
                    break;
                case BusynessLevel.VeryBusy:
                    recommendations.Add("Long wait expected - consider nearby options");
                    recommendations.Add("Peak time! Consider ordering takeaway instead");
                    break;
                case BusynessLevel.Packed:
                    recommendations.Add("This spot is packed right now — try nearby alternatives");
                    recommendations.Add("Consider visiting after peak hours");
                    recommendations.Add("Order ahead if possible to skip the queue");
                    break;
            }
            if (IsInBusinessDistrict(location))
            {
                recommendations.Add("Business district - try spots in residential areas nearby");
            }

            return recommendations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recommendations for place {PlaceId}", placeId);
            return new List<string> { "Consider trying other nearby amala spots" };
        }
    }

    private BusynessLevel CalculateBusynessFromGooglePlace(GooglePlaceBusynessRequest request, int? googlePopularity)
    {
        var baseLevel = googlePopularity.HasValue 
            ? CalculateBusynessFromPopularity(googlePopularity.Value)
            : BusynessLevel.Moderate;
        var adjustment = 0;
        if (request.Rating >= 4.5m) adjustment += 1;
        else if (request.Rating <= 3.0m) adjustment -= 1;
        if (request.UserRatingsTotal >= 500) adjustment += 1;
        else if (request.UserRatingsTotal <= 50) adjustment -= 1;
        if (request.IsOpenNow == true) adjustment += 1;
        if (request.PriceLevel == PriceRange.Budget) adjustment += 1; 
        else if (request.PriceLevel == PriceRange.VeryExpensive) adjustment -= 1;
        var finalLevel = (int)baseLevel + adjustment;
        return (BusynessLevel)Math.Max(1, Math.Min(6, finalLevel));
    }

    private BusynessLevel EstimateBusynessFromRating(decimal? rating, int? reviewCount, bool? isOpen)
    {
        var score = 50; 

        if (rating.HasValue)
        {
            if (rating >= 4.5m) score += 20;
            else if (rating >= 4.0m) score += 10;
            else if (rating <= 3.0m) score -= 10;
        }

        if (reviewCount.HasValue)
        {
            if (reviewCount >= 500) score += 15;
            else if (reviewCount >= 100) score += 5;
            else if (reviewCount <= 20) score -= 10;
        }

        if (isOpen == true) score += 10;
        else if (isOpen == false) score -= 20;
        var hour = DateTime.Now.Hour;
        if (hour >= 12 && hour <= 14) score += 20; 
        else if (hour >= 18 && hour <= 20) score += 25; 

        return CalculateBusynessFromPopularity(Math.Max(0, Math.Min(100, score)));
    }

    private int CalculateWaitTimeFromGooglePlace(GooglePlaceBusynessRequest request, BusynessLevel level)
    {
        var baseWaitTime = CalculateWaitTimeFromLevel(level);
        if (request.UserRatingsTotal >= 1000) baseWaitTime += 5; 
        if (request.Rating >= 4.5m) baseWaitTime += 3; 
        if (request.PriceLevel == PriceRange.Budget) baseWaitTime += 2; 

        return Math.Max(0, baseWaitTime);
    }

    private int CalculateWaitTimeFromLevel(BusynessLevel level)
    {
        return level switch
        {
            BusynessLevel.VeryQuiet => 0,
            BusynessLevel.Quiet => 2,
            BusynessLevel.Moderate => 8,
            BusynessLevel.Busy => 15,
            BusynessLevel.VeryBusy => 25,
            BusynessLevel.Packed => 40,
            _ => 10
        };
    }

    private string GenerateQuickMessage(string placeName, BusynessLevel level, int waitTime)
    {
        return level switch
        {
            BusynessLevel.VeryQuiet => $"✅ {placeName} is very quiet - perfect time to visit!",
            BusynessLevel.Quiet => $"😊 {placeName} has minimal wait ({waitTime} min)",
            BusynessLevel.Moderate => $"⏰ {placeName} is moderately busy (~{waitTime} min wait)",
            BusynessLevel.Busy => $"🕐 {placeName} is busy - {waitTime} min wait expected",
            BusynessLevel.VeryBusy => $"⚠️ {placeName} is very busy - {waitTime} min wait",
            BusynessLevel.Packed => $"🚨 {placeName} is packed! {waitTime}+ min wait - try alternatives",
            _ => $"ℹ️ {placeName} - wait time unknown"
        };
    }

    private bool IsInBusinessDistrict(Location location)
    {
        var businessAreas = new[]
        {
            new { Name = "Victoria Island", Lat = 6.4281, Lng = 3.4219, Radius = 0.02 },
            new { Name = "Ikoyi", Lat = 6.4541, Lng = 3.4316, Radius = 0.015 },
            new { Name = "Ikeja GRA", Lat = 6.6018, Lng = 3.3515, Radius = 0.01 }
        };

        return businessAreas.Any(area => 
            Math.Abs(location.Latitude - area.Lat) < area.Radius &&
            Math.Abs(location.Longitude - area.Lng) < area.Radius);
    }

    public async Task<SimpleBusynessResponse> GetSimpleBusynessAsync(SimpleBusynessRequest request)
    {
        try
        {
            _logger.LogInformation("Getting simple busyness for: {Name}", request.Name);
            var location = new Location(request.Latitude, request.Longitude);
            var busynessLevel = await CalculateSimpleBusyness(request, location);
            var waitMinutes = CalculateWaitTimeFromLevel(busynessLevel);
            var message = GenerateSimpleMessage(request.Name, busynessLevel, waitMinutes);
            var tips = GenerateSimpleTips(busynessLevel);

            return new SimpleBusynessResponse
            {
                Name = request.Name,
                BusynessLevel = GetSimpleBusynessName(busynessLevel),
                WaitMinutes = waitMinutes,
                Message = message,
                Tips = tips
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting simple busyness for {Name}", request.Name);
            return new SimpleBusynessResponse
            {
                Name = request.Name,
                BusynessLevel = "Unknown",
                WaitMinutes = 10,
                Message = $"ℹ️ {request.Name} - busyness unknown",
                Tips = new List<string> { "Try calling ahead to check wait times" }
            };
        }
    }

    public async Task<BatchBusynessResponse> GetBatchBusynessAsync(BatchBusynessRequest request)
    {
        try
        {
            _logger.LogInformation("Getting batch busyness for {Count} places", request.Places.Count);

            var places = new List<SimpleBusynessResponse>();
            foreach (var place in request.Places)
            {
                try
                {
                    var busyness = await GetSimpleBusynessAsync(place);
                    places.Add(busyness);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get busyness for {Name}", place.Name);
                    places.Add(new SimpleBusynessResponse
                    {
                        Name = place.Name,
                        BusynessLevel = "Unknown",
                        WaitMinutes = 10,
                        Message = $"ℹ️ {place.Name} - busyness unknown"
                    });
                }
            }

            var response = new BatchBusynessResponse
            {
                Places = places
            };
            if (request.IncludeHeatmap && places.Count > 1)
            {
                response.HeatmapSummary = GenerateSimpleHeatmapSummary(places, request.Places);
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting batch busyness");
            throw;
        }
    }

    private async Task<BusynessLevel> CalculateSimpleBusyness(SimpleBusynessRequest request, Location location)
    {
        var baseScore = GetTimeBasedBusynessScore();
        if (request.Rating.HasValue)
        {
            if (request.Rating >= 4.5m) baseScore += 15; 
            else if (request.Rating >= 4.0m) baseScore += 10;
            else if (request.Rating <= 3.0m) baseScore -= 10;
        }
        if (request.IsOpen == true) baseScore += 10;
        else if (request.IsOpen == false) baseScore -= 20;
        if (IsInBusinessDistrict(location)) baseScore += 15;
        if (!string.IsNullOrEmpty(request.PlaceId))
        {
            try
            {
                var googlePopularity = await GetCurrentPopularityFromGoogle(request.PlaceId);
                if (googlePopularity.HasValue)
                {
                    baseScore = (int)(googlePopularity.Value * 0.7 + baseScore * 0.3);
                }
            }
            catch
            {
            }
        }

        return CalculateBusynessFromPopularity(Math.Max(0, Math.Min(100, baseScore)));
    }

    private int GetTimeBasedBusynessScore()
    {
        var now = DateTime.Now;
        var hour = now.Hour;
        var dayOfWeek = now.DayOfWeek;

        var score = 30; 
        if (hour >= 12 && hour <= 14) score += 40; 
        else if (hour >= 18 && hour <= 20) score += 45; 
        else if (hour >= 11 && hour <= 15) score += 20; 
        else if (hour >= 17 && hour <= 21) score += 25; 
        if (dayOfWeek == DayOfWeek.Saturday || dayOfWeek == DayOfWeek.Sunday)
            score += 15;
        if (dayOfWeek == DayOfWeek.Friday && hour >= 17)
            score += 20;

        return score;
    }

    private string GetSimpleBusynessName(BusynessLevel level)
    {
        return level switch
        {
            BusynessLevel.VeryQuiet => "Very Quiet",
            BusynessLevel.Quiet => "Quiet",
            BusynessLevel.Moderate => "Moderate",
            BusynessLevel.Busy => "Busy",
            BusynessLevel.VeryBusy => "Very Busy",
            BusynessLevel.Packed => "Packed",
            _ => "Unknown"
        };
    }

    private string GenerateSimpleMessage(string name, BusynessLevel level, int waitMinutes)
    {
        return level switch
        {
            BusynessLevel.VeryQuiet => $"✅ {name} is very quiet - perfect time to visit!",
            BusynessLevel.Quiet => $"😊 {name} is quiet - good time to go",
            BusynessLevel.Moderate => $"⏰ {name} is moderately busy (~{waitMinutes} min)",
            BusynessLevel.Busy => $"🕐 {name} is busy - {waitMinutes} min wait",
            BusynessLevel.VeryBusy => $"⚠️ {name} is very busy - {waitMinutes} min wait",
            BusynessLevel.Packed => $"🚨 {name} is packed! {waitMinutes}+ min wait",
            _ => $"ℹ️ {name} - busyness unknown"
        };
    }

    private List<string> GenerateSimpleTips(BusynessLevel level)
    {
        return level switch
        {
            BusynessLevel.VeryQuiet or BusynessLevel.Quiet => new List<string>
            {
                "Great time to visit - no wait expected"
            },
            BusynessLevel.Moderate => new List<string>
            {
                "Short wait expected",
                "Consider calling ahead"
            },
            BusynessLevel.Busy => new List<string>
            {
                "Try visiting in 30-45 minutes",
                "Look for nearby alternatives"
            },
            BusynessLevel.VeryBusy => new List<string>
            {
                "Long wait expected",
                "Consider takeaway/delivery",
                "Try nearby spots"
            },
            BusynessLevel.Packed => new List<string>
            {
                "Very crowded - try alternatives",
                "Visit after peak hours",
                "Order ahead if possible"
            },
            _ => new List<string>
            {
                "Call ahead to check wait times"
            }
        };
    }

    private SimpleHeatmapSummary GenerateSimpleHeatmapSummary(List<SimpleBusynessResponse> places, List<SimpleBusynessRequest> originalPlaces)
    {
        var busyPlaces = places.Count(p => p.BusynessLevel == "Busy" || p.BusynessLevel == "Very Busy" || p.BusynessLevel == "Packed");
        var totalPlaces = places.Count;

        var summary = busyPlaces switch
        {
            0 => $"All {totalPlaces} spots are quiet right now - great time to visit!",
            var n when n == totalPlaces => $"All {totalPlaces} spots are busy - high demand area",
            _ => $"{busyPlaces} of {totalPlaces} spots are busy right now"
        };
        var hotspots = new List<string>();
        var opportunities = new List<string>();
        var locationGroups = originalPlaces
            .GroupBy(p => new { 
                LatGroup = Math.Round(p.Latitude, 2), 
                LngGroup = Math.Round(p.Longitude, 2) 
            })
            .Where(g => g.Count() > 1)
            .ToList();

        foreach (var group in locationGroups)
        {
            var areaName = GetSimpleAreaName(group.Key.LatGroup, group.Key.LngGroup);
            hotspots.Add($"{areaName} has {group.Count()} amala spots");
        }
        if (totalPlaces < 5)
        {
            opportunities.Add("More amala spots needed in this area");
        }

        return new SimpleHeatmapSummary
        {
            TotalPlaces = totalPlaces,
            BusyPlaces = busyPlaces,
            Summary = summary,
            Hotspots = hotspots,
            Opportunities = opportunities
        };
    }

    private string GetSimpleAreaName(double lat, double lng)
    {
        if (lat >= 6.52 && lat <= 6.54 && lng >= 3.37 && lng <= 3.39)
            return "Victoria Island area";
        else if (lat >= 6.59 && lat <= 6.61 && lng >= 3.34 && lng <= 3.36)
            return "Ikeja area";
        else if (lat >= 6.57 && lat <= 6.59 && lng >= 3.31 && lng <= 3.33)
            return "Surulere area";
        else if (lat >= 6.45 && lat <= 6.47 && lng >= 3.39 && lng <= 3.41)
            return "Ikoyi area";
        else
            return $"Area ({lat:F2}, {lng:F2})";
    }
}