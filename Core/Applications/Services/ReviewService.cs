using Microsoft.EntityFrameworkCore;
using AmalaSpotLocator.Data;
using AmalaSpotLocator.Interfaces;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System;
using Microsoft.Extensions.Logging;
using AmalaSpotLocator.Models;

namespace AmalaSpotLocator.Core.Applications.Services;

public class ReviewService : IReviewService
{
    private readonly AmalaSpotContext _context;
    private readonly ILogger<ReviewService> _logger;

    private readonly HashSet<string> _spamKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "spam", "fake", "bot", "scam", "click here", "visit now", "buy now",
        "free money", "guaranteed", "limited time", "act now", "urgent"
    };

    public ReviewService(AmalaSpotContext context, ILogger<ReviewService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Review> CreateReviewAsync(CreateReviewRequest request)
    {
        try
        {

            if (!await CanUserReviewSpotAsync(request.UserId, request.SpotId))
            {
                throw new InvalidOperationException("User has already reviewed this spot");
            }

            var isSpam = await IsReviewSpamAsync(request.Comment ?? string.Empty);

            var review = new Review
            {
                SpotId = request.SpotId,
                UserId = request.UserId,
                Rating = request.Rating,
                Comment = request.Comment,
                IsModerated = !isSpam, // Auto-approve non-spam reviews
                IsHidden = isSpam
            };

            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();

            await UpdateSpotRatingAsync(request.SpotId);

            return review;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating review for spot: {SpotId}", request.SpotId);
            throw;
        }
    }

    public async Task<Review?> GetReviewByIdAsync(Guid reviewId)
    {
        try
        {
            return await _context.Reviews
                .Include(r => r.User)
                .Include(r => r.Spot)
                .FirstOrDefaultAsync(r => r.Id == reviewId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving review: {ReviewId}", reviewId);
            return null;
        }
    }

    public async Task<IEnumerable<Review>> GetReviewsBySpotIdAsync(Guid spotId, int page = 1, int pageSize = 20)
    {
        try
        {
            return await _context.Reviews
                .Include(r => r.User)
                .Where(r => r.SpotId == spotId && r.IsModerated && !r.IsHidden)
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving reviews for spot: {SpotId}", spotId);
            return Enumerable.Empty<Review>();
        }
    }

    public async Task<IEnumerable<Review>> GetReviewsByUserIdAsync(Guid userId, int page = 1, int pageSize = 20)
    {
        try
        {
            return await _context.Reviews
                .Include(r => r.Spot)
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving reviews for user: {UserId}", userId);
            return Enumerable.Empty<Review>();
        }
    }

    public async Task<Review> UpdateReviewAsync(Guid reviewId, UpdateReviewRequest request)
    {
        try
        {
            var review = await _context.Reviews.FindAsync(reviewId);
            if (review == null)
                throw new ArgumentException("Review not found");

            var isSpam = await IsReviewSpamAsync(request.Comment ?? string.Empty);

            review.Rating = request.Rating;
            review.Comment = request.Comment;
            review.UpdatedAt = DateTime.UtcNow;
            review.IsModerated = !isSpam; // Re-moderate if spam detected
            review.IsHidden = isSpam;

            await _context.SaveChangesAsync();

            await UpdateSpotRatingAsync(review.SpotId);

            return review;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating review: {ReviewId}", reviewId);
            throw;
        }
    }

    public async Task<bool> DeleteReviewAsync(Guid reviewId, Guid userId)
    {
        try
        {
            var review = await _context.Reviews
                .FirstOrDefaultAsync(r => r.Id == reviewId && r.UserId == userId);

            if (review == null)
                return false;

            var spotId = review.SpotId;
            _context.Reviews.Remove(review);
            await _context.SaveChangesAsync();

            await UpdateSpotRatingAsync(spotId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting review: {ReviewId}", reviewId);
            return false;
        }
    }

    public async Task<bool> CanUserReviewSpotAsync(Guid userId, Guid spotId)
    {
        try
        {

            var existingReview = await _context.Reviews
                .FirstOrDefaultAsync(r => r.UserId == userId && r.SpotId == spotId);

            return existingReview == null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if user can review spot: {UserId}, {SpotId}", userId, spotId);
            return false;
        }
    }

    public async Task<ReviewStatistics> GetSpotReviewStatisticsAsync(Guid spotId)
    {
        try
        {
            var reviews = await _context.Reviews
                .Where(r => r.SpotId == spotId && r.IsModerated && !r.IsHidden)
                .ToListAsync();

            if (!reviews.Any())
            {
                return new ReviewStatistics
                {
                    AverageRating = 0,
                    TotalReviews = 0,
                    RatingDistribution = new Dictionary<int, int>()
                };
            }

            var ratingDistribution = reviews
                .GroupBy(r => r.Rating)
                .ToDictionary(g => g.Key, g => g.Count());

            for (int i = 1; i <= 5; i++)
            {
                if (!ratingDistribution.ContainsKey(i))
                    ratingDistribution[i] = 0;
            }

            return new ReviewStatistics
            {
                AverageRating = (decimal)reviews.Average(r => r.Rating),
                TotalReviews = reviews.Count,
                RatingDistribution = ratingDistribution
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating review statistics for spot: {SpotId}", spotId);
            return new ReviewStatistics();
        }
    }

    public async Task<bool> ModerateReviewAsync(Guid reviewId, bool isApproved, Guid moderatorId)
    {
        try
        {
            var review = await _context.Reviews.FindAsync(reviewId);
            if (review == null)
                return false;

            review.IsModerated = true;
            review.IsHidden = !isApproved;
            review.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            if (isApproved)
            {
                await UpdateSpotRatingAsync(review.SpotId);
            }

            _logger.LogInformation("Review {ReviewId} moderated by {ModeratorId}: {IsApproved}", 
                reviewId, moderatorId, isApproved);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error moderating review: {ReviewId}", reviewId);
            return false;
        }
    }

    public async Task<IEnumerable<Review>> GetUnmoderatedReviewsAsync(int page = 1, int pageSize = 20)
    {
        try
        {
            return await _context.Reviews
                .Include(r => r.User)
                .Include(r => r.Spot)
                .Where(r => !r.IsModerated)
                .OrderBy(r => r.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving unmoderated reviews");
            return Enumerable.Empty<Review>();
        }
    }

    public async Task<bool> ReportReviewAsync(Guid reviewId, Guid reporterId, string reason)
    {
        try
        {

            var review = await _context.Reviews.FindAsync(reviewId);
            if (review == null)
                return false;

            review.IsModerated = false; // Mark for re-moderation
            await _context.SaveChangesAsync();

            _logger.LogWarning("Review {ReviewId} reported by user {ReporterId}: {Reason}", 
                reviewId, reporterId, reason);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reporting review: {ReviewId}", reviewId);
            return false;
        }
    }

    public async Task<bool> IsReviewSpamAsync(string comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
            return false;

        try
        {

            var words = comment.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var spamWordCount = words.Count(word => _spamKeywords.Contains(word));

            var spamRatio = (double)spamWordCount / words.Length;

            var uniqueWords = words.Distinct().Count();
            var repetitionRatio = (double)uniqueWords / words.Length;

            var isSpam = spamRatio > 0.2 || repetitionRatio < 0.3;

            if (isSpam)
            {
                _logger.LogWarning("Potential spam detected in review comment: {Comment}", comment);
            }

            return await Task.FromResult(isSpam);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for spam in comment");
            return false; // Default to not spam if error occurs
        }
    }

    private async Task UpdateSpotRatingAsync(Guid spotId)
    {
        try
        {
            var spot = await _context.AmalaSpots.FindAsync(spotId);
            if (spot == null)
                return;

            var statistics = await GetSpotReviewStatisticsAsync(spotId);
            spot.AverageRating = statistics.AverageRating;
            spot.ReviewCount = statistics.TotalReviews;
            spot.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating spot rating: {SpotId}", spotId);
        }
    }
}