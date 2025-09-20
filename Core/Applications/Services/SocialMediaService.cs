using AmalaSpotLocator.Core.Applications.Interfaces.Services;
using AmalaSpotLocator.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AmalaSpotLocator.Core.Applications.Services;

public class SocialMediaService : ISocialMediaService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SocialMediaService> _logger;
    private readonly IConfiguration _configuration;

    private readonly string[] _amalaHashtags = {
        "#amala", "#amalaspot", "#amalalovers", "#yorubafood", "#nigerianfood",
        "#lagosrestaurant", "#abeokutafood", "#ibadanfood", "#gbegiri", "#ewedu",
        "#abula", "#mamaput", "#buka", "#localfood", "#naijafood"
    };

    public SocialMediaService(
        HttpClient httpClient,
        ILogger<SocialMediaService> logger,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration;
        
        _httpClient.DefaultRequestHeaders.Add("User-Agent", 
            "AmalaSpotLocator/1.0 Social Media Monitor");
    }

    public async Task<HashtagSearchResponse> SearchHashtagAsync(HashtagSearchRequest request)
    {
        try
        {
            _logger.LogInformation("Searching hashtag: {Hashtag} on {Platform}", 
                request.Hashtag, request.Platform);

            var response = new HashtagSearchResponse
            {
                Hashtag = request.Hashtag,
                SearchedAt = DateTime.UtcNow
            };

            var allPosts = new List<SocialMediaPost>();
            if (request.Platform == "all" || request.Platform == "reddit")
            {
                var redditPosts = await SearchRedditAsync(request);
                allPosts.AddRange(redditPosts);
            }
            if (request.Platform == "all" || request.Platform == "hackernews")
            {
                var hackerNewsPosts = await SearchHackerNewsAsync(request);
                allPosts.AddRange(hackerNewsPosts);
            }
            response.Posts = allPosts
                .OrderByDescending(p => p.Likes + p.Shares + p.Comments)
                .Take(request.MaxResults)
                .ToList();

            response.TotalFound = allPosts.Count;
            response.PlatformCounts = allPosts
                .GroupBy(p => p.Platform)
                .ToDictionary(g => g.Key, g => g.Count());

            response.SentimentCounts = allPosts
                .GroupBy(p => p.SentimentLabel)
                .ToDictionary(g => g.Key, g => g.Count());

            response.TrendingRelatedHashtags = ExtractRelatedHashtags(allPosts);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching hashtag {Hashtag}", request.Hashtag);
            throw;
        }
    }

    public async Task<SocialMediaTrends> GetTrendingContentAsync(string category = "food")
    {
        try
        {
            var trends = new SocialMediaTrends();
            var foodHashtags = category == "food" ? _amalaHashtags : new[] { $"#{category}" };
            
            foreach (var hashtag in foodHashtags.Take(5))
            {
                var searchRequest = new HashtagSearchRequest
                {
                    Hashtag = hashtag,
                    MaxResults = 10,
                    Since = DateTime.UtcNow.AddDays(-7)
                };
                
                var results = await SearchHashtagAsync(searchRequest);
                
                trends.TrendingHashtags.Add(new TrendingHashtag
                {
                    Hashtag = hashtag,
                    PostCount = results.TotalFound,
                    GrowthRate = CalculateGrowthRate(results.Posts),
                    Category = category
                });
                var popularPosts = results.Posts
                    .Take(3)
                    .Select(p => new PopularPost
                    {
                        Post = p,
                        PopularityScore = CalculatePopularityScore(p),
                        PopularityReason = GetPopularityReason(p)
                    });
                
                trends.PopularPosts.AddRange(popularPosts);
            }

            return trends;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting trending content for {Category}", category);
            throw;
        }
    }

    public async Task<List<SocialMediaPost>> SearchMentionsAsync(string keyword, string platform = "all")
    {
        try
        {
            var request = new HashtagSearchRequest
            {
                Hashtag = keyword,
                Platform = platform,
                MaxResults = 50
            };

            var response = await SearchHashtagAsync(request);
            return response.Posts;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching mentions for {Keyword}", keyword);
            throw;
        }
    }

    public async Task<double> AnalyzeSentimentAsync(string text)
    {
        try
        {
            var positiveWords = new[] { "good", "great", "excellent", "amazing", "delicious", "tasty", "best", "love", "awesome", "fantastic" };
            var negativeWords = new[] { "bad", "terrible", "awful", "disgusting", "worst", "hate", "horrible", "nasty", "poor", "disappointing" };

            var words = text.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            var positiveCount = words.Count(w => positiveWords.Contains(w));
            var negativeCount = words.Count(w => negativeWords.Contains(w));
            
            if (positiveCount == 0 && negativeCount == 0) return 0; // neutral
            
            var totalSentimentWords = positiveCount + negativeCount;
            var sentimentScore = (double)(positiveCount - negativeCount) / totalSentimentWords;
            
            return Math.Max(-1, Math.Min(1, sentimentScore));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing sentiment");
            return 0; // neutral on error
        }
    }

    public async Task<List<string>> ExtractHashtagsAsync(string text)
    {
        try
        {
            var hashtagPattern = @"#\w+";
            var matches = Regex.Matches(text, hashtagPattern, RegexOptions.IgnoreCase);
            
            return matches
                .Cast<Match>()
                .Select(m => m.Value.ToLowerInvariant())
                .Distinct()
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting hashtags");
            return new List<string>();
        }
    }

    public async Task<List<SocialMediaPost>> GetPopularAmalaPostsAsync(string location = "Lagos")
    {
        try
        {
            var allPosts = new List<SocialMediaPost>();

            foreach (var hashtag in _amalaHashtags.Take(5))
            {
                var request = new HashtagSearchRequest
                {
                    Hashtag = hashtag,
                    Location = location,
                    MaxResults = 10,
                    Since = DateTime.UtcNow.AddDays(-30)
                };

                var response = await SearchHashtagAsync(request);
                allPosts.AddRange(response.Posts);
            }

            return allPosts
                .OrderByDescending(p => CalculatePopularityScore(p))
                .Take(20)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting popular amala posts for {Location}", location);
            throw;
        }
    }
    private async Task<List<SocialMediaPost>> SearchRedditAsync(HashtagSearchRequest request)
    {
        try
        {
            var searchQuery = request.Hashtag.Replace("#", "") + " amala Nigerian food Lagos";
            var url = $"https://www.reddit.com/search.json" +
                     $"?q={Uri.EscapeDataString(searchQuery)}" +
                     $"&sort=hot" +
                     $"&limit={Math.Min(request.MaxResults, 25)}" +
                     $"&t=month" +
                     $"&type=link";
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "AmalaSpotLocator/1.0 (by /u/amalaspotlocator)");

            var response = await _httpClient.GetStringAsync(url);
            var redditResponse = JsonSerializer.Deserialize<RedditSearchResponse>(response);

            var posts = new List<SocialMediaPost>();

            if (redditResponse?.Data?.Children != null)
            {
                foreach (var child in redditResponse.Data.Children)
                {
                    var data = child.Data;
                    if (data == null || string.IsNullOrEmpty(data.Title)) continue;

                    var post = new SocialMediaPost
                    {
                        Id = data.Id,
                        Platform = "reddit",
                        Content = data.Title + (string.IsNullOrEmpty(data.Selftext) ? "" : " - " + data.Selftext),
                        Author = data.Author,
                        AuthorHandle = $"u/{data.Author}",
                        CreatedAt = DateTimeOffset.FromUnixTimeSeconds((long)data.CreatedUtc).DateTime,
                        Likes = data.Ups,
                        Shares = 0, // Reddit doesn't have shares
                        Comments = data.NumComments,
                        Hashtags = await ExtractHashtagsAsync(data.Title + " " + data.Selftext),
                        PostUrl = $"https://reddit.com{data.Permalink}",
                        SentimentScore = await AnalyzeSentimentAsync(data.Title + " " + data.Selftext)
                    };

                    post.SentimentLabel = post.SentimentScore switch
                    {
                        > 0.1 => "positive",
                        < -0.1 => "negative",
                        _ => "neutral"
                    };

                    posts.Add(post);
                }
            }

            _logger.LogInformation("Found {Count} Reddit posts for {Query}", posts.Count, searchQuery);
            return posts;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching Reddit for {Hashtag}", request.Hashtag);
            return new List<SocialMediaPost>();
        }
    }
    public async Task<List<string>> SearchGoogleCustomAsync(string query, int maxResults = 20)
{
    string apiKey = _configuration["Google:ApiKey"];
    string searchEngineId = _configuration["Google:SearchEngineId"];
    var results = new List<string>();

    int startIndex = 1;
    int remaining = maxResults;

    while (remaining > 0)
    {
        int num = Math.Min(10, remaining);

        var requestUrl = $"https://www.googleapis.com/customsearch/v1?q={Uri.EscapeDataString(query)}&key={apiKey}&cx={searchEngineId}&start={startIndex}&num={num}";
        var response = await _httpClient.GetAsync(requestUrl);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        if (doc.RootElement.TryGetProperty("items", out var items))
        {
            foreach (var item in items.EnumerateArray())
            {
                if (item.TryGetProperty("link", out var link))
                {
                    results.Add(link.GetString());
                }
            }
        }
        else
        {
            break; // No more results
        }

        remaining -= num;
        startIndex += num;
        if (!doc.RootElement.TryGetProperty("searchInformation", out var info) ||
            info.GetProperty("totalResults").GetString() == "0")
        {
            break;
        }
    }

    return results;
}


    private async Task<List<SocialMediaPost>> SearchHackerNewsAsync(HashtagSearchRequest request)
    {
        try
        {
            var searchQuery = request.Hashtag.Replace("#", "") + " Nigerian food amala restaurant";
            var url = $"https://hn.algolia.com/api/v1/search" +
                     $"?query={Uri.EscapeDataString(searchQuery)}" +
                     $"&tags=story" +
                     $"&hitsPerPage={Math.Min(request.MaxResults, 20)}";

            var response = await _httpClient.GetStringAsync(url);
            var hnResponse = JsonSerializer.Deserialize<HackerNewsResponse>(response);

            var posts = new List<SocialMediaPost>();

            if (hnResponse?.Hits != null)
            {
                foreach (var hit in hnResponse.Hits)
                {
                    if (string.IsNullOrEmpty(hit.Title)) continue;

                    var post = new SocialMediaPost
                    {
                        Id = hit.ObjectId,
                        Platform = "hackernews",
                        Content = hit.Title + (string.IsNullOrEmpty(hit.StoryText) ? "" : " - " + hit.StoryText),
                        Author = hit.Author,
                        AuthorHandle = hit.Author,
                        CreatedAt = DateTimeOffset.FromUnixTimeSeconds(hit.CreatedAtI).DateTime,
                        Likes = hit.Points,
                        Shares = 0,
                        Comments = hit.NumComments,
                        Hashtags = await ExtractHashtagsAsync(hit.Title + " " + hit.StoryText),
                        PostUrl = string.IsNullOrEmpty(hit.Url) ? $"https://news.ycombinator.com/item?id={hit.ObjectId}" : hit.Url,
                        SentimentScore = await AnalyzeSentimentAsync(hit.Title + " " + hit.StoryText)
                    };

                    post.SentimentLabel = post.SentimentScore switch
                    {
                        > 0.1 => "positive",
                        < -0.1 => "negative",
                        _ => "neutral"
                    };

                    posts.Add(post);
                }
            }

            _logger.LogInformation("Found {Count} HackerNews posts for {Query}", posts.Count, searchQuery);
            return posts;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching HackerNews for {Hashtag}", request.Hashtag);
            return new List<SocialMediaPost>();
        }
    }

    private List<string> ExtractRelatedHashtags(List<SocialMediaPost> posts)
    {
        return posts
            .SelectMany(p => p.Hashtags)
            .GroupBy(h => h)
            .OrderByDescending(g => g.Count())
            .Take(10)
            .Select(g => g.Key)
            .ToList();
    }

    private double CalculateGrowthRate(List<SocialMediaPost> posts)
    {
        if (!posts.Any()) return 0;
        
        var recentPosts = posts.Count(p => p.CreatedAt > DateTime.UtcNow.AddDays(-1));
        var olderPosts = posts.Count(p => p.CreatedAt <= DateTime.UtcNow.AddDays(-1));
        
        return olderPosts == 0 ? 100 : ((double)recentPosts / olderPosts - 1) * 100;
    }

    private double CalculatePopularityScore(SocialMediaPost post)
    {
        var engagementScore = post.Likes + (post.Shares * 2) + (post.Comments * 1.5);
        var recencyBonus = Math.Max(0, 1 - (DateTime.UtcNow - post.CreatedAt).TotalDays / 7);
        var sentimentBonus = Math.Max(0, post.SentimentScore);
        
        return engagementScore * (1 + recencyBonus) * (1 + sentimentBonus);
    }

    private string GetPopularityReason(SocialMediaPost post)
    {
        var totalEngagement = post.Likes + post.Shares + post.Comments;
        
        if (totalEngagement > 1000) return "viral";
        if (post.Likes > 500) return "high_engagement";
        if (post.CreatedAt > DateTime.UtcNow.AddHours(-6)) return "trending";
        
        return "popular";
    }
}