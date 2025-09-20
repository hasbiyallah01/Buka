using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using AmalaSpotLocator.Interfaces;
using System.Collections.Generic;
using Swashbuckle.AspNetCore.Annotations;
using AmalaSpotLocator.Models.ChatModel;
using AmalaSpotLocator.Core.Applications.Interfaces.Services;
using AmalaSpotLocator.Models.UserModel;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace AmalaSpotLocator.Controllers;

[ApiController]
[Route("api/chat")]
[Produces("application/json")]
[Tags("Chat")]
public class ChatController : ControllerBase
{
    private readonly IAgentOrchestrator _orchestrator;
    private readonly IChatSessionService _sessionService;
    private readonly IRateLimitingService _rateLimitingService;
    private readonly IVoiceProcessingService _voiceProcessingService;
    private readonly ILogger<ChatController> _logger;
    
    public ChatController(
        IAgentOrchestrator orchestrator,
        IChatSessionService sessionService,
        IRateLimitingService rateLimitingService,
        IVoiceProcessingService voiceProcessingService,
        ILogger<ChatController> logger)
    {
        _orchestrator = orchestrator;
        _sessionService = sessionService;
        _rateLimitingService = rateLimitingService;
        _voiceProcessingService = voiceProcessingService;
        _logger = logger;
    }

    [HttpPost("text")]
    public async Task<ActionResult<ChatResponse>> ProcessTextMessage([FromBody] TextChatRequest request)
    {
        try
        {

            var clientId = GetClientIdentifier();

            if (!await _rateLimitingService.IsRequestAllowedAsync(clientId, "chat/text"))
            {
                var timeUntilReset = await _rateLimitingService.GetTimeUntilResetAsync(clientId, "chat/text");
                return StatusCode(429, new ChatResponse
                {
                    Success = false,
                    ErrorMessage = $"Rate limit exceeded. Try again in {timeUntilReset.TotalSeconds:F0} seconds.",
                    SessionId = request.SessionId ?? string.Empty
                });
            }

            var sessionId = await ValidateOrCreateSession(request.SessionId);

            var userInput = new UserInput
            {
                Message = request.Message,
                SessionId = sessionId,
                UserLocation = request.UserLocation,
                Language = request.Language
            };

            await _sessionService.AddMessageAsync(sessionId, new ChatMessage
            {
                Content = request.Message,
                IsFromUser = true,
                Type = MessageType.Text
            });

            var agentResponse = await _orchestrator.ProcessUserInput(userInput);

            await _sessionService.AddMessageAsync(sessionId, new ChatMessage
            {
                Content = agentResponse.TextResponse,
                IsFromUser = false,
                Type = MessageType.Text,
                Metadata = agentResponse.Metadata
            });

            await _sessionService.UpdateSessionActivityAsync(sessionId);

            var response = new ChatResponse
            {
                Success = agentResponse.Success,
                Reply = agentResponse.TextResponse,
                Spots = agentResponse.Spots,
                MapUrl = agentResponse.MapUrl,
                SessionId = sessionId,
                ErrorMessage = agentResponse.ErrorMessage,
                Metadata = agentResponse.Metadata
            };
            
            _logger.LogInformation("Processed text chat message for session {SessionId}", sessionId);
            
            return Ok(response);
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning("Validation error in text chat: {Message}", ex.Message);
            return BadRequest(new ChatResponse
            {
                Success = false,
                ErrorMessage = ex.Message,
                SessionId = request.SessionId ?? string.Empty
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing text chat message");
            return StatusCode(500, new ChatResponse
            {
                Success = false,
                ErrorMessage = "An error occurred while processing your message. Please try again.",
                SessionId = request.SessionId ?? string.Empty
            });
        }
    }

    [HttpPost("voice")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<VoiceChatResponse>> ProcessVoiceMessage([FromForm] VoiceChatRequest request)
    {
        try
        {

            var clientId = GetClientIdentifier();

            if (!await _rateLimitingService.IsRequestAllowedAsync(clientId, "chat/voice"))
            {
                var timeUntilReset = await _rateLimitingService.GetTimeUntilResetAsync(clientId, "chat/voice");
                return StatusCode(429, new ChatResponse
                {
                    Success = false,
                    ErrorMessage = $"Rate limit exceeded. Try again in {timeUntilReset.TotalSeconds:F0} seconds.",
                    SessionId = request.SessionId ?? string.Empty
                });
            }

            if (request.AudioFile == null || request.AudioFile.Length == 0)
            {
                return BadRequest(new ChatResponse
                {
                    Success = false,
                    ErrorMessage = "Audio file is required",
                    SessionId = request.SessionId ?? string.Empty
                });
            }

            var sessionId = await ValidateOrCreateSession(request.SessionId);

            byte[] audioData;
            using (var memoryStream = new MemoryStream())
            {
                await request.AudioFile.CopyToAsync(memoryStream);
                audioData = memoryStream.ToArray();
            }

            var validationResult = await _voiceProcessingService.ValidateAudioInputAsync(audioData, request.AudioFormat);
            if (!validationResult.IsValid)
            {
                return BadRequest(new ChatResponse
                {
                    Success = false,
                    ErrorMessage = validationResult.ErrorMessage ?? "Invalid audio format",
                    SessionId = sessionId
                });
            }

            var voiceInput = new VoiceInput
            {
                AudioData = audioData,
                AudioFormat = request.AudioFormat,
                SessionId = sessionId,
                UserLocation = request.UserLocation,
                Language = request.Language
            };

            await _sessionService.AddMessageAsync(sessionId, new ChatMessage
            {
                Content = "[Voice Message]",
                IsFromUser = true,
                Type = MessageType.Voice,
                Metadata = new Dictionary<string, object>
                {
                    { "audioFormat", request.AudioFormat },
                    { "audioSize", audioData.Length }
                }
            });

            var voiceResponse = await _orchestrator.ProcessVoiceInput(voiceInput);

            await _sessionService.AddMessageAsync(sessionId, new ChatMessage
            {
                Content = voiceResponse.TextResponse,
                IsFromUser = false,
                Type = MessageType.Voice,
                Metadata = voiceResponse.Metadata
            });

            await _sessionService.UpdateSessionActivityAsync(sessionId);

            var response = new VoiceChatResponse
            {
                Success = voiceResponse.Success,
                Reply = voiceResponse.TextResponse,
                Spots = voiceResponse.Spots,
                MapUrl = voiceResponse.MapUrl,
                SessionId = sessionId,
                ErrorMessage = voiceResponse.ErrorMessage,
                Metadata = voiceResponse.Metadata,
                AudioData = voiceResponse.AudioData,
                AudioFormat = voiceResponse.AudioFormat
            };
            
            _logger.LogInformation("Processed voice chat message for session {SessionId}", sessionId);
            
            return Ok(response);
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning("Validation error in voice chat: {Message}", ex.Message);
            return BadRequest(new ChatResponse
            {
                Success = false,
                ErrorMessage = ex.Message,
                SessionId = request.SessionId ?? string.Empty
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing voice chat message");
            return StatusCode(500, new ChatResponse
            {
                Success = false,
                ErrorMessage = "An error occurred while processing your voice message. Please try again.",
                SessionId = request.SessionId ?? string.Empty
            });
        }
    }

    [HttpGet("session/{sessionId}")]
    public async Task<ActionResult<ChatSession>> GetSession(string sessionId)
    {
        try
        {
            var session = await _sessionService.GetSessionAsync(sessionId);
            if (session == null)
            {
                return NotFound(new { Message = "Session not found" });
            }
            
            return Ok(session);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving session {SessionId}", sessionId);
            return StatusCode(500, new { Message = "An error occurred while retrieving the session" });
        }
    }

    [HttpPost("session")]
    public async Task<ActionResult> CreateSession()
    {
        try
        {
            var sessionId = await _sessionService.CreateSessionAsync();
            return Ok(new { SessionId = sessionId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating new session");
            return StatusCode(500, new { Message = "An error occurred while creating the session" });
        }
    }
    
    private async Task<string> ValidateOrCreateSession(string? sessionId)
    {
        if (string.IsNullOrEmpty(sessionId) || !await _sessionService.IsSessionValidAsync(sessionId))
        {
            return await _sessionService.CreateSessionAsync();
        }
        
        return sessionId;
    }
    
    private string GetClientIdentifier()
    {

        var userId = User?.Identity?.Name;
        if (!string.IsNullOrEmpty(userId))
        {
            return $"user:{userId}";
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return $"ip:{ipAddress}";
    }
}