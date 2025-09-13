using System.Collections.Generic;
using AmalaSpotLocator.Models.SpotModel;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AmalaSpotLocator.Models;

namespace AmalaSpotLocator.Interfaces;

public interface ISpotService
{

    Task<AmalaSpot?> GetByIdAsync(Guid id);
    Task<AmalaSpot> CreateAsync(AmalaSpot spot);
    Task<AmalaSpot> UpdateAsync(AmalaSpot spot);
    Task<bool> DeleteAsync(Guid id);
    Task<IEnumerable<AmalaSpot>> GetAllAsync();

    Task<IEnumerable<AmalaSpot>> GetNearbyAsync(Models.Location location, double radiusKm, int limit = 50);
    Task<IEnumerable<AmalaSpot>> FilterByPriceRangeAsync(PriceRange minPrice, PriceRange maxPrice);
    Task<IEnumerable<AmalaSpot>> FilterByRatingAsync(decimal minRating);
    Task<IEnumerable<AmalaSpot>> FilterByOpenHoursAsync(TimeSpan currentTime);
    Task<IEnumerable<AmalaSpot>> SearchAsync(SpotSearchCriteria criteria);

    Task<bool> ValidateSpotAsync(AmalaSpot spot);
    Task<AmalaSpot> SanitizeSpotDataAsync(AmalaSpot spot);
    Task<bool> ExistsAsync(Guid id);
    Task<bool> IsLocationUniqueAsync(Models.Location location, Guid? excludeSpotId = null);

    Task<int> GetTotalCountAsync();
    Task<decimal> GetAverageRatingAsync();
    Task<IEnumerable<AmalaSpot>> GetTopRatedAsync(int count = 10);
    Task<IEnumerable<AmalaSpot>> GetRecentlyAddedAsync(int count = 10);
}