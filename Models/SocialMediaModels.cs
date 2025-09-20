using System.ComponentModel.DataAnnotations;

namespace AmalaSpotLocator.Models;

public class HashtagSearchRequest
{
    [Required]
    public string Hashtag { get; set; } = string.Empty;
    
    public string Platform { get; set; } = "all"; // twitter, instagram, tiktok, all
    
    public int MaxResults { get; set; } = 20;
    
    public DateTime? Since { get; set; }
    
    public string? Location { get; set; }
}

public class SocialMediaPost
{
    public string Id { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string AuthorHandle { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int Likes { get; set; }
    public int Shares { get; set; }
    public int Comments { get; set; }
    public List<string> Hashtags { get; set; } = new();
    public List<string> Mentions { get; set; } = new();
    public string? ImageUrl { get; set; }
    public string? VideoUrl { get; set; }
    public string? Location { get; set; }
    public string PostUrl { get; set; } = string.Empty;
    public double SentimentScore { get; set; } // -1 to 1
    public string SentimentLabel { get; set; } = "neutral"; // positive, negative, neutral
}

public class HashtagSearchResponse
{
    public string Hashtag { get; set; } = string.Empty;
    public List<SocialMediaPost> Posts { get; set; } = new();
    public int TotalFound { get; set; }
    public Dictionary<string, int> PlatformCounts { get; set; } = new();
    public Dictionary<string, int> SentimentCounts { get; set; } = new();
    public List<string> TrendingRelatedHashtags { get; set; } = new();
    public DateTime SearchedAt { get; set; } = DateTime.UtcNow;
}

public class SocialMediaTrends
{
    public List<TrendingHashtag> TrendingHashtags { get; set; } = new();
    public List<PopularPost> PopularPosts { get; set; } = new();
    public Dictionary<string, double> SentimentTrends { get; set; } = new();
}

public class TrendingHashtag
{
    public string Hashtag { get; set; } = string.Empty;
    public int PostCount { get; set; }
    public double GrowthRate { get; set; }
    public string Category { get; set; } = string.Empty; // food, restaurant, location
}

public class PopularPost
{
    public SocialMediaPost Post { get; set; } = new();
    public double PopularityScore { get; set; }
    public string PopularityReason { get; set; } = string.Empty; // viral, high_engagement, trending
}
public class RedditSearchResponse
{
    public RedditData? Data { get; set; }
}

public class RedditData
{
    public List<RedditChild> Children { get; set; } = new();
}

public class RedditChild
{
    public RedditPost? Data { get; set; }
}

public class RedditPost
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Selftext { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public double CreatedUtc { get; set; }
    public int Ups { get; set; }
    public int NumComments { get; set; }
    public string Permalink { get; set; } = string.Empty;
}
public class HackerNewsResponse
{
    public List<HackerNewsHit> Hits { get; set; } = new();
}

public class HackerNewsHit
{
    public string ObjectId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? StoryText { get; set; }
    public string Author { get; set; } = string.Empty;
    public string? Url { get; set; }
    public int Points { get; set; }
    public int NumComments { get; set; }
    public long CreatedAtI { get; set; }
}