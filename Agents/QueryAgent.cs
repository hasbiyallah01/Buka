using AmalaSpotLocator.Interfaces;
using AmalaSpotLocator.Models.Exceptions;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using AmalaSpotLocator.Models.UserModel;
using AmalaSpotLocator.Models.SpotModel;
using System.Threading.Tasks;
using System;
using AmalaSpotLocator.Models;
using AmalaSpotLocator.Extensions;
using AmalaSpotLocator.Core.Applications.Interfaces.Services;
using AmalaSpotLocator.Core.Applications.Services;

namespace AmalaSpotLocator.Agents;

public class QueryAgent : BaseAgent, IQueryAgent
{
    private readonly ISpotService _spotService;
    private readonly IGeospatialService _geospatialService;
    private readonly IBusynessService _busynessService;
    private readonly IHeatmapService _heatmapService;
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(15);
    
    public QueryAgent(
        ISpotService spotService,
        IGeospatialService geospatialService,
        IBusynessService busynessService,
        IHeatmapService heatmapService,
        IMemoryCache cache,
        ILogger<QueryAgent> logger) : base(logger)
    {
        _spotService = spotService ?? throw new ArgumentNullException(nameof(spotService));
        _geospatialService = geospatialService ?? throw new ArgumentNullException(nameof(geospatialService));
        _busynessService = busynessService ?? throw new ArgumentNullException(nameof(busynessService));
        _heatmapService = heatmapService ?? throw new ArgumentNullException(nameof(heatmapService));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }
    
    public async Task<QueryResult> ExecuteSpotSearch(UserIntent intent)
    {
        return await ExecuteWithErrorHandling(
            async () => await ExecuteSpotSearchInternal(intent),
            nameof(ExecuteSpotSearch),
            ex => new QueryAgentException($"Failed to execute spot search: {ex.Message}", ex));
    }
    
    public async Task<QueryResult> ExecuteSpotDetails(UserIntent intent)
    {
        return await ExecuteWithErrorHandling(
            async () => await ExecuteSpotDetailsInternal(intent),
            nameof(ExecuteSpotDetails),
            ex => new QueryAgentException($"Failed to get spot details: {ex.Message}", ex));
    }
    
    public async Task<QueryResult> ExecuteAddSpot(UserIntent intent)
    {
        return await ExecuteWithErrorHandling(
            async () => await ExecuteAddSpotInternal(intent),
            nameof(ExecuteAddSpot),
            ex => new QueryAgentException($"Failed to add spot: {ex.Message}", ex));
    }
    
    public async Task<QueryResult> ExecuteReviewQuery(UserIntent intent)
    {
        return await ExecuteWithErrorHandling(
            async () => await ExecuteReviewQueryInternal(intent),
            nameof(ExecuteReviewQuery),
            ex => new QueryAgentException($"Failed to execute review query: {ex.Message}", ex));
    }
    
    public async Task<QueryResult> ExecuteGenericQuery(UserIntent intent)
    {
        return await ExecuteWithErrorHandling(
            async () => await ExecuteGenericQueryInternal(intent),
            nameof(ExecuteGenericQuery),
            ex => new QueryAgentException($"Failed to execute generic query: {ex.Message}", ex));
    }
    
    private async Task<QueryResult> ExecuteSpotSearchInternal(UserIntent intent)
    {
        ValidateInput(intent, nameof(intent));
        
        if (intent.TargetLocation == null)
        {
            return new QueryResult
            {
                Success = false,
                ErrorMessage = "Location is required for spot search"
            };
        }
        
        try
        {

            var cacheKey = GenerateSearchCacheKey(intent);

            if (_cache.TryGetValue(cacheKey, out QueryResult? cachedResult) && cachedResult != null)
            {
                Logger.LogInformation("Returning cached search results for key: {CacheKey}", cacheKey);
                cachedResult.Metadata["fromCache"] = true;
                return cachedResult;
            }

            var searchCriteria = BuildSearchCriteria(intent);

            var spots = await _spotService.SearchAsync(searchCriteria);

            if (intent.Preferences.Any())
            {
                spots = FilterSpotsByPreferences(spots, intent.Preferences);
            }

            var orderedSpots = OrderSpotsByRelevance(spots, intent.TargetLocation);
            
            var result = new QueryResult
            {
                Success = true,
                Spots = orderedSpots.ToDtoList(_geospatialService, intent.TargetLocation),
                TotalCount = orderedSpots.Count(),
                ExecutedAt = DateTime.UtcNow
            };

            AddSearchMetadata(result, intent, searchCriteria);

            _cache.Set(cacheKey, result, _cacheExpiration);
            
            Logger.LogInformation("Found {Count} spots for search query, cached with key: {CacheKey}", 
                orderedSpots.Count(), cacheKey);
            return result;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error executing spot search");
            return new QueryResult
            {
                Success = false,
                ErrorMessage = $"Search failed: {ex.Message}"
            };
        }
    }
    
    private async Task<QueryResult> ExecuteSpotDetailsInternal(UserIntent intent)
    {
        ValidateInput(intent, nameof(intent));
        
        try
        {

            if (!intent.Metadata.TryGetValue("spotId", out var spotIdObj) || 
                !Guid.TryParse(spotIdObj?.ToString(), out var spotId))
            {
                Logger.LogWarning("No valid spot ID found in intent metadata");
                return new QueryResult
                {
                    Success = false,
                    ErrorMessage = "Please specify which amala spot you want details for"
                };
            }

            var cacheKey = $"spot_details_{spotId}";
            if (_cache.TryGetValue(cacheKey, out QueryResult? cachedResult) && cachedResult != null)
            {
                Logger.LogInformation("Returning cached spot details for ID: {SpotId}", spotId);
                cachedResult.Metadata["fromCache"] = true;
                return cachedResult;
            }

            var spot = await _spotService.GetByIdAsync(spotId);
            if (spot == null)
            {
                return new QueryResult
                {
                    Success = false,
                    ErrorMessage = "Amala spot not found"
                };
            }

            var reviews = await GetSpotReviews(spotId);
            
            var result = new QueryResult
            {
                Success = true,
                SingleSpot = spot.ToDto(_geospatialService, intent.TargetLocation),
                Reviews = reviews,
                TotalCount = 1,
                ExecutedAt = DateTime.UtcNow
            };

            result.Metadata["spotId"] = spotId;
            result.Metadata["reviewCount"] = reviews.Count;

            _cache.Set(cacheKey, result, _cacheExpiration);
            
            Logger.LogInformation("Retrieved details for spot: {SpotName} (ID: {SpotId})", spot.Name, spotId);
            return result;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting spot details");
            return new QueryResult
            {
                Success = false,
                ErrorMessage = $"Failed to get spot details: {ex.Message}"
            };
        }
    }
    
    private async Task<QueryResult> ExecuteAddSpotInternal(UserIntent intent)
    {
        ValidateInput(intent, nameof(intent));

        Logger.LogInformation("Adding new spot for intent: {Message}", intent.OriginalMessage);
        
        return new QueryResult
        {
            Success = false,
            ErrorMessage = "Add spot functionality not yet implemented - requires entity extraction from NLU"
        };
    }
    
    private async Task<QueryResult> ExecuteReviewQueryInternal(UserIntent intent)
    {
        ValidateInput(intent, nameof(intent));
        
        try
        {

            if (intent.Metadata.TryGetValue("action", out var actionObj) && 
                actionObj?.ToString() == "add_review")
            {
                return await AddReviewFromIntent(intent);
            }

            if (intent.Metadata.TryGetValue("spotId", out var spotIdObj) && 
                Guid.TryParse(spotIdObj?.ToString(), out var spotId))
            {
                return await GetReviewsForSpot(spotId);
            }

            return await GetRecentReviews();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error processing review query");
            return new QueryResult
            {
                Success = false,
                ErrorMessage = $"Failed to process review query: {ex.Message}"
            };
        }
    }
    
    private async Task<QueryResult> ExecuteGenericQueryInternal(UserIntent intent)
    {
        ValidateInput(intent, nameof(intent));
        
        Logger.LogInformation("Processing generic query for intent type: {Type}", intent.Type);

        return intent.Type switch
        {
            IntentType.GetDirections => await HandleDirectionsQuery(intent),
            IntentType.Unknown => await HandleUnknownQuery(intent),
            _ => new QueryResult
            {
                Success = false,
                ErrorMessage = $"Query type {intent.Type} not supported in generic query handler"
            }
        };
    }
    
    private async Task<QueryResult> HandleDirectionsQuery(UserIntent intent)
    {

        Logger.LogInformation("Handling directions query");
        
        return new QueryResult
        {
            Success = false,
            ErrorMessage = "Directions functionality not yet implemented - requires Google Maps integration"
        };
    }
    
    private async Task<QueryResult> HandleUnknownQuery(UserIntent intent)
    {
        Logger.LogWarning("Handling unknown query type for message: {Message}", intent.OriginalMessage);
        
        return new QueryResult
        {
            Success = false,
            ErrorMessage = "I didn't understand your request. Could you please rephrase it?",
            Metadata = { ["originalMessage"] = intent.OriginalMessage }
        };
    }
    
    private IEnumerable<Models.AmalaSpot> FilterSpotsByPreferences(
        IEnumerable<Models.AmalaSpot> spots, 
        List<string> preferences)
    {
        if (!preferences.Any())
            return spots;

        return spots;
    }
    
    private bool IsPriceRelatedTerm(string term)
    {
        var priceTerms = new[] { "cheap", "expensive", "budget", "affordable", "costly", "pricey", "inexpensive" };
        return priceTerms.Contains(term.ToLowerInvariant());
    }

    private PriceRange GetPriceRangeFromBudget(decimal budget)
    {
        return budget switch
        {
            <= 1000 => PriceRange.Budget,
            <= 2500 => PriceRange.Moderate,
            _ => PriceRange.Expensive
        };
    }
    
    #region Query Optimization and Caching
    
    private string GenerateSearchCacheKey(UserIntent intent)
    {
        var keyData = new
        {
            Location = intent.TargetLocation != null ? $"{intent.TargetLocation.Latitude:F4},{intent.TargetLocation.Longitude:F4}" : "null",
            MaxDistance = intent.MaxDistance ?? 5.0,
            MaxBudget = intent.MaxBudget,
            MinRating = intent.MinRating,
            Preferences = string.Join(",", intent.Preferences.OrderBy(p => p))
        };
        
        var keyJson = JsonSerializer.Serialize(keyData);
        return $"spot_search_{keyJson.GetHashCode():X}";
    }
    
    private SpotSearchCriteria BuildSearchCriteria(UserIntent intent)
    {
        var criteria = new SpotSearchCriteria
        {
            Location = intent.TargetLocation,
            RadiusKm = intent.MaxDistance ?? 15.0,
            MinRating = intent.MinRating,
            Limit = 50 
        };

        if (intent.MaxBudget.HasValue)
        {
            criteria.MaxPriceRange = GetPriceRangeFromBudget(intent.MaxBudget.Value);
        }

        if (intent.Preferences.Any())
        {
            var foodPreferences = intent.Preferences
                .Where(p => !IsPriceRelatedTerm(p))
                .ToList();
            
            if (foodPreferences.Any())
            {
                criteria.Specialties = foodPreferences;
            }
        }
        
        return criteria;
    }
    
    private IEnumerable<AmalaSpot> OrderSpotsByRelevance(IEnumerable<AmalaSpot> spots, Location userLocation)
    {
        return spots.OrderByDescending(s => CalculateRelevanceScore(s, userLocation));
    }
    
    private double CalculateRelevanceScore(AmalaSpot spot, Location userLocation)
    {
        double score = 0;

        score += (double)spot.AverageRating * 0.4;

        if (spot.IsVerified)
            score += 1.0 * 0.2;

        var normalizedReviewCount = Math.Min(spot.ReviewCount / 50.0, 1.0);
        score += normalizedReviewCount * 0.2;

        var distance = _geospatialService.CalculateDistanceToSpot(userLocation, spot);
        var normalizedDistance = Math.Max(0, 1.0 - (distance / 15.0)); // Within 10km gets full points
        score += normalizedDistance * 0.2;
        
        return score;
    }
    
    private void AddSearchMetadata(QueryResult result, UserIntent intent, SpotSearchCriteria criteria)
    {
        result.Metadata["searchRadius"] = criteria.RadiusKm;
        result.Metadata["searchLocation"] = intent.TargetLocation;
        result.Metadata["searchCriteria"] = criteria;
        
        if (intent.MaxBudget.HasValue)
            result.Metadata["maxBudget"] = intent.MaxBudget.Value;
        if (intent.MinRating.HasValue)
            result.Metadata["minRating"] = intent.MinRating.Value;
        if (intent.Preferences.Any())
            result.Metadata["preferences"] = intent.Preferences;
    }
    
    #endregion
    
    #region Review Operations
    
    private async Task<List<Review>> GetSpotReviews(Guid spotId)
    {

        Logger.LogInformation("Getting reviews for spot: {SpotId}", spotId);

        return new List<Review>();
    }
    
    private async Task<QueryResult> GetReviewsForSpot(Guid spotId)
    {
        var cacheKey = $"spot_reviews_{spotId}";
        
        if (_cache.TryGetValue(cacheKey, out QueryResult? cachedResult) && cachedResult != null)
        {
            Logger.LogInformation("Returning cached reviews for spot: {SpotId}", spotId);
            cachedResult.Metadata["fromCache"] = true;
            return cachedResult;
        }
        
        var reviews = await GetSpotReviews(spotId);
        var spot = await _spotService.GetByIdAsync(spotId);
        
        var result = new QueryResult
        {
            Success = true,
            Reviews = reviews,
            SingleSpot = spot?.ToDto(_geospatialService),
            TotalCount = reviews.Count,
            ExecutedAt = DateTime.UtcNow
        };
        
        result.Metadata["spotId"] = spotId;
        result.Metadata["reviewCount"] = reviews.Count;
        
        _cache.Set(cacheKey, result, _cacheExpiration);
        
        return result;
    }
    
    private async Task<QueryResult> AddReviewFromIntent(UserIntent intent)
    {

        if (!intent.Metadata.TryGetValue("spotId", out var spotIdObj) ||
            !Guid.TryParse(spotIdObj?.ToString(), out var spotId))
        {
            return new QueryResult
            {
                Success = false,
                ErrorMessage = "Spot ID is required to add a review"
            };
        }
        
        if (!intent.Metadata.TryGetValue("rating", out var ratingObj) ||
            !int.TryParse(ratingObj?.ToString(), out var rating) ||
            rating < 1 || rating > 5)
        {
            return new QueryResult
            {
                Success = false,
                ErrorMessage = "Valid rating (1-5) is required"
            };
        }

        Logger.LogInformation("Adding review for spot {SpotId} with rating {Rating}", spotId, rating);
        
        return new QueryResult
        {
            Success = false,
            ErrorMessage = "Review creation not yet implemented - requires ReviewService"
        };
    }
    
    private async Task<QueryResult> GetRecentReviews()
    {
        var cacheKey = "recent_reviews";
        
        if (_cache.TryGetValue(cacheKey, out QueryResult? cachedResult) && cachedResult != null)
        {
            Logger.LogInformation("Returning cached recent reviews");
            cachedResult.Metadata["fromCache"] = true;
            return cachedResult;
        }

        Logger.LogInformation("Getting recent reviews across all spots");
        
        var result = new QueryResult
        {
            Success = true,
            Reviews = new List<Review>(),
            TotalCount = 0,
            ExecutedAt = DateTime.UtcNow
        };
        
        result.Metadata["queryType"] = "recent_reviews";
        
        _cache.Set(cacheKey, result, TimeSpan.FromMinutes(5)); // Shorter cache for recent reviews
        
        return result;
    }
    
    #endregion
    public async Task<QueryResult> ExecuteBusynessQuery(UserIntent intent)
    {
        return await ExecuteWithErrorHandling(
            async () => await ExecuteBusynessQueryInternal(intent),
            nameof(ExecuteBusynessQuery),
            ex => new QueryAgentException($"Failed to get busyness information: {ex.Message}", ex));
    }
    
    public async Task<QueryResult> ExecuteHeatmapQuery(UserIntent intent)
    {
        return await ExecuteWithErrorHandling(
            async () => await ExecuteHeatmapQueryInternal(intent),
            nameof(ExecuteHeatmapQuery),
            ex => new QueryAgentException($"Failed to generate heatmap: {ex.Message}", ex));
    }

    private async Task<QueryResult> ExecuteBusynessQueryInternal(UserIntent intent)
    {
        try
        {
            Logger.LogInformation("Processing busyness query for intent: {Intent}", intent.Intent);
            if (intent.Parameters.ContainsKey("spotId") && Guid.TryParse(intent.Parameters["spotId"], out var spotId))
            {
                var busyness = await _busynessService.GetCurrentBusynessAsync(spotId);
                
                var response = $"Current busyness at this spot: {busyness.Description}\n" +
                              $"Estimated wait time: {busyness.EstimatedWaitMinutes} minutes\n" +
                              $"Last updated: {busyness.LastUpdated:HH:mm}";

                if (busyness.Recommendations.Any())
                {
                    response += $"\n\nRecommendations:\n• {string.Join("\n• ", busyness.Recommendations)}";
                }

                return new QueryResult
                {
                    Success = true,
                    Message = response,
                    Data = busyness,
                    QueryType = "busyness_check"
                };
            }
            if (intent.Location != null)
            {
                var nearbySpots = await _spotService.GetNearbyAsync(intent.Location, 5);
                var busynessInfo = new List<object>();

                foreach (var spot in nearbySpots.Take(5))
                {
                    try
                    {
                        var busyness = await _busynessService.GetCurrentBusynessAsync(spot.Id);
                        var location = _geospatialService.PointToLocation(spot.Location);
                        
                        busynessInfo.Add(new
                        {
                            SpotName = spot.Name,
                            BusynessLevel = busyness.CurrentLevel.ToString(),
                            Description = busyness.Description,
                            WaitTime = busyness.EstimatedWaitMinutes,
                            Distance = _geospatialService.CalculateDistance(intent.TargetLocation, location)
                        });
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning(ex, "Failed to get busyness for spot {SpotId}", spot.Id);
                    }
                }

                var message = "Here's the current busyness at nearby amala spots:\n\n";
                foreach (dynamic info in busynessInfo)
                {
                    message += $"📍 {info.SpotName} ({info.Distance:F1}km away)\n";
                    message += $"   Status: {info.Description}\n";
                    message += $"   Wait time: ~{info.WaitTime} minutes\n\n";
                }

                return new QueryResult
                {
                    Success = true,
                    Message = message.TrimEnd(),
                    Data = busynessInfo,
                    QueryType = "area_busyness"
                };
            }

            return new QueryResult
            {
                Success = false,
                Message = "Please provide a location or specific spot to check busyness levels.",
                QueryType = "busyness_error"
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error processing busyness query");
            throw;
        }
    }

    private async Task<QueryResult> ExecuteHeatmapQueryInternal(UserIntent intent)
    {
        try
        {
            Logger.LogInformation("Processing heatmap query for intent: {Intent}", intent.Intent);
            if (intent.Intent.ToLowerInvariant().Contains("business") || 
                intent.Intent.ToLowerInvariant().Contains("opportunity") ||
                intent.Intent.ToLowerInvariant().Contains("invest"))
            {
                var opportunities = await _heatmapService.GetBusinessOpportunitiesAsync();
                
                var message = "🚀 Top Business Opportunities for Amala Spots in Lagos:\n\n";
                foreach (var opp in opportunities.Take(5))
                {
                    message += $"📍 {opp.AreaName}\n";
                    message += $"   Opportunity Score: {opp.OpportunityScore:F0}/100\n";
                    message += $"   Investment: {opp.RecommendedInvestment}\n";
                    message += $"   Expected ROI: {opp.EstimatedROI}\n";
                    message += $"   Competition: {opp.CompetitionLevel}\n\n";
                }

                return new QueryResult
                {
                    Success = true,
                    Message = message.TrimEnd(),
                    Data = opportunities,
                    QueryType = "business_opportunities"
                };
            }
            if (intent.Intent.ToLowerInvariant().Contains("underserved") || 
                intent.Intent.ToLowerInvariant().Contains("no amala") ||
                intent.Intent.ToLowerInvariant().Contains("few spots"))
            {
                var underserved = await _heatmapService.IdentifyUnderservedAreasAsync();
                
                var message = "🎯 Underserved Areas in Lagos (Great for New Amala Spots):\n\n";
                foreach (var area in underserved.Take(5))
                {
                    message += $"📍 {area.AreaName}\n";
                    message += $"   Population: {area.Population:N0}\n";
                    message += $"   Current spots: {area.CurrentSpotCount}\n";
                    message += $"   Spots per 100k people: {area.SpotsPerCapita:F1}\n";
                    message += $"   Severity: {area.Severity}\n\n";
                }

                return new QueryResult
                {
                    Success = true,
                    Message = message.TrimEnd(),
                    Data = underserved,
                    QueryType = "underserved_areas"
                };
            }
            var heatmap = await _heatmapService.GenerateLagosAmalaHeatmapAsync();
            
            var hotspots = heatmap.Points
                .Where(p => p.Category >= HeatmapCategory.High)
                .OrderByDescending(p => p.Intensity)
                .Take(5)
                .ToList();

            var response = $"🗺️ Lagos Amala Heatmap Analysis:\n\n";
            response += $"📊 Total amala spots: {heatmap.TotalSpots}\n";
            response += $"📈 Average density: {heatmap.AverageIntensity:F1}/100\n";
            response += $"🔥 Hotspots found: {hotspots.Count}\n";
            response += $"🎯 Underserved areas: {heatmap.UnderservedAreas.Count}\n\n";

            if (hotspots.Any())
            {
                response += "🔥 Top Amala Hotspots:\n";
                foreach (var hotspot in hotspots)
                {
                    response += $"• {hotspot.SpotCount} spots in {hotspot.Radius}km radius\n";
                    response += $"  Intensity: {hotspot.Intensity:F0}/100\n";
                }
                response += "\n";
            }

            response += $"💡 {heatmap.Summary}";

            return new QueryResult
            {
                Success = true,
                Message = response,
                Data = heatmap,
                QueryType = "heatmap_analysis"
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error processing heatmap query");
            throw;
        }
    }
}