using System.Collections.Concurrent;
using AmalaSpotLocator.Core.Applications.Interfaces.Services;

namespace AmalaSpotLocator.Core.Applications.Services;

public class RateLimitingService : IRateLimitingService
{
    private readonly ConcurrentDictionary<string, ClientRateLimit> _rateLimits = new();
    private readonly ILogger<RateLimitingService> _logger;

    private readonly Dictionary<string, RateLimitConfig> _configs = new()
    {
        { "chat/text", new RateLimitConfig { RequestsPerMinute = 30, RequestsPerHour = 500 } },
        { "chat/voice", new RateLimitConfig { RequestsPerMinute = 10, RequestsPerHour = 100 } },
        { "default", new RateLimitConfig { RequestsPerMinute = 60, RequestsPerHour = 1000 } }
    };
    
    public RateLimitingService(ILogger<RateLimitingService> logger)
    {
        _logger = logger;
    }
    
    public Task<bool> IsRequestAllowedAsync(string clientId, string endpoint)
    {
        var key = $"{clientId}:{endpoint}";
        var config = GetConfigForEndpoint(endpoint);
        
        var rateLimit = _rateLimits.GetOrAdd(key, _ => new ClientRateLimit());
        
        var now = DateTime.UtcNow;

        rateLimit.Requests.RemoveAll(r => r < now.AddMinutes(-1));
        rateLimit.HourlyRequests.RemoveAll(r => r < now.AddHours(-1));

        if (rateLimit.Requests.Count >= config.RequestsPerMinute)
        {
            _logger.LogWarning("Rate limit exceeded for client {ClientId} on endpoint {Endpoint} (minute limit)", 
                clientId, endpoint);
            return Task.FromResult(false);
        }

        if (rateLimit.HourlyRequests.Count >= config.RequestsPerHour)
        {
            _logger.LogWarning("Rate limit exceeded for client {ClientId} on endpoint {Endpoint} (hourly limit)", 
                clientId, endpoint);
            return Task.FromResult(false);
        }

        rateLimit.Requests.Add(now);
        rateLimit.HourlyRequests.Add(now);
        
        return Task.FromResult(true);
    }
    
    public Task<int> GetRemainingRequestsAsync(string clientId, string endpoint)
    {
        var key = $"{clientId}:{endpoint}";
        var config = GetConfigForEndpoint(endpoint);
        
        if (!_rateLimits.TryGetValue(key, out var rateLimit))
        {
            return Task.FromResult(config.RequestsPerMinute);
        }
        
        var now = DateTime.UtcNow;
        var recentRequests = rateLimit.Requests.Count(r => r > now.AddMinutes(-1));
        
        return Task.FromResult(Math.Max(0, config.RequestsPerMinute - recentRequests));
    }
    
    public Task<TimeSpan> GetTimeUntilResetAsync(string clientId, string endpoint)
    {
        var key = $"{clientId}:{endpoint}";
        
        if (!_rateLimits.TryGetValue(key, out var rateLimit) || !rateLimit.Requests.Any())
        {
            return Task.FromResult(TimeSpan.Zero);
        }
        
        var oldestRequest = rateLimit.Requests.Min();
        var resetTime = oldestRequest.AddMinutes(1);
        var timeUntilReset = resetTime - DateTime.UtcNow;
        
        return Task.FromResult(timeUntilReset > TimeSpan.Zero ? timeUntilReset : TimeSpan.Zero);
    }
    
    private RateLimitConfig GetConfigForEndpoint(string endpoint)
    {
        return _configs.TryGetValue(endpoint, out var config) ? config : _configs["default"];
    }
}

public class ClientRateLimit
{
    public List<DateTime> Requests { get; set; } = new();
    public List<DateTime> HourlyRequests { get; set; } = new();
}

public class RateLimitConfig
{
    public int RequestsPerMinute { get; set; }
    public int RequestsPerHour { get; set; }
}