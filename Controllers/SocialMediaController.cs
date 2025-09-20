using AmalaSpotLocator.Core.Applications.Interfaces.Services;
using AmalaSpotLocator.Models;
using Microsoft.AspNetCore.Mvc;

namespace AmalaSpotLocator.Controllers;

[ApiController]
[Route("api/socialmedia")]
public class SocialMediaController : ControllerBase
{
    private readonly ISocialMediaService _socialMediaService;
    private readonly ILogger<SocialMediaController> _logger;

    public SocialMediaController(
        ISocialMediaService socialMediaService,
        ILogger<SocialMediaController> logger)
    {
        _socialMediaService = socialMediaService;
        _logger = logger;
    }

    [HttpPost("search-hashtag")]
    public async Task<ActionResult<HashtagSearchResponse>> SearchHashtag([FromBody] HashtagSearchRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _socialMediaService.SearchHashtagAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching hashtag {Hashtag}", request.Hashtag);
            return StatusCode(500, new { error = "Failed to search hashtag", details = ex.Message });
        }
    }

    [HttpGet("trending")]
    public async Task<ActionResult<SocialMediaTrends>> GetTrending([FromQuery] string category = "food")
    {
        try
        {
            var trends = await _socialMediaService.GetTrendingContentAsync(category);
            return Ok(trends);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting trending content for {Category}", category);
            return StatusCode(500, new { error = "Failed to get trending content", details = ex.Message });
        }
    }

    [HttpGet("mentions")]
    public async Task<ActionResult<List<SocialMediaPost>>> SearchMentions(
        [FromQuery] string keyword,
        [FromQuery] string platform = "all")
    {
        try
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return BadRequest("Keyword is required");
            }

            var posts = await _socialMediaService.SearchMentionsAsync(keyword, platform);
            return Ok(posts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching mentions for {Keyword}", keyword);
            return StatusCode(500, new { error = "Failed to search mentions", details = ex.Message });
        }
    }

    [HttpGet("popular-amala-posts")]
    public async Task<ActionResult<List<SocialMediaPost>>> GetPopularAmalaPosts([FromQuery] string location = "Lagos")
    {
        try
        {
            var posts = await _socialMediaService.GetPopularAmalaPostsAsync(location);
            return Ok(posts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting popular amala posts for {Location}", location);
            return StatusCode(500, new { error = "Failed to get popular posts", details = ex.Message });
        }
    }

    [HttpPost("analyze-sentiment")]
    public async Task<ActionResult<object>> AnalyzeSentiment([FromBody] SentimentAnalysisRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Text))
            {
                return BadRequest("Text is required");
            }

            var sentimentScore = await _socialMediaService.AnalyzeSentimentAsync(request.Text);
            var hashtags = await _socialMediaService.ExtractHashtagsAsync(request.Text);

            var sentimentLabel = sentimentScore switch
            {
                > 0.1 => "positive",
                < -0.1 => "negative",
                _ => "neutral"
            };

            return Ok(new
            {
                text = request.Text,
                sentimentScore,
                sentimentLabel,
                hashtags,
                analyzedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing sentiment");
            return StatusCode(500, new { error = "Failed to analyze sentiment", details = ex.Message });
        }
    }

    [HttpGet("amala-buzz")]
    public async Task<ActionResult<object>> GetAmalaBuzz([FromQuery] string location = "Lagos")
    {
        try
        {
            var amalaHashtags = new[] { "#amala", "#amalaspot", "#amalalovers", "#yorubafood", "#nigerianfood" };
            var allPosts = new List<SocialMediaPost>();

            foreach (var hashtag in amalaHashtags.Take(3))
            {
                var request = new HashtagSearchRequest
                {
                    Hashtag = hashtag,
                    MaxResults = 5,
                    Location = location,
                    Since = DateTime.UtcNow.AddDays(-7)
                };

                var response = await _socialMediaService.SearchHashtagAsync(request);
                allPosts.AddRange(response.Posts);
            }

            var topPosts = allPosts
                .OrderByDescending(p => p.Likes + p.Shares + p.Comments)
                .Take(10)
                .ToList();

            var sentimentBreakdown = allPosts
                .GroupBy(p => p.SentimentLabel)
                .ToDictionary(g => g.Key, g => g.Count());

            var platformBreakdown = allPosts
                .GroupBy(p => p.Platform)
                .ToDictionary(g => g.Key, g => g.Count());

            return Ok(new
            {
                location,
                totalPosts = allPosts.Count,
                topPosts,
                sentimentBreakdown,
                platformBreakdown,
                searchedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting amala buzz for {Location}", location);
            return StatusCode(500, new { error = "Failed to get amala buzz", details = ex.Message });
        }
    }
}

public class SentimentAnalysisRequest
{
    public string Text { get; set; } = string.Empty;
}