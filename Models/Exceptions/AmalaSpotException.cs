namespace AmalaSpotLocator.Models.Exceptions;

public class AmalaSpotException : Exception
{
    public string ErrorCode { get; set; }
    
    public AmalaSpotException(string message, string errorCode) : base(message)
    {
        ErrorCode = errorCode;
    }
    
    public AmalaSpotException(string message, string errorCode, Exception innerException) 
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}

public class LocationNotFoundException : AmalaSpotException
{
    public LocationNotFoundException(string location) 
        : base($"Location '{location}' not found", "LOCATION_NOT_FOUND") { }
}

public class AIServiceException : AmalaSpotException
{
    public AIServiceException(string message) 
        : base(message, "AI_SERVICE_ERROR") { }
        
    public AIServiceException(string message, Exception innerException) 
        : base(message, "AI_SERVICE_ERROR", innerException) { }
}
public 
class ValidationException : AmalaSpotException
{
    public ValidationException(string message) 
        : base(message, "VALIDATION_ERROR") { }
        
    public ValidationException(string message, Exception innerException) 
        : base(message, "VALIDATION_ERROR", innerException) { }
}

public class RateLimitExceededException : AmalaSpotException
{
    public TimeSpan RetryAfter { get; }
    
    public RateLimitExceededException(TimeSpan retryAfter) 
        : base("Rate limit exceeded", "RATE_LIMIT_EXCEEDED") 
    {
        RetryAfter = retryAfter;
    }
}

public class SecurityException : AmalaSpotException
{
    public SecurityException(string message) 
        : base(message, "SECURITY_ERROR") { }
        
    public SecurityException(string message, Exception innerException) 
        : base(message, "SECURITY_ERROR", innerException) { }
}

public class AuthenticationException : AmalaSpotException
{
    public AuthenticationException(string message) 
        : base(message, "AUTHENTICATION_FAILED") { }
        
    public AuthenticationException(string message, Exception innerException) 
        : base(message, "AUTHENTICATION_FAILED", innerException) { }
}

public class AuthorizationException : AmalaSpotException
{
    public AuthorizationException(string message) 
        : base(message, "AUTHORIZATION_FAILED") { }
        
    public AuthorizationException(string message, Exception innerException) 
        : base(message, "AUTHORIZATION_FAILED", innerException) { }
}