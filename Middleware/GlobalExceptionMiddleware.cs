using System.Net;
using System.Text.Json;
using AmalaSpotLocator.Models.Exceptions;

namespace AmalaSpotLocator.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IWebHostEnvironment _environment;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger,
        IWebHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred");
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        
        var response = new ErrorResponse();

        switch (exception)
        {
            case AmalaSpotException amalaEx:
                response.ErrorCode = amalaEx.ErrorCode;
                response.Message = amalaEx.Message;
                context.Response.StatusCode = GetStatusCodeForAmalaException(amalaEx);
                break;
                
            case AgentException agentEx:
                response.ErrorCode = agentEx.ErrorCode;
                response.Message = "An error occurred while processing your request";
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                break;
                
            case ArgumentException argEx:
                response.ErrorCode = "INVALID_ARGUMENT";
                response.Message = argEx.Message;
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                break;
                
            case UnauthorizedAccessException:
                response.ErrorCode = "UNAUTHORIZED";
                response.Message = "Access denied";
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                break;
                
            case TimeoutException:
                response.ErrorCode = "TIMEOUT";
                response.Message = "The request timed out";
                context.Response.StatusCode = (int)HttpStatusCode.RequestTimeout;
                break;
                
            default:
                response.ErrorCode = "INTERNAL_ERROR";
                response.Message = _environment.IsDevelopment() 
                    ? exception.Message 
                    : "An internal server error occurred";
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                break;
        }

        response.CorrelationId = context.TraceIdentifier;

        response.Timestamp = DateTime.UtcNow;

        if (_environment.IsDevelopment() && exception.StackTrace != null)
        {
            response.Details = exception.StackTrace;
        }

        var jsonResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(jsonResponse);
    }

    private static int GetStatusCodeForAmalaException(AmalaSpotException exception)
    {
        return exception.ErrorCode switch
        {
            "LOCATION_NOT_FOUND" => (int)HttpStatusCode.NotFound,
            "AI_SERVICE_ERROR" => (int)HttpStatusCode.ServiceUnavailable,
            "VALIDATION_ERROR" => (int)HttpStatusCode.BadRequest,
            "RATE_LIMIT_EXCEEDED" => (int)HttpStatusCode.TooManyRequests,
            "AUTHENTICATION_FAILED" => (int)HttpStatusCode.Unauthorized,
            "AUTHORIZATION_FAILED" => (int)HttpStatusCode.Forbidden,
            _ => (int)HttpStatusCode.InternalServerError
        };
    }
}

public class ErrorResponse
{
    public string ErrorCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? Details { get; set; }
}