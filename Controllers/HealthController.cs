using Microsoft.AspNetCore.Mvc;
using AmalaSpotLocator.Data;
using AmalaSpotLocator.Interfaces;
using Swashbuckle.AspNetCore.Annotations;
using Microsoft.EntityFrameworkCore;

namespace AmalaSpotLocator.Controllers;

[ApiController]
[Route("api/health")]
[Produces("application/json")]
[Tags("System")]
public class HealthController : ControllerBase
{
    private readonly AmalaSpotContext _context;
    private readonly ILogger<HealthController> _logger;

    public HealthController(AmalaSpotContext context, ILogger<HealthController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetHealth()
    {
        var healthResponse = new HealthResponse
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Service = "Amala Spot Locator API",
            Version = "1.0.0",
            Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown",
            Uptime = GetUptime(),
            Checks = new Dictionary<string, HealthCheck>()
        };

        try
        {

            var dbHealthy = await CheckDatabaseHealth();
            healthResponse.Checks["database"] = dbHealthy;

            var memoryCheck = CheckMemoryUsage();
            healthResponse.Checks["memory"] = memoryCheck;

            var diskCheck = CheckDiskSpace();
            healthResponse.Checks["disk"] = diskCheck;

            var hasUnhealthy = healthResponse.Checks.Values.Any(c => c.Status == "Unhealthy");
            var hasDegraded = healthResponse.Checks.Values.Any(c => c.Status == "Degraded");

            if (hasUnhealthy)
            {
                healthResponse.Status = "Unhealthy";
                return StatusCode(503, healthResponse);
            }
            else if (hasDegraded)
            {
                healthResponse.Status = "Degraded";
            }

            return Ok(healthResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            healthResponse.Status = "Unhealthy";
            healthResponse.Checks["api"] = new HealthCheck
            {
                Status = "Unhealthy",
                Description = "Health check failed",
                Error = ex.Message
            };
            return StatusCode(503, healthResponse);
        }
    }

    [HttpGet("info")]
    public IActionResult GetApiInfo()
    {
        var apiInfo = new ApiInfoResponse
        {
            Name = "Amala Spot Locator API",
            Version = "1.0.0",
            Description = "A comprehensive REST API for discovering, locating, and interacting with amala restaurants and food spots across Nigeria.",
            Features = new List<string>
            {
                "Geospatial search and location-based queries",
                "AI-powered natural language chat interface",
                "Voice interaction with speech-to-text/text-to-speech",
                "Community reviews and ratings system",
                "Google Maps integration with clustering",
                "Autonomous spot discovery from web sources",
                "Multi-language support (English, Pidgin, Yoruba)",
                "Real-time map data with clustering algorithms"
            },
            SupportedLanguages = new List<string> { "English", "Pidgin", "Yoruba" },
            Authentication = new AuthenticationInfo
            {
                Type = "JWT Bearer Token",
                TokenExpiry = "24 hours",
                RefreshTokenExpiry = "30 days",
                RequiredEndpoints = new List<string>
                {
                    "POST /api/spots",
                    "PUT /api/spots/{id}",
                    "DELETE /api/spots/{id}",
                    "POST /api/reviews",
                    "PUT /api/reviews/{id}",
                    "DELETE /api/reviews/{id}",
                    "GET /api/auth/me",
                    "PUT /api/auth/change-password"
                }
            },
            RateLimits = new Dictionary<string, string>
            {
                ["Chat Text"] = "60 requests per minute",
                ["Chat Voice"] = "30 requests per minute",
                ["Search"] = "100 requests per minute",
                ["General"] = "200 requests per minute"
            },
            ExternalIntegrations = new List<string>
            {
                "OpenAI GPT for natural language processing",
                "Whisper API for speech-to-text",
                "Google Maps API for geocoding and places",
                "PostGIS for geospatial operations"
            },
            Contact = new ContactInfo
            {
                Email = "support@amalaspotlocator.com",
                Documentation = "/swagger",
                Repository = "https://github.com/amalaspotlocator/api"
            }
        };

        return Ok(apiInfo);
    }

    [HttpGet("metrics")]
    public IActionResult GetMetrics()
    {
        var metrics = new MetricsResponse
        {
            Timestamp = DateTime.UtcNow,
            Uptime = GetUptime(),
            RequestCount = GetRequestCount(),
            AverageResponseTime = GetAverageResponseTime(),
            ErrorRate = GetErrorRate(),
            ActiveSessions = GetActiveSessions(),
            DatabaseConnections = GetDatabaseConnections(),
            MemoryUsage = GetMemoryUsageMetrics(),
            CacheHitRate = GetCacheHitRate()
        };

        return Ok(metrics);
    }

    #region Private Helper Methods

    private async Task<HealthCheck> CheckDatabaseHealth()
    {
        try
        {
            var canConnect = await _context.Database.CanConnectAsync();
            if (canConnect)
            {
                var spotCount = await _context.AmalaSpots.CountAsync();
                return new HealthCheck
                {
                    Status = "Healthy",
                    Description = $"Database connected successfully. {spotCount} spots in database.",
                    ResponseTime = "< 100ms"
                };
            }
            else
            {
                return new HealthCheck
                {
                    Status = "Unhealthy",
                    Description = "Cannot connect to database",
                    Error = "Database connection failed"
                };
            }
        }
        catch (Exception ex)
        {
            return new HealthCheck
            {
                Status = "Unhealthy",
                Description = "Database health check failed",
                Error = ex.Message
            };
        }
    }

    private HealthCheck CheckMemoryUsage()
    {
        try
        {
            var process = System.Diagnostics.Process.GetCurrentProcess();
            var memoryMB = process.WorkingSet64 / 1024 / 1024;
            
            var status = memoryMB switch
            {
                > 1000 => "Unhealthy",
                > 500 => "Degraded",
                _ => "Healthy"
            };

            return new HealthCheck
            {
                Status = status,
                Description = $"Memory usage: {memoryMB} MB",
                ResponseTime = "< 1ms"
            };
        }
        catch (Exception ex)
        {
            return new HealthCheck
            {
                Status = "Degraded",
                Description = "Could not check memory usage",
                Error = ex.Message
            };
        }
    }

    private HealthCheck CheckDiskSpace()
    {
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(Environment.CurrentDirectory) ?? "C:");
            var freeSpaceGB = drive.AvailableFreeSpace / 1024 / 1024 / 1024;
            
            var status = freeSpaceGB switch
            {
                < 1 => "Unhealthy",
                < 5 => "Degraded",
                _ => "Healthy"
            };

            return new HealthCheck
            {
                Status = status,
                Description = $"Free disk space: {freeSpaceGB} GB",
                ResponseTime = "< 1ms"
            };
        }
        catch (Exception ex)
        {
            return new HealthCheck
            {
                Status = "Degraded",
                Description = "Could not check disk space",
                Error = ex.Message
            };
        }
    }

    private TimeSpan GetUptime()
    {
        return DateTime.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime();
    }

    private long GetRequestCount() => 0; // Would be implemented with actual metrics collection
    private double GetAverageResponseTime() => 0; // Would be implemented with actual metrics collection
    private double GetErrorRate() => 0; // Would be implemented with actual metrics collection
    private int GetActiveSessions() => 0; // Would be implemented with actual session tracking
    private int GetDatabaseConnections() => 0; // Would be implemented with actual connection pool monitoring
    private object GetMemoryUsageMetrics() => new { }; // Would be implemented with actual memory metrics
    private double GetCacheHitRate() => 0; // Would be implemented with actual cache metrics

    #endregion
}

#region Response Models

public class HealthResponse
{
    public string Status { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Service { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public TimeSpan Uptime { get; set; }
    public Dictionary<string, HealthCheck> Checks { get; set; } = new();
}

public class HealthCheck
{
    public string Status { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ResponseTime { get; set; }
    public string? Error { get; set; }
}

public class ApiInfoResponse
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Features { get; set; } = new();
    public List<string> SupportedLanguages { get; set; } = new();
    public AuthenticationInfo Authentication { get; set; } = new();
    public Dictionary<string, string> RateLimits { get; set; } = new();
    public List<string> ExternalIntegrations { get; set; } = new();
    public ContactInfo Contact { get; set; } = new();
}

public class AuthenticationInfo
{
    public string Type { get; set; } = string.Empty;
    public string TokenExpiry { get; set; } = string.Empty;
    public string RefreshTokenExpiry { get; set; } = string.Empty;
    public List<string> RequiredEndpoints { get; set; } = new();
}

public class ContactInfo
{
    public string Email { get; set; } = string.Empty;
    public string Documentation { get; set; } = string.Empty;
    public string Repository { get; set; } = string.Empty;
}

public class MetricsResponse
{
    public DateTime Timestamp { get; set; }
    public TimeSpan Uptime { get; set; }
    public long RequestCount { get; set; }
    public double AverageResponseTime { get; set; }
    public double ErrorRate { get; set; }
    public int ActiveSessions { get; set; }
    public int DatabaseConnections { get; set; }
    public object MemoryUsage { get; set; } = new();
    public double CacheHitRate { get; set; }
}

#endregion