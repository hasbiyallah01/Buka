namespace AmalaSpotLocator.Core.Applications.Interfaces.Services;

public interface IRateLimitingService
{

    Task<bool> IsRequestAllowedAsync(string clientId, string endpoint);

    Task<int> GetRemainingRequestsAsync(string clientId, string endpoint);

    Task<TimeSpan> GetTimeUntilResetAsync(string clientId, string endpoint);
}