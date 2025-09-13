using AmalaSpotLocator.Models.Exceptions;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace AmalaSpotLocator.Agents;

public abstract class BaseAgent
{
    protected readonly ILogger Logger;
    
    protected BaseAgent(ILogger logger)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    protected async Task<T> ExecuteWithErrorHandling<T>(Func<Task<T>> operation, string operationName,Func<Exception, AgentException>? exceptionMapper = null)
    {
        try
        {
            Logger.LogInformation("Starting {OperationName}", operationName);
            var result = await operation();
            Logger.LogInformation("Completed {OperationName} successfully", operationName);
            return result;
        }
        catch (AgentException)
        {
            Logger.LogError("Agent exception occurred in {OperationName}", operationName);
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error in {OperationName}", operationName);
            
            if (exceptionMapper != null)
            {
                throw exceptionMapper(ex);
            }
            
            throw new AgentException($"Unexpected error in {operationName}: {ex.Message}", "AGENT_ERROR", ex);
        }
    }
    
    protected void ValidateInput<T>(T input, string parameterName) where T : class
    {
        if (input == null)
        {
            throw new ArgumentNullException(parameterName);
        }
    }
    
    protected void ValidateStringInput(string input, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new ArgumentException($"{parameterName} cannot be null or empty", parameterName);
        }
    }
    
    protected async Task<T> RetryOperation<T>(
        Func<Task<T>> operation,
        int maxRetries = 3,
        TimeSpan? delay = null)
    {
        var actualDelay = delay ?? TimeSpan.FromSeconds(1);
        Exception? lastException = null;
        
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex)
            {
                lastException = ex;
                Logger.LogWarning(ex, "Attempt {Attempt} of {MaxRetries} failed", attempt, maxRetries);
                
                if (attempt < maxRetries)
                {
                    await Task.Delay(actualDelay * attempt);
                }
            }
        }
        
        throw lastException ?? new AgentException("Operation failed after retries", "RETRY_EXHAUSTED");
    }
}