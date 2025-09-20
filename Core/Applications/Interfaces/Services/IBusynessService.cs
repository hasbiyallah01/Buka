using AmalaSpotLocator.Models;

namespace AmalaSpotLocator.Core.Applications.Interfaces.Services;

public interface IBusynessService
{
    Task<BusynessInfo> GetCurrentBusynessAsync(Guid spotId, string? placeId = null);
    Task<BusynessInfo> GetBusynessFromGooglePlaceAsync(GooglePlaceBusynessRequest request);
    Task<QuickBusynessResponse> GetQuickBusynessAsync(QuickBusynessRequest request);
    Task<BusynessInfo> SubmitCheckInAsync(CheckInRequest checkIn);
    Task<List<HourlyBusyness>> GetWeeklyPatternAsync(string placeId);
    Task<List<string>> GetAlternativeRecommendationsAsync(Guid spotId, BusynessLevel currentLevel);
    Task<List<string>> GetAlternativeRecommendationsForPlaceAsync(string placeId, Location location, BusynessLevel currentLevel);
    Task<SimpleBusynessResponse> GetSimpleBusynessAsync(SimpleBusynessRequest request);
    Task<BatchBusynessResponse> GetBatchBusynessAsync(BatchBusynessRequest request);
    BusynessLevel CalculateBusynessFromPopularity(int popularityScore);
    string GetBusynessDescription(BusynessLevel level);
}
