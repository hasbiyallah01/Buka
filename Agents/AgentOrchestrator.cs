using AmalaSpotLocator.Interfaces;
using AmalaSpotLocator.Models.Exceptions;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using AmalaSpotLocator.Models.UserModel;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using AmalaSpotLocator.Models;
using AmalaSpotLocator.Models.DTOs;

namespace AmalaSpotLocator.Agents;

public class AgentOrchestrator : BaseAgent, IAgentOrchestrator
{
    private readonly INLUAgent _nluAgent;
    private readonly IQueryAgent _queryAgent;
    private readonly IResponseAgent _responseAgent;

    private readonly ConcurrentDictionary<string, ConversationContext> _conversationContexts;
    private readonly TimeSpan _contextExpiryTime = TimeSpan.FromMinutes(30);

    private readonly AgentFallbackConfig _fallbackConfig;
    
    public AgentOrchestrator(
        INLUAgent nluAgent,
        IQueryAgent queryAgent,
        IResponseAgent responseAgent,
        ILogger<AgentOrchestrator> logger) : base(logger)
    {
        _nluAgent = nluAgent ?? throw new ArgumentNullException(nameof(nluAgent));
        _queryAgent = queryAgent ?? throw new ArgumentNullException(nameof(queryAgent));
        _responseAgent = responseAgent ?? throw new ArgumentNullException(nameof(responseAgent));
        
        _conversationContexts = new ConcurrentDictionary<string, ConversationContext>();
        _fallbackConfig = new AgentFallbackConfig();

        _ = Task.Run(CleanupExpiredContextsAsync);
    }
    
    public async Task<AgentResponse> ProcessUserInput(UserInput input)
    {
        return await ExecuteWithErrorHandling(
            async () => await ProcessUserInputInternal(input),
            nameof(ProcessUserInput),
            ex => new OrchestrationException($"Failed to process user input: {ex.Message}", ex));
    }
    
    public async Task<VoiceResponse> ProcessVoiceInput(VoiceInput input)
    {
        return await ExecuteWithErrorHandling(
            async () => await ProcessVoiceInputInternal(input),
            nameof(ProcessVoiceInput),
            ex => new OrchestrationException($"Failed to process voice input: {ex.Message}", ex));
    }
    
    public async Task<AgentResponse> ProcessUserIntent(UserIntent intent)
    {
        return await ExecuteWithErrorHandling(
            async () => await ProcessUserIntentInternal(intent),
            nameof(ProcessUserIntent),
            ex => new OrchestrationException($"Failed to process user intent: {ex.Message}", ex));
    }
    
    private async Task<AgentResponse> ProcessUserInputInternal(UserInput input)
    {
        ValidateInput(input, nameof(input));
        ValidateStringInput(input.Message, nameof(input.Message));
        
        try
        {

            var intent = await _nluAgent.ExtractIntent(input.Message, input.SessionId, input.UserLocation, input.Language);
            
            return await ProcessUserIntentInternal(intent);
        }
        catch (AgentException ex)
        {
            Logger.LogError(ex, "Agent exception in ProcessUserInputInternal: {ErrorMessage}", ex.Message);
            throw new OrchestrationException($"Failed to process user input: {ex.Message}", ex);
        }
    }
    
    private async Task<VoiceResponse> ProcessVoiceInputInternal(VoiceInput input)
    {
        ValidateInput(input, nameof(input));
        
        if (input.AudioData == null || input.AudioData.Length == 0)
        {
            throw new OrchestrationException("Audio data is required for voice input");
        }
        
        try
        {

            var intent = await _nluAgent.ExtractIntentFromVoice(input.AudioData, input.SessionId, input.UserLocation, input.Language);

            var textResponse = await ProcessUserIntentInternal(intent);

            var audioData = await _responseAgent.GenerateVoiceResponse(textResponse.TextResponse);
            
            return new VoiceResponse
            {
                Success = textResponse.Success,
                TextResponse = textResponse.TextResponse,
                Spots = textResponse.Spots,
                MapUrl = textResponse.MapUrl,
                ErrorMessage = textResponse.ErrorMessage,
                SessionId = textResponse.SessionId,
                AudioData = audioData,
                AudioFormat = "wav",
                GeneratedAt = DateTime.UtcNow,
                Metadata = textResponse.Metadata
            };
        }
        catch (AgentException ex)
        {
            Logger.LogError(ex, "Agent exception in ProcessVoiceInputInternal: {ErrorMessage}", ex.Message);
            throw new OrchestrationException($"Failed to process voice input: {ex.Message}", ex);
        }
    }
    
    private async Task<AgentResponse> ProcessUserIntentInternal(UserIntent intent)
    {
        ValidateInput(intent, nameof(intent));
        
        var sessionId = intent.SessionId ?? Guid.NewGuid().ToString();
        var context = GetOrCreateContext(sessionId);
        
        Logger.LogInformation("Processing intent {IntentType} for session {SessionId}. Interaction #{InteractionCount}", 
            intent.Type, sessionId, context.InteractionCount + 1);
        
        try
        {

            var isValidIntent = await _nluAgent.ValidateIntent(intent);
            if (!isValidIntent)
            {
                Logger.LogWarning("Intent validation failed for session {SessionId}. Intent: {IntentType}", 
                    sessionId, intent.Type);
                
                var clarificationResponse = await _responseAgent.GenerateClarificationResponse(intent);

                var failedResult = new QueryResult
                {
                    Success = false,
                    ErrorMessage = "Intent validation failed - clarification needed"
                };
                UpdateContext(context, intent, failedResult);
                
                return new AgentResponse
                {
                    Success = false,
                    TextResponse = clarificationResponse,
                    SessionId = sessionId,
                    ErrorMessage = "Intent validation failed - clarification needed",
                    Metadata = new Dictionary<string, object>
                    {
                        ["validation_failed"] = true,
                        ["interaction_count"] = context.InteractionCount
                    }
                };
            }

            var queryResult = await ExecuteQueryForIntent(intent);

            UpdateContext(context, intent, queryResult);

            var responseText = await _responseAgent.GenerateTextResponse(intent, queryResult);
            
            Logger.LogInformation("Successfully processed intent {IntentType} for session {SessionId}. Success: {Success}", 
                intent.Type, sessionId, queryResult.Success);
            
            return new AgentResponse
            {
                Success = queryResult.Success,
                TextResponse = responseText,
                Spots = queryResult.Spots,
                MapUrl = queryResult.MapUrl,
                SessionId = sessionId,
                ErrorMessage = queryResult.ErrorMessage,
                GeneratedAt = DateTime.UtcNow,
                Metadata = new Dictionary<string, object>(queryResult.Metadata)
                {
                    ["interaction_count"] = context.InteractionCount,
                    ["session_duration"] = DateTime.UtcNow - context.CreatedAt,
                    ["context_tracked"] = true
                }
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error processing user intent {IntentType} for session {SessionId}: {ErrorMessage}", 
                intent.Type, sessionId, ex.Message);

            var errorResult = new QueryResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
            UpdateContext(context, intent, errorResult);
            
            var errorResponse = await _responseAgent.GenerateErrorResponse(
                ex.Message, 
                intent.Language);
            
            return new AgentResponse
            {
                Success = false,
                TextResponse = errorResponse,
                SessionId = sessionId,
                ErrorMessage = ex.Message,
                GeneratedAt = DateTime.UtcNow,
                Metadata = new Dictionary<string, object>
                {
                    ["error_occurred"] = true,
                    ["interaction_count"] = context.InteractionCount,
                    ["session_duration"] = DateTime.UtcNow - context.CreatedAt
                }
            };
        }
    }
    
    private async Task<QueryResult> ExecuteQueryForIntent(UserIntent intent)
    {
        return intent.Type switch
        {
            IntentType.FindNearbySpots => await ExecuteWithFallback(() => _queryAgent.ExecuteSpotSearch(intent), "SpotSearch"),
            IntentType.GetSpotDetails => await ExecuteWithFallback(() => _queryAgent.ExecuteSpotDetails(intent), "SpotDetails"),
            IntentType.AddNewSpot => await ExecuteWithFallback(() => _queryAgent.ExecuteAddSpot(intent), "AddSpot"),
            IntentType.AddReview => await ExecuteWithFallback(() => _queryAgent.ExecuteReviewQuery(intent), "ReviewQuery"),
            IntentType.FilterSpots => await ExecuteWithFallback(() => _queryAgent.ExecuteSpotSearch(intent), "FilterSpots"),
            IntentType.GetDirections => await ExecuteWithFallback(() => _queryAgent.ExecuteGenericQuery(intent), "GetDirections"),
            _ => await ExecuteWithFallback(() => _queryAgent.ExecuteGenericQuery(intent), "GenericQuery")
        };
    }
    
    private ConversationContext GetOrCreateContext(string sessionId)
    {
        return _conversationContexts.GetOrAdd(sessionId, _ => new ConversationContext
        {
            SessionId = sessionId,
            CreatedAt = DateTime.UtcNow,
            LastAccessedAt = DateTime.UtcNow
        });
    }
    
    private void UpdateContext(ConversationContext context, UserIntent intent, QueryResult result)
    {
        context.LastAccessedAt = DateTime.UtcNow;
        context.InteractionCount++;

        context.LastIntent = intent;
        context.LastQueryResult = result;

        context.ConversationFlow.Add(new ConversationStep
        {
            Timestamp = DateTime.UtcNow,
            IntentType = intent.Type,
            Success = result.Success,
            ErrorMessage = result.ErrorMessage
        });

        if (context.ConversationFlow.Count > 10)
        {
            context.ConversationFlow.RemoveAt(0);
        }
        
        Logger.LogInformation("Updated conversation context for session {SessionId}. Interaction count: {Count}", 
            context.SessionId, context.InteractionCount);
    }
    
    private async Task<QueryResult> ExecuteWithFallback(Func<Task<QueryResult>> primaryOperation, string operationName)
    {
        try
        {
            Logger.LogInformation("Executing primary operation: {OperationName}", operationName);
            return await RetryOperation(primaryOperation, _fallbackConfig.MaxRetries, _fallbackConfig.RetryDelay);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Primary operation {OperationName} failed, attempting fallback", operationName);

            return await ExecuteFallbackStrategy(operationName, ex);
        }
    }
    
    private async Task<QueryResult> ExecuteFallbackStrategy(string operationName, Exception originalException)
    {
        try
        {

            if (operationName.Contains("Search") || operationName.Contains("SpotDetails") || operationName.Contains("FilterSpots"))
            {
                Logger.LogInformation("Using fallback strategy for search operation: {OperationName}", operationName);
                return new QueryResult
                {
                    Success = false,
                    ErrorMessage = "Search service temporarily unavailable. Please try again later.",
                    Spots = new List<SpotDto>(),
                    Metadata = new Dictionary<string, object>
                    {
                        ["fallback_used"] = true,
                        ["original_error"] = originalException.Message,
                        ["fallback_strategy"] = "empty_results"
                    }
                };
            }

            return new QueryResult
            {
                Success = false,
                ErrorMessage = "Service temporarily unavailable. Please try again later.",
                Metadata = new Dictionary<string, object>
                {
                    ["fallback_used"] = true,
                    ["original_error"] = originalException.Message,
                    ["fallback_strategy"] = "generic_error"
                }
            };
        }
        catch (Exception fallbackEx)
        {
            Logger.LogError(fallbackEx, "Fallback strategy also failed for operation: {OperationName}", operationName);
            
            return new QueryResult
            {
                Success = false,
                ErrorMessage = "Service unavailable. Please try again later.",
                Metadata = new Dictionary<string, object>
                {
                    ["fallback_used"] = true,
                    ["fallback_failed"] = true,
                    ["original_error"] = originalException.Message,
                    ["fallback_error"] = fallbackEx.Message
                }
            };
        }
    }
    
    private async Task CleanupExpiredContextsAsync()
    {
        while (true)
        {
            try
            {
                var expiredSessions = _conversationContexts
                    .Where(kvp => DateTime.UtcNow - kvp.Value.LastAccessedAt > _contextExpiryTime)
                    .Select(kvp => kvp.Key)
                    .ToList();
                
                foreach (var sessionId in expiredSessions)
                {
                    if (_conversationContexts.TryRemove(sessionId, out var context))
                    {
                        Logger.LogInformation("Cleaned up expired conversation context for session {SessionId}", sessionId);
                    }
                }

                await Task.Delay(TimeSpan.FromMinutes(5));
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error during conversation context cleanup");
                await Task.Delay(TimeSpan.FromMinutes(1)); // Shorter delay on error
            }
        }
    }
}

public class ConversationContext
{
    public string SessionId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime LastAccessedAt { get; set; }
    public int InteractionCount { get; set; }
    public UserIntent? LastIntent { get; set; }
    public QueryResult? LastQueryResult { get; set; }
    public List<ConversationStep> ConversationFlow { get; set; } = new();
    public Dictionary<string, object> UserPreferences { get; set; } = new();
}

public class ConversationStep
{
    public DateTime Timestamp { get; set; }
    public IntentType IntentType { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

public class AgentFallbackConfig
{
    public int MaxRetries { get; set; } = 3;
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);
    public bool EnableFallbackStrategies { get; set; } = true;
    public TimeSpan FallbackTimeout { get; set; } = TimeSpan.FromSeconds(30);
}