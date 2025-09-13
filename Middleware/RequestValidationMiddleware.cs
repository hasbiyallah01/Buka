using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace AmalaSpotLocator.Middleware;

public class RequestValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestValidationMiddleware> _logger;

    public RequestValidationMiddleware(RequestDelegate next, ILogger<RequestValidationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {

        if (!ValidateRequestSize(context))
        {
            await WriteErrorResponse(context, 413, "Request too large", 
                "The request payload exceeds the maximum allowed size");
            return;
        }

        if (!ValidateContentType(context))
        {
            await WriteErrorResponse(context, 415, "Unsupported media type", 
                "The request content type is not supported");
            return;
        }

        if (!ValidateHeaders(context))
        {
            await WriteErrorResponse(context, 400, "Invalid headers", 
                "One or more request headers are invalid");
            return;
        }

        if (!ValidateUrl(context))
        {
            await WriteErrorResponse(context, 400, "Invalid URL", 
                "The request URL contains invalid characters or is too long");
            return;
        }

        await _next(context);
    }

    private bool ValidateRequestSize(HttpContext context)
    {
        const long maxRequestSize = 10 * 1024 * 1024; // 10MB
        
        if (context.Request.ContentLength.HasValue && 
            context.Request.ContentLength.Value > maxRequestSize)
        {
            _logger.LogWarning("Request size {Size} exceeds maximum allowed size from {RemoteIpAddress}", 
                context.Request.ContentLength.Value, context.Connection.RemoteIpAddress);
            return false;
        }

        return true;
    }

    private bool ValidateContentType(HttpContext context)
    {
        var method = context.Request.Method.ToUpper();

        if (method != "POST" && method != "PUT" && method != "PATCH")
            return true;

        var contentType = context.Request.ContentType?.ToLower();

        if (string.IsNullOrEmpty(contentType) && 
            (!context.Request.ContentLength.HasValue || context.Request.ContentLength.Value == 0))
            return true;

        var allowedContentTypes = new[]
        {
            "application/json",
            "multipart/form-data",
            "application/x-www-form-urlencoded"
        };

        if (contentType != null && allowedContentTypes.Any(allowed => contentType.StartsWith(allowed)))
            return true;

        _logger.LogWarning("Invalid content type {ContentType} from {RemoteIpAddress}", 
            contentType, context.Connection.RemoteIpAddress);
        return false;
    }

    private bool ValidateHeaders(HttpContext context)
    {

        var suspiciousHeaders = new[]
        {
            "x-forwarded-host",
            "x-original-url",
            "x-rewrite-url"
        };

        foreach (var header in suspiciousHeaders)
        {
            if (context.Request.Headers.ContainsKey(header))
            {
                var value = context.Request.Headers[header].ToString();
                if (ContainsSuspiciousContent(value))
                {
                    _logger.LogWarning("Suspicious header {Header} with value {Value} from {RemoteIpAddress}", 
                        header, value, context.Connection.RemoteIpAddress);
                    return false;
                }
            }
        }

        var userAgent = context.Request.Headers.UserAgent.ToString();
        if (!string.IsNullOrEmpty(userAgent) && (userAgent.Length > 500 || ContainsSuspiciousContent(userAgent)))
        {
            _logger.LogWarning("Suspicious User-Agent {UserAgent} from {RemoteIpAddress}", 
                userAgent, context.Connection.RemoteIpAddress);
            return false;
        }

        if (context.Request.Headers.ContainsKey("Authorization"))
        {
            var authHeader = context.Request.Headers.Authorization.ToString();
            if (!IsValidAuthorizationHeader(authHeader))
            {
                _logger.LogWarning("Invalid Authorization header format from {RemoteIpAddress}", 
                    context.Connection.RemoteIpAddress);
                return false;
            }
        }

        return true;
    }

    private bool ValidateUrl(HttpContext context)
    {
        var url = context.Request.Path + context.Request.QueryString;
        var urlString = url.ToString();

        if (urlString.Length > 2048)
        {
            _logger.LogWarning("URL too long ({Length} characters) from {RemoteIpAddress}", 
                urlString.Length, context.Connection.RemoteIpAddress);
            return false;
        }

        var lowerUrlString = urlString.ToLower();
        var suspiciousPatterns = new[]
        {
            "../",
            "..\\",
            "%2e%2e",
            "%252e%252e",
            "javascript:",
            "vbscript:",
            "data:",
            "file:",
            "<script",
            "%3cscript"
        };

        if (suspiciousPatterns.Any(pattern => lowerUrlString.Contains(pattern)))
        {
            _logger.LogWarning("Suspicious URL pattern detected in {Url} from {RemoteIpAddress}", 
                urlString, context.Connection.RemoteIpAddress);
            return false;
        }

        return true;
    }

    private static bool ContainsSuspiciousContent(string content)
    {
        if (string.IsNullOrEmpty(content))
            return false;

        var suspiciousPatterns = new[]
        {
            "<script",
            "javascript:",
            "vbscript:",
            "onload=",
            "onerror=",
            "onclick=",
            "eval(",
            "expression(",
            "url(",
            "@import"
        };

        var lowerContent = content.ToLower();
        return suspiciousPatterns.Any(pattern => lowerContent.Contains(pattern));
    }

    private static bool IsValidAuthorizationHeader(string authHeader)
    {
        if (string.IsNullOrEmpty(authHeader))
            return false;

        if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = authHeader.Substring(7);

            return token.Length > 10 && token.Length < 2048 && 
                   token.All(c => char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '_');
        }

        if (authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            var credentials = authHeader.Substring(6);
            try
            {
                Convert.FromBase64String(credentials);
                return true;
            }
            catch
            {
                return false;
            }
        }

        return false;
    }

    private static async Task WriteErrorResponse(HttpContext context, int statusCode, string error, string message)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var response = new
        {
            error,
            message,
            timestamp = DateTime.UtcNow,
            correlationId = context.TraceIdentifier
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }));
    }
}