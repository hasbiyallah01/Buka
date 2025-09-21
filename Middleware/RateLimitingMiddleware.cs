using AmalaSpotLocator.Core.Applications.Interfaces.Services;
using System.Net;

namespace AmalaSpotLocator.Middleware;

public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IRateLimitingService _rateLimitingService;
    private readonly ILogger<RateLimitingMiddleware> _logger;

    public RateLimitingMiddleware(
        RequestDelegate next,
        IRateLimitingService rateLimitingService,
        ILogger<RateLimitingMiddleware> logger)
    {
        _next = next;
        _rateLimitingService = rateLimitingService;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {

        if (ShouldSkipRateLimit(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var clientId = GetClientIdentifier(context);
        var endpoint = GetEndpointKey(context.Request);

        if (!await _rateLimitingService.IsRequestAllowedAsync(clientId, endpoint))
        {
            var timeUntilReset = await _rateLimitingService.GetTimeUntilResetAsync(clientId, endpoint);
            
            _logger.LogWarning("Rate limit exceeded for client {ClientId} on endpoint {Endpoint}", 
                clientId, endpoint);

            context.Response.Headers.Append("X-RateLimit-Limit", "100");
            context.Response.Headers.Append("X-RateLimit-Remaining", "0");
            context.Response.Headers.Append("X-RateLimit-Reset", DateTimeOffset.UtcNow.Add(timeUntilReset).ToUnixTimeSeconds().ToString());
            context.Response.Headers.Append("Retry-After", ((int)timeUntilReset.TotalSeconds).ToString());

            context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
            context.Response.ContentType = "application/json";

            var response = new
            {
                error = "Rate limit exceeded",
                message = $"Too many requests. Try again in {timeUntilReset.TotalSeconds:F0} seconds.",
                retryAfter = (int)timeUntilReset.TotalSeconds
            };

            await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
            return;
        }

        var remaining = await _rateLimitingService.GetRemainingRequestsAsync(clientId, endpoint);
        var resetTime = DateTimeOffset.UtcNow.AddMinutes(1); 

        context.Response.OnStarting(() =>
        {
            context.Response.Headers.Append("X-RateLimit-Limit", GetRateLimitForEndpoint(endpoint).ToString());
            context.Response.Headers.Append("X-RateLimit-Remaining", remaining.ToString());
            context.Response.Headers.Append("X-RateLimit-Reset", resetTime.ToUnixTimeSeconds().ToString());
            return Task.CompletedTask;
        });

        await _next(context);
    }

    private static bool ShouldSkipRateLimit(PathString path)
    {
        var pathValue = path.Value?.ToLower() ?? "";
        
        return pathValue.StartsWith("/health") ||
               pathValue.StartsWith("/swagger") ||
               pathValue.StartsWith("/favicon") ||
               pathValue.Contains(".css") ||
               pathValue.Contains(".js") ||
               pathValue.Contains(".png") ||
               pathValue.Contains(".jpg") ||
               pathValue.Contains(".ico");
    }

    private static string GetClientIdentifier(HttpContext context)
    {

        var userIdClaim = context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (userIdClaim != null)
        {
            return $"user:{userIdClaim.Value}";
        }

        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        if (context.Request.Headers.ContainsKey("X-Forwarded-For"))
        {
            var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwardedFor))
            {
                ipAddress = forwardedFor.Split(',')[0].Trim();
            }
        }
        else if (context.Request.Headers.ContainsKey("X-Real-IP"))
        {
            var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrEmpty(realIp))
            {
                ipAddress = realIp;
            }
        }

        return $"ip:{ipAddress}";
    }

    private static string GetEndpointKey(HttpRequest request)
    {
        var path = request.Path.Value?.ToLower() ?? "";
        var method = request.Method.ToUpper();

        if (path.StartsWith("/api/chat"))
        {
            if (path.Contains("/voice"))
                return "chat/voice";
            return "chat/text";
        }

        if (path.StartsWith("/api/spots"))
        {
            if (method == "GET")
                return "spots/read";
            if (method == "POST")
                return "spots/create";
            return "spots/modify";
        }

        if (path.StartsWith("/api/reviews"))
        {
            if (method == "GET")
                return "reviews/read";
            if (method == "POST")
                return "reviews/create";
            return "reviews/modify";
        }

        if (path.StartsWith("/api/auth"))
        {
            return "auth";
        }

        return $"{method.ToLower()}/general";
    }

    private static int GetRateLimitForEndpoint(string endpoint)
    {
        return endpoint switch
        {
            "chat/voice" => 20,      
            "chat/text" => 100,      
            "spots/create" => 10,    
            "spots/modify" => 20,    
            "spots/read" => 200,     
            "reviews/create" => 15,  
            "reviews/modify" => 10,  
            "reviews/read" => 100,   
            "auth" => 5,             
            _ => 100                 
        };
    }
}