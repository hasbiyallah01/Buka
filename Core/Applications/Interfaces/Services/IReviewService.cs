using System.Collections.Generic;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AmalaSpotLocator.Models;

namespace AmalaSpotLocator.Interfaces;

public interface IReviewService
{
    Task<Review> CreateReviewAsync(CreateReviewRequest request);
    Task<Review?> GetReviewByIdAsync(Guid reviewId);
    Task<IEnumerable<Review>> GetReviewsBySpotIdAsync(Guid spotId, int page = 1, int pageSize = 20);
    Task<IEnumerable<Review>> GetReviewsByUserIdAsync(Guid userId, int page = 1, int pageSize = 20);
    Task<Review> UpdateReviewAsync(Guid reviewId, UpdateReviewRequest request);
    Task<bool> DeleteReviewAsync(Guid reviewId, Guid userId);
    Task<bool> CanUserReviewSpotAsync(Guid userId, Guid spotId);
    Task<ReviewStatistics> GetSpotReviewStatisticsAsync(Guid spotId);
    Task<bool> ModerateReviewAsync(Guid reviewId, bool isApproved, Guid moderatorId);
    Task<IEnumerable<Review>> GetUnmoderatedReviewsAsync(int page = 1, int pageSize = 20);
    Task<bool> ReportReviewAsync(Guid reviewId, Guid reporterId, string reason);
    Task<bool> IsReviewSpamAsync(string comment);
}

public class CreateReviewRequest
{
    public Guid SpotId { get; set; }
    public Guid UserId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
}

public class UpdateReviewRequest
{
    public int Rating { get; set; }
    public string? Comment { get; set; }
}

public class ReviewStatistics
{
    public decimal AverageRating { get; set; }
    public int TotalReviews { get; set; }
    public Dictionary<int, int> RatingDistribution { get; set; } = new();
}