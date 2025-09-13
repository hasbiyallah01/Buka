using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AmalaSpotLocator.Middleware;

public class InputSanitizationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<InputSanitizationMiddleware> _logger;

    private static readonly Regex[] MaliciousPatterns = {
        new(@"<script[^>]*>.*?</script>", RegexOptions.IgnoreCase | RegexOptions.Singleline),
        new(@"javascript:", RegexOptions.IgnoreCase),
        new(@"vbscript:", RegexOptions.IgnoreCase),
        new(@"onload\s*=", RegexOptions.IgnoreCase),
        new(@"onerror\s*=", RegexOptions.IgnoreCase),
        new(@"onclick\s*=", RegexOptions.IgnoreCase),
        new(@"<iframe[^>]*>", RegexOptions.IgnoreCase),
        new(@"<object[^>]*>", RegexOptions.IgnoreCase),
        new(@"<embed[^>]*>", RegexOptions.IgnoreCase),
        new(@"<link[^>]*>", RegexOptions.IgnoreCase),
        new(@"<meta[^>]*>", RegexOptions.IgnoreCase),
        new(@"expression\s*\(", RegexOptions.IgnoreCase),
        new(@"url\s*\(", RegexOptions.IgnoreCase),
        new(@"@import", RegexOptions.IgnoreCase),
        new(@"<!--.*?-->", RegexOptions.Singleline),

        new(@"(\b(ALTER|CREATE|DELETE|DROP|EXEC(UTE)?|INSERT( +INTO)?|MERGE|SELECT|UPDATE|UNION( +ALL)?)\b)", RegexOptions.IgnoreCase),
        new(@"(\b(AND|OR)\b.{1,6}?(=|>|<|\!=|<>|<=|>=))", RegexOptions.IgnoreCase),
        new(@"(\b(AND|OR)\b.{1,6}?\b(TRUE|FALSE)\b)", RegexOptions.IgnoreCase),
        new(@"1\s*=\s*1", RegexOptions.IgnoreCase),
        new(@"1\s*=\s*0", RegexOptions.IgnoreCase),
        new(@"'\s*OR\s*'", RegexOptions.IgnoreCase),
        new(@"--", RegexOptions.IgnoreCase),
        new(@"/\*.*?\*/", RegexOptions.Singleline),

        new(@"[;&|`]", RegexOptions.None),
        new(@"\$\(.*?\)", RegexOptions.None),
        new(@"`.*?`", RegexOptions.None)
    };

    public InputSanitizationMiddleware(RequestDelegate next, ILogger<InputSanitizationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {

        if (ShouldProcessRequest(context.Request))
        {
            await ProcessRequestBody(context);
        }

        ProcessQueryParameters(context);

        await _next(context);
    }

    private static bool ShouldProcessRequest(HttpRequest request)
    {
        var method = request.Method.ToUpper();
        var contentType = request.ContentType?.ToLower();
        
        return (method == "POST" || method == "PUT" || method == "PATCH") &&
               contentType != null &&
               contentType.Contains("application/json");
    }

    private async Task ProcessRequestBody(HttpContext context)
    {
        context.Request.EnableBuffering();
        
        using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        context.Request.Body.Position = 0;

        if (string.IsNullOrEmpty(body))
            return;

        if (ContainsMaliciousContent(body))
        {
            _logger.LogWarning("Malicious content detected in request body from {RemoteIpAddress}", 
                context.Connection.RemoteIpAddress);
            
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                error = "Invalid input detected",
                message = "The request contains potentially harmful content"
            }));
            return;
        }

        try
        {
            var jsonDocument = JsonDocument.Parse(body);
            var sanitizedJson = SanitizeJsonElement(jsonDocument.RootElement);
            
            if (sanitizedJson != body)
            {
                _logger.LogInformation("Sanitized request body for {Path}", context.Request.Path);

                var sanitizedBytes = Encoding.UTF8.GetBytes(sanitizedJson);
                context.Request.Body = new MemoryStream(sanitizedBytes);
            }
        }
        catch (JsonException)
        {

            _logger.LogWarning("Invalid JSON in request body from {RemoteIpAddress}", 
                context.Connection.RemoteIpAddress);
        }
    }

    private void ProcessQueryParameters(HttpContext context)
    {
        var sanitizedQuery = new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>();
        bool hasChanges = false;

        foreach (var param in context.Request.Query)
        {
            var sanitizedValues = new List<string>();
            
            foreach (var value in param.Value)
            {
                if (value != null)
                {
                    var sanitized = SanitizeString(value);
                    sanitizedValues.Add(sanitized);
                    
                    if (sanitized != value)
                    {
                        hasChanges = true;
                        _logger.LogInformation("Sanitized query parameter {Key} for {Path}", 
                            param.Key, context.Request.Path);
                    }
                }
            }
            
            sanitizedQuery[param.Key] = new Microsoft.Extensions.Primitives.StringValues(sanitizedValues.ToArray());
        }

        if (hasChanges)
        {
            context.Request.Query = new QueryCollection(sanitizedQuery);
        }
    }

    private static bool ContainsMaliciousContent(string input)
    {
        if (string.IsNullOrEmpty(input))
            return false;

        return MaliciousPatterns.Any(pattern => pattern.IsMatch(input));
    }

    private static string SanitizeString(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var sanitized = input;

        sanitized = sanitized.Replace("<", "&lt;")
                           .Replace(">", "&gt;")
                           .Replace("\"", "&quot;")
                           .Replace("'", "&#x27;")
                           .Replace("/", "&#x2F;");

        sanitized = Regex.Replace(sanitized, @"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]", "");

        if (sanitized.Length > 10000)
        {
            sanitized = sanitized.Substring(0, 10000);
        }

        return sanitized;
    }

    private static string SanitizeJsonElement(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var obj = new Dictionary<string, object>();
                foreach (var property in element.EnumerateObject())
                {
                    var sanitizedValue = SanitizeJsonProperty(property.Value);
                    obj[property.Name] = sanitizedValue;
                }
                return JsonSerializer.Serialize(obj);

            case JsonValueKind.Array:
                var array = new List<object>();
                foreach (var item in element.EnumerateArray())
                {
                    array.Add(SanitizeJsonProperty(item));
                }
                return JsonSerializer.Serialize(array);

            case JsonValueKind.String:
                return JsonSerializer.Serialize(SanitizeString(element.GetString() ?? ""));

            default:
                return element.GetRawText();
        }
    }

    private static object SanitizeJsonProperty(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                return SanitizeString(element.GetString() ?? "");
            case JsonValueKind.Number:
                return element.GetDecimal();
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            case JsonValueKind.Null:
                return null!;
            case JsonValueKind.Object:
                var obj = new Dictionary<string, object>();
                foreach (var property in element.EnumerateObject())
                {
                    obj[property.Name] = SanitizeJsonProperty(property.Value);
                }
                return obj;
            case JsonValueKind.Array:
                var array = new List<object>();
                foreach (var item in element.EnumerateArray())
                {
                    array.Add(SanitizeJsonProperty(item));
                }
                return array;
            default:
                return element.GetRawText();
        }
    }
}