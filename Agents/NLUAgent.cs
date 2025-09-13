using AmalaSpotLocator.Interfaces;
using AmalaSpotLocator.Models.Exceptions;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Chat;
using System.Text.Json;
using System.Text.RegularExpressions;
using AmalaSpotLocator.Models.UserModel;
using AmalaSpotLocator.Core.Applications.Interfaces.Services;
using System.Threading.Tasks;
using System;
using AmalaSpotLocator.Models;

namespace AmalaSpotLocator.Agents;

public class NLUAgent : BaseAgent, INLUAgent
{
    private readonly OpenAIClient _openAIClient;
    private readonly IConfiguration _configuration;
    private readonly IVoiceProcessingService _voiceProcessingService;
    private readonly string _model;

    public NLUAgent(
        ILogger<NLUAgent> logger, 
        IConfiguration configuration,
        IVoiceProcessingService voiceProcessingService) : base(logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _voiceProcessingService = voiceProcessingService ?? throw new ArgumentNullException(nameof(voiceProcessingService));
        
        var apiKey = _configuration["OpenAI:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("OpenAI API key is not configured");
        }
        
        _openAIClient = new OpenAIClient(apiKey);
        _model = _configuration["OpenAI:Model"] ?? "gpt-4";
    }
    
    public async Task<UserIntent> ExtractIntent(string userMessage, string? sessionId = null, Location? userLocation = null)
    {
        return await ExecuteWithErrorHandling(
            async () => await ExtractIntentInternal(userMessage, sessionId, userLocation),
            nameof(ExtractIntent),
            ex => new NLUAgentException($"Failed to extract intent from message: {ex.Message}", ex));
    }
    
    public async Task<UserIntent> ExtractIntentFromVoice(byte[] audioData, string? sessionId = null, Location? userLocation = null)
    {
        return await ExecuteWithErrorHandling(
            async () => await ExtractIntentFromVoiceInternal(audioData, sessionId, userLocation),
            nameof(ExtractIntentFromVoice),
            ex => new NLUAgentException($"Failed to extract intent from voice: {ex.Message}", ex));
    }
    
    public async Task<bool> ValidateIntent(UserIntent intent)
    {
        return await ExecuteWithErrorHandling(
            async () => await ValidateIntentInternal(intent),
            nameof(ValidateIntent),
            ex => new NLUAgentException($"Failed to validate intent: {ex.Message}", ex));
    }
    
    private async Task<UserIntent> ExtractIntentInternal(string userMessage, string? sessionId, Location? userLocation)
    {
        ValidateStringInput(userMessage, nameof(userMessage));
        
        Logger.LogInformation("Processing message with OpenAI: {Message}", userMessage);
        
        try
        {
            var systemPrompt = GetSystemPrompt();
            var userPrompt = GetUserPrompt(userMessage, userLocation);
            
            var chatCompletion = await RetryOperation(async () =>
            {
                var response = await _openAIClient.GetChatClient(_model).CompleteChatAsync(
                    new OpenAI.Chat.ChatMessage[]
                    {
                        new SystemChatMessage(systemPrompt),
                        new UserChatMessage(userPrompt)
                    });
                
                return response.Value;
            }, maxRetries: 3, delay: TimeSpan.FromSeconds(2));
            
            var responseContent = chatCompletion.Content[0].Text;
            Logger.LogDebug("OpenAI response: {Response}", responseContent);
            
            var intent = ParseOpenAIResponse(responseContent, userMessage, sessionId, userLocation);

            if (intent.Type == IntentType.Unknown)
            {
                Logger.LogWarning("OpenAI failed to extract intent, falling back to basic detection");
                intent = CreateFallbackIntent(userMessage, sessionId, userLocation);
            }
            
            return intent;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error calling OpenAI API, falling back to basic intent detection");
            return CreateFallbackIntent(userMessage, sessionId, userLocation);
        }
    }
    
    private async Task<UserIntent> ExtractIntentFromVoiceInternal(byte[] audioData, string? sessionId, Location? userLocation)
    {
        ValidateInput(audioData, nameof(audioData));
        
        if (audioData.Length == 0)
        {
            throw new NLUAgentException("Audio data cannot be empty");
        }
        
        Logger.LogInformation("Processing voice input of {Size} bytes", audioData.Length);
        
        try
        {

            var transcribedText = await _voiceProcessingService.SpeechToTextAsync(audioData, "wav");
            Logger.LogInformation("Successfully transcribed voice to text: {Text}", transcribedText);

            return await ExtractIntentInternal(transcribedText, sessionId, userLocation);
        }
        catch (VoiceProcessingException ex)
        {
            Logger.LogError(ex, "Voice processing failed");
            throw new NLUAgentException($"Failed to process voice input: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error during voice processing");
            throw new NLUAgentException($"Failed to process voice input: {ex.Message}", ex);
        }
    }
    
    private async Task<bool> ValidateIntentInternal(UserIntent intent)
    {
        ValidateInput(intent, nameof(intent));

        if (intent.Type == IntentType.Unknown)
        {
            Logger.LogWarning("Intent type is unknown for message: {Message}", intent.OriginalMessage);
            return false;
        }

        if (intent.Type == IntentType.FindNearbySpots && intent.TargetLocation == null)
        {
            Logger.LogWarning("Location required for nearby spots search but not provided");
            return false;
        }
        
        await Task.CompletedTask; // Placeholder for async validation
        return true;
    }
    
    private string DetectLanguage(string message)
    {

        var lowerMessage = message.ToLowerInvariant();

        if (lowerMessage.Contains("wetin") || lowerMessage.Contains("dey") || lowerMessage.Contains("wey"))
        {
            return "pidgin";
        }

        if (lowerMessage.Contains("bawo") || lowerMessage.Contains("nibo") || 
            lowerMessage.Contains("ni mo se") || lowerMessage.Contains("to dara"))
        {
            return "yoruba";
        }

        return "en";
    }
    
    private IntentType DetectIntentType(string message)
    {
        var lowerMessage = message.ToLowerInvariant();

        if (lowerMessage.Contains("find") || lowerMessage.Contains("near") || 
            lowerMessage.Contains("close") || lowerMessage.Contains("dey"))
        {
            return IntentType.FindNearbySpots;
        }

        if (lowerMessage.Contains("details") || lowerMessage.Contains("info") || 
            lowerMessage.Contains("about"))
        {
            return IntentType.GetSpotDetails;
        }

        if (lowerMessage.Contains("add") || lowerMessage.Contains("new spot") || 
            lowerMessage.Contains("register"))
        {
            return IntentType.AddNewSpot;
        }

        if (lowerMessage.Contains("filter"))
        {
            return IntentType.FilterSpots;
        }

        if (lowerMessage.Contains("review") || lowerMessage.Contains("rate") || 
            lowerMessage.Contains("rating"))
        {
            return IntentType.AddReview;
        }

        if (lowerMessage.Contains("direction") || lowerMessage.Contains("route") || 
            lowerMessage.Contains("how to get"))
        {
            return IntentType.GetDirections;
        }
        
        return IntentType.Unknown;
    }
    
    private void ExtractBasicEntities(string message, UserIntent intent)
    {
        var lowerMessage = message.ToLowerInvariant();

        if (lowerMessage.Contains("cheap") || lowerMessage.Contains("budget"))
        {
            intent.MaxBudget = 1000; // Budget range
        }
        else if (lowerMessage.Contains("expensive") || lowerMessage.Contains("premium"))
        {
            intent.MaxBudget = null; // No budget limit
        }

        if (lowerMessage.Contains("good") || lowerMessage.Contains("best"))
        {
            intent.MinRating = 4.0m;
        }
        else if (lowerMessage.Contains("excellent") || lowerMessage.Contains("top"))
        {
            intent.MinRating = 4.5m;
        }

        if (lowerMessage.Contains("very close") || lowerMessage.Contains("walking"))
        {
            intent.MaxDistance = 1.0; // 1km
        }
        else if (lowerMessage.Contains("nearby") || lowerMessage.Contains("close"))
        {
            intent.MaxDistance = 5.0; // 5km
        }

        if (lowerMessage.Contains("spicy"))
        {
            intent.Preferences.Add("spicy");
        }
        if (lowerMessage.Contains("vegetarian"))
        {
            intent.Preferences.Add("vegetarian");
        }
    }
    
    private string GetSystemPrompt()
    {
        return @"You are an AI assistant specialized in understanding queries about amala restaurants and food spots in Nigeria. 
Your task is to extract structured information from user messages in English, Nigerian Pidgin, or Yoruba.

You must respond with a JSON object containing the following fields:
- intentType: one of [""FindNearbySpots"", ""GetSpotDetails"", ""AddNewSpot"", ""AddReview"", ""GetDirections"", ""FilterSpots"", ""Unknown""]
- language: detected language (""en"", ""pidgin"", ""yoruba"")
- location: extracted location name or null
- maxBudget: extracted budget limit in Naira or null
- minRating: minimum rating preference (1-5) or null
- maxDistance: maximum distance in kilometers or null
- preferences: array of food preferences/requirements

Examples of queries you should understand:
- English: ""Find cheap amala spots near Ikeja""
- Pidgin: ""Where amala dey near Yaba wey cheap pass?""
- Yoruba: ""Bawo ni mo se le ri amala to dara ni Ibadan?""

Common Nigerian locations: Lagos, Abuja, Ibadan, Kano, Port Harcourt, Benin City, Kaduna, Jos, Ilorin, Aba, Onitsha, Warri, Calabar, Akure, Abeokuta, Enugu, Sokoto, Maiduguri, Zaria, Owerri, Uyo, Bauchi, Katsina, Gombe, Yola, Makurdi, Lafia, Lokoja, Asaba, Awka, Ado-Ekiti, Osogbo, Abakaliki, Jalingo, Dutse, Birnin Kebbi, Damaturu, Gusau, Minna, Yenagoa.

Budget ranges:
- ""cheap""/""budget"": under ₦1000
- ""moderate"": ₦1000-2500  
- ""expensive""/""premium"": above ₦2500

Distance indicators:
- ""very close""/""walking distance"": 1km
- ""nearby""/""close"": 5km
- ""far""/""anywhere"": no limit

Respond only with valid JSON, no additional text.";
    }
    
    private string GetUserPrompt(string userMessage, Location? userLocation)
    {
        var prompt = $"Extract intent from this message: \"{userMessage}\"";
        
        if (userLocation != null)
        {
            prompt += $"\nUser's current location: Latitude {userLocation.Latitude}, Longitude {userLocation.Longitude}";
        }
        
        return prompt;
    }
    
    private UserIntent ParseOpenAIResponse(string responseContent, string originalMessage, string? sessionId, Location? userLocation)
    {
        try
        {

            var jsonMatch = Regex.Match(responseContent, @"\{.*\}", RegexOptions.Singleline);
            var jsonContent = jsonMatch.Success ? jsonMatch.Value : responseContent;
            
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            
            var aiResponse = JsonSerializer.Deserialize<OpenAIIntentResponse>(jsonContent, options);
            
            if (aiResponse == null)
            {
                Logger.LogWarning("Failed to deserialize OpenAI response");
                return CreateFallbackIntent(originalMessage, sessionId, userLocation);
            }
            
            return new UserIntent
            {
                Type = ParseIntentType(aiResponse.IntentType),
                OriginalMessage = originalMessage,
                SessionId = sessionId,
                TargetLocation = ParseLocation(aiResponse.Location, userLocation),
                MaxBudget = aiResponse.MaxBudget,
                MinRating = aiResponse.MinRating,
                MaxDistance = aiResponse.MaxDistance,
                Preferences = aiResponse.Preferences ?? new List<string>(),
                Language = aiResponse.Language ?? DetectLanguage(originalMessage),
                ExtractedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error parsing OpenAI response: {Response}", responseContent);
            return CreateFallbackIntent(originalMessage, sessionId, userLocation);
        }
    }
    
    private UserIntent CreateFallbackIntent(string userMessage, string? sessionId, Location? userLocation)
    {
        Logger.LogInformation("Using fallback intent detection for message: {Message}", userMessage);
        
        var intent = new UserIntent
        {
            OriginalMessage = userMessage,
            SessionId = sessionId,
            TargetLocation = userLocation,
            Language = DetectLanguage(userMessage),
            Type = DetectIntentType(userMessage),
            ExtractedAt = DateTime.UtcNow
        };
        
        ExtractBasicEntities(userMessage, intent);
        return intent;
    }
    
    private IntentType ParseIntentType(string? intentTypeString)
    {
        if (string.IsNullOrWhiteSpace(intentTypeString))
            return IntentType.Unknown;
            
        return intentTypeString.ToLowerInvariant() switch
        {
            "findnearbyspots" => IntentType.FindNearbySpots,
            "getspotdetails" => IntentType.GetSpotDetails,
            "addnewspot" => IntentType.AddNewSpot,
            "addreview" => IntentType.AddReview,
            "getdirections" => IntentType.GetDirections,
            "filterspots" => IntentType.FilterSpots,
            _ => IntentType.Unknown
        };
    }
    
    private Location? ParseLocation(string? locationString, Location? userLocation)
    {
        if (string.IsNullOrWhiteSpace(locationString))
            return userLocation;

        return userLocation;
    }
    
    private class OpenAIIntentResponse
    {
        public string? IntentType { get; set; }
        public string? Language { get; set; }
        public string? Location { get; set; }
        public decimal? MaxBudget { get; set; }
        public decimal? MinRating { get; set; }
        public double? MaxDistance { get; set; }
        public List<string>? Preferences { get; set; }
    }
}