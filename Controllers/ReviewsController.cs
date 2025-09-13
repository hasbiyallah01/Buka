using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AmalaSpotLocator.Interfaces;
using System.ComponentModel.DataAnnotations;

namespace AmalaSpotLocator.Controllers;

[ApiController]
[Route("api/reviews")]
public class ReviewsController : ControllerBase
{
    private readonly IReviewService _reviewService;
    private readonly ILogger<ReviewsController> _logger;

    public ReviewsController(IReviewService reviewService, ILogger<ReviewsController> logger)
    {
        _reviewService = reviewService;
        _logger = logger;
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateReview([FromBody] CreateReviewDto request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized();

            var createRequest = new CreateReviewRequest
            {
                SpotId = request.SpotId,
                UserId = userId,
                Rating = request.Rating,
                Comment = request.Comment
            };

            var review = await _reviewService.CreateReviewAsync(createRequest);

            return CreatedAtAction(nameof(GetReview), new { id = review.Id }, new
            {
                id = review.Id,
                spotId = review.SpotId,
                userId = review.UserId,
                rating = review.Rating,
                comment = review.Comment,
                createdAt = review.CreatedAt,
                isModerated = review.IsModerated
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating review");
            return StatusCode(500, new { message = "Failed to create review" });
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetReview(Guid id)
    {
        try
        {
            var review = await _reviewService.GetReviewByIdAsync(id);
            if (review == null)
                return NotFound();

            return Ok(new
            {
                id = review.Id,
                spotId = review.SpotId,
                userId = review.UserId,
                rating = review.Rating,
                comment = review.Comment,
                createdAt = review.CreatedAt,
                updatedAt = review.UpdatedAt,
                user = new
                {
                    id = review.User.Id,
                    firstName = review.User.FirstName,
                    lastName = review.User.LastName
                },
                spot = new
                {
                    id = review.Spot.Id,
                    name = review.Spot.Name
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving review: {ReviewId}", id);
            return StatusCode(500, new { message = "Failed to retrieve review" });
        }
    }

    [HttpGet("spot/{spotId}")]
    public async Task<IActionResult> GetSpotReviews(Guid spotId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            var reviews = await _reviewService.GetReviewsBySpotIdAsync(spotId, page, pageSize);

            return Ok(reviews.Select(r => new
            {
                id = r.Id,
                rating = r.Rating,
                comment = r.Comment,
                createdAt = r.CreatedAt,
                user = new
                {
                    id = r.User.Id,
                    firstName = r.User.FirstName,
                    lastName = r.User.LastName
                }
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving reviews for spot: {SpotId}", spotId);
            return StatusCode(500, new { message = "Failed to retrieve reviews" });
        }
    }

    [HttpGet("user/{userId}")]
    [Authorize]
    public async Task<IActionResult> GetUserReviews(Guid userId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        try
        {

            var currentUserIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            var currentUserRoleClaim = User.FindFirst(System.Security.Claims.ClaimTypes.Role);
            
            if (currentUserIdClaim == null || !Guid.TryParse(currentUserIdClaim.Value, out var currentUserId))
                return Unauthorized();

            var isAdminOrModerator = currentUserRoleClaim?.Value is "Admin" or "Moderator";
            
            if (userId != currentUserId && !isAdminOrModerator)
                return Forbid();

            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            var reviews = await _reviewService.GetReviewsByUserIdAsync(userId, page, pageSize);

            return Ok(reviews.Select(r => new
            {
                id = r.Id,
                spotId = r.SpotId,
                rating = r.Rating,
                comment = r.Comment,
                createdAt = r.CreatedAt,
                updatedAt = r.UpdatedAt,
                isModerated = r.IsModerated,
                isHidden = r.IsHidden,
                spot = new
                {
                    id = r.Spot.Id,
                    name = r.Spot.Name
                }
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving reviews for user: {UserId}", userId);
            return StatusCode(500, new { message = "Failed to retrieve reviews" });
        }
    }

    [HttpPut("{id}")]
    [Authorize]
    public async Task<IActionResult> UpdateReview(Guid id, [FromBody] UpdateReviewDto request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var review = await _reviewService.GetReviewByIdAsync(id);
            if (review == null)
                return NotFound();

            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized();

            if (review.UserId != userId)
                return Forbid();

            var updateRequest = new UpdateReviewRequest
            {
                Rating = request.Rating,
                Comment = request.Comment
            };

            var updatedReview = await _reviewService.UpdateReviewAsync(id, updateRequest);

            return Ok(new
            {
                id = updatedReview.Id,
                rating = updatedReview.Rating,
                comment = updatedReview.Comment,
                updatedAt = updatedReview.UpdatedAt,
                isModerated = updatedReview.IsModerated
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating review: {ReviewId}", id);
            return StatusCode(500, new { message = "Failed to update review" });
        }
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> DeleteReview(Guid id)
    {
        try
        {
            var review = await _reviewService.GetReviewByIdAsync(id);
            if (review == null)
                return NotFound();

            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            var userRoleClaim = User.FindFirst(System.Security.Claims.ClaimTypes.Role);
            
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized();

            var isAdminOrModerator = userRoleClaim?.Value is "Admin" or "Moderator";

            if (review.UserId != userId && !isAdminOrModerator)
                return Forbid();

            var success = await _reviewService.DeleteReviewAsync(id, userId);
            if (!success)
                return NotFound();

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting review: {ReviewId}", id);
            return StatusCode(500, new { message = "Failed to delete review" });
        }
    }

    [HttpGet("spot/{spotId}/statistics")]
    public async Task<IActionResult> GetSpotReviewStatistics(Guid spotId)
    {
        try
        {
            var statistics = await _reviewService.GetSpotReviewStatisticsAsync(spotId);
            return Ok(statistics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving review statistics for spot: {SpotId}", spotId);
            return StatusCode(500, new { message = "Failed to retrieve statistics" });
        }
    }

    [HttpPost("{id}/report")]
    [Authorize]
    public async Task<IActionResult> ReportReview(Guid id, [FromBody] ReportReviewDto request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized();

            var success = await _reviewService.ReportReviewAsync(id, userId, request.Reason);
            if (!success)
                return NotFound();

            return Ok(new { message = "Review reported successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reporting review: {ReviewId}", id);
            return StatusCode(500, new { message = "Failed to report review" });
        }
    }

    [HttpGet("unmoderated")]
    [Authorize(Roles = "Admin,Moderator")]
    public async Task<IActionResult> GetUnmoderatedReviews([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            var reviews = await _reviewService.GetUnmoderatedReviewsAsync(page, pageSize);

            return Ok(reviews.Select(r => new
            {
                id = r.Id,
                spotId = r.SpotId,
                rating = r.Rating,
                comment = r.Comment,
                createdAt = r.CreatedAt,
                user = new
                {
                    id = r.User.Id,
                    firstName = r.User.FirstName,
                    lastName = r.User.LastName,
                    email = r.User.Email
                },
                spot = new
                {
                    id = r.Spot.Id,
                    name = r.Spot.Name
                }
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving unmoderated reviews");
            return StatusCode(500, new { message = "Failed to retrieve unmoderated reviews" });
        }
    }

    [HttpPost("{id}/moderate")]
    [Authorize(Roles = "Admin,Moderator")]
    public async Task<IActionResult> ModerateReview(Guid id, [FromBody] ModerateReviewDto request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var moderatorId))
                return Unauthorized();

            var success = await _reviewService.ModerateReviewAsync(id, request.IsApproved, moderatorId);
            if (!success)
                return NotFound();

            return Ok(new { message = "Review moderated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error moderating review: {ReviewId}", id);
            return StatusCode(500, new { message = "Failed to moderate review" });
        }
    }
}

public class CreateReviewDto
{
    [Required]
    public Guid SpotId { get; set; }

    [Required]
    [Range(1, 5)]
    public int Rating { get; set; }

    [MaxLength(1000)]
    public string? Comment { get; set; }
}

public class UpdateReviewDto
{
    [Required]
    [Range(1, 5)]
    public int Rating { get; set; }

    [MaxLength(1000)]
    public string? Comment { get; set; }
}

public class ReportReviewDto
{
    [Required]
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;
}

public class ModerateReviewDto
{
    [Required]
    public bool IsApproved { get; set; }
}