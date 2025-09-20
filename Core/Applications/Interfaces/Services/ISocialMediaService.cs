using AmalaSpotLocator.Models;

namespace AmalaSpotLocator.Core.Applications.Interfaces.Services;

public interface ISocialMediaService
{
    Task<HashtagSearchResponse> SearchHashtagAsync(HashtagSearchRequest request);
    Task<SocialMediaTrends> GetTrendingContentAsync(string category = "food");
    Task<List<SocialMediaPost>> SearchMentionsAsync(string keyword, string platform = "all");
    Task<double> AnalyzeSentimentAsync(string text);
    Task<List<string>> ExtractHashtagsAsync(string text);
    Task<List<SocialMediaPost>> GetPopularAmalaPostsAsync(string location = "Lagos");
}