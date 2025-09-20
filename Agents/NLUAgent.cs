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
    
    public async Task<UserIntent> ExtractIntent(string userMessage, string? sessionId = null, Location? userLocation = null, string? language = null)
    {
        return await ExecuteWithErrorHandling(
            async () => await ExtractIntentInternal(userMessage, sessionId, userLocation, language),
            nameof(ExtractIntent),
            ex => new NLUAgentException($"Failed to extract intent from message: {ex.Message}", ex));
    }
    
    public async Task<UserIntent> ExtractIntentFromVoice(byte[] audioData, string? sessionId = null, Location? userLocation = null, string? language = null)
    {
        return await ExecuteWithErrorHandling(
            async () => await ExtractIntentFromVoiceInternal(audioData, sessionId, userLocation, language),
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
    
    private async Task<UserIntent> ExtractIntentInternal(string userMessage, string? sessionId, Location? userLocation, string? language = null)
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
            
            var intent = ParseOpenAIResponse(responseContent, userMessage, sessionId, userLocation, language);

            if (intent.Type == IntentType.Unknown)
            {
                Logger.LogWarning("OpenAI failed to extract intent, falling back to basic detection");
                intent = CreateFallbackIntent(userMessage, sessionId, userLocation, language);
            }
            
            return intent;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error calling OpenAI API, falling back to basic intent detection");
            return CreateFallbackIntent(userMessage, sessionId, userLocation, language);
        }
    }
    
    private async Task<UserIntent> ExtractIntentFromVoiceInternal(byte[] audioData, string? sessionId, Location? userLocation, string? language = null)
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

            return await ExtractIntentInternal(transcribedText, sessionId, userLocation, language);
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
        var pidginTerms = new[] { 
            "wetin", "dey", "wey", "na", "make", "no", "go", "come", "see", "find", 
            "where", "how", "wan", "fit", "chop", "food", "place", "near", "close",
            "cheap", "pass", "beta", "fine", "good", "bad", "small", "big", "plenty"
        };
        var yorubaTerms = new[] { 
            "bawo", "nibo", "ni mo se", "to dara", "ki lo", "elo ni", "wa", "ri", 
            "fun mi", "mo fe", "mo wa", "ti o", "ni", "si", "ati", "tabi", "sugbon",
            "amala", "gbegiri", "ewedu", "obe", "ounje", "ile", "ibi", "sunmo"
        };

        var pidginCount = pidginTerms.Count(term => lowerMessage.Contains(term));
        var yorubaCount = yorubaTerms.Count(term => lowerMessage.Contains(term));
        if (pidginCount >= 2 || (pidginCount > yorubaCount && pidginCount > 0))
        {
            return "pcm";
        }
        if (yorubaCount >= 2 || (yorubaCount > pidginCount && yorubaCount > 0))
        {
            return "yo";
        }
        if (pidginTerms.Any(term => lowerMessage.Contains(term)))
        {
            return "pcm";
        }

        if (yorubaTerms.Any(term => lowerMessage.Contains(term)))
        {
            return "yo";
        }

        return "en";
    }
    
    private IntentType DetectIntentType(string message)
    {
        var lowerMessage = message.ToLowerInvariant();
        var findTerms = new[] { 
            "find", "near", "close", "nearby", "around", "search", "look for",
            "dey", "where", "wey dey", "find me", "show me", "wetin dey",
            "wa", "ri", "nibo", "sunmo", "ni agbegbe", "to wa nitosi"
        };
        
        if (findTerms.Any(term => lowerMessage.Contains(term)))
        {
            return IntentType.FindNearbySpots;
        }
        var detailTerms = new[] { 
            "details", "info", "about", "tell me", "information",
            "wetin be", "how e be", "talk about",
            "ki lo", "so fun mi", "elo ni"
        };
        
        if (detailTerms.Any(term => lowerMessage.Contains(term)))
        {
            return IntentType.GetSpotDetails;
        }
        var addTerms = new[] { 
            "add", "new spot", "register", "create", "submit",
            "make new", "put new", "add new place",
            "fi kun", "se tuntun", "gbe sinu"
        };
        
        if (addTerms.Any(term => lowerMessage.Contains(term)))
        {
            return IntentType.AddNewSpot;
        }
        var reviewTerms = new[] { 
            "review", "rate", "rating", "comment", "feedback",
            "wetin you think", "how e be", "talk about am",
            "ero mi", "ki lo ro", "so ero re"
        };
        
        if (reviewTerms.Any(term => lowerMessage.Contains(term)))
        {
            return IntentType.AddReview;
        }
        var directionTerms = new[] { 
            "direction", "route", "how to get", "way to", "path to",
            "how I go reach", "wetin be the way", "which way",
            "ona", "bi mo se le de", "ibo ni mo le gba"
        };
        
        if (directionTerms.Any(term => lowerMessage.Contains(term)))
        {
            return IntentType.GetDirections;
        }
        
        return IntentType.Unknown;
    }
    
    private void ExtractBasicEntities(string message, UserIntent intent)
    {
        var lowerMessage = message.ToLowerInvariant();
        var cheapTerms = new[] { 
            "cheap", "budget", "affordable", "low price", "not expensive",
            "cheap pass", "no cost much", "small money", "budget friendly",
            "owo kekere", "ko gbowo", "owo die", "ti ko gbowo"
        };
        
        var expensiveTerms = new[] { 
            "expensive", "premium", "high end", "costly", "pricey",
            "cost well well", "big money", "no cheap",
            "owo nla", "gbowo", "owo pupọ"
        };

        if (cheapTerms.Any(term => lowerMessage.Contains(term)))
        {
            intent.MaxBudget = 1000; // Budget range
        }
        else if (expensiveTerms.Any(term => lowerMessage.Contains(term)))
        {
            intent.MaxBudget = null; // No budget limit
        }
        var goodTerms = new[] { 
            "good", "best", "quality", "nice", "fine",
            "beta", "good well well", "sweet", "fine pass",
            "dara", "to dara", "ti o dara", "to ga ju"
        };
        
        var excellentTerms = new[] { 
            "excellent", "top", "amazing", "perfect", "outstanding",
            "best pass", "number one", "top notch",
            "to ga ju", "ti o tayọ", "ọga"
        };

        if (excellentTerms.Any(term => lowerMessage.Contains(term)))
        {
            intent.MinRating = 4.5m;
        }
        else if (goodTerms.Any(term => lowerMessage.Contains(term)))
        {
            intent.MinRating = 4.0m;
        }
        var veryCloseTerms = new[] { 
            "very close", "walking", "walking distance", "very near",
            "close well well", "I fit walk", "no far",
            "sunmo pupọ", "to sunmo", "irin ajo kekere"
        };
        
        var nearbyTerms = new[] { 
            "nearby", "close", "near", "around here", "this area",
            "for here", "around", "close by",
            "nitosi", "ni agbegbe", "sunmo"
        };

        if (veryCloseTerms.Any(term => lowerMessage.Contains(term)))
        {
            intent.MaxDistance = 1.0; // 1km
        }
        else if (nearbyTerms.Any(term => lowerMessage.Contains(term)))
        {
            intent.MaxDistance = 5.0; // 5km
        }
        var spicyTerms = new[] { 
            "spicy", "hot", "pepper", "peppery",
            "pepper well well", "hot pepper", "ata",
            "ata pupọ", "gbona", "ata gbigbona"
        };
        
        var amalaTerms = new[] { 
            "amala", "gbegiri", "ewedu", "abula", "stew",
            "amala funfun", "amala dudu", "obe"
        };

        if (spicyTerms.Any(term => lowerMessage.Contains(term)))
        {
            intent.Preferences.Add("spicy");
        }
        foreach (var term in amalaTerms)
        {
            if (lowerMessage.Contains(term))
            {
                intent.Preferences.Add(term);
            }
        }
        if (amalaTerms.Any(term => lowerMessage.Contains(term)) && !intent.Preferences.Contains("amala"))
        {
            intent.Preferences.Add("amala");
        }
    }
    
    private string GetSystemPrompt()
    {
        return @"You are an AI assistant specialized in understanding queries about amala restaurants and food spots in Nigeria. 
Your task is to extract structured information from user messages in English, Nigerian Pidgin, or Yoruba.

You must respond with a JSON object containing the following fields:
- intentType: one of [""FindNearbySpots"", ""GetSpotDetails"", ""AddNewSpot"", ""AddReview"", ""GetDirections"", ""FilterSpots"", ""CheckBusyness"", ""SubmitCheckIn"", ""ViewHeatmap"", ""BusinessOpportunities"", ""UnderservedAreas"", ""Unknown""]
- language: detected language (""en"", ""pcm"", ""yo"")
- location: extracted location name or null
- maxBudget: extracted budget limit in Naira or null
- minRating: minimum rating preference (1-5) or null
- maxDistance: maximum distance in kilometers or null
- preferences: array of food preferences/requirements

Examples of queries you should understand:

ENGLISH:
- ""Find cheap amala spots near Ikeja""
- ""Show me good amala restaurants in Lagos""
- ""Where can I get the best gbegiri and ewedu?""
- ""Is Mama Put busy right now?"" (CheckBusyness)
- ""How crowded is this restaurant?"" (CheckBusyness)
- ""I'm here at the restaurant, it's very busy"" (SubmitCheckIn)
- ""Show me amala heatmap for Lagos"" (ViewHeatmap)
- ""Where should I open a new amala restaurant?"" (BusinessOpportunities)
- ""Which areas need more amala spots?"" (UnderservedAreas)

NIGERIAN PIDGIN:
- ""Where amala dey near Yaba wey cheap pass?""
- ""Abeg find me good buka for Surulere""
- ""Wetin be the best amala joint for Victoria Island?""
- ""I wan chop amala, where I fit find am for Ikeja?""
- ""Show me mama put wey dey sell good amala""
- ""This buka dey full o, people plenty well well"" (SubmitCheckIn)
- ""Mama Put get queue? E dey busy?"" (CheckBusyness)
- ""Where I fit open new amala business?"" (BusinessOpportunities)
- ""Show me where amala no dey for Lagos"" (UnderservedAreas)

YORUBA:
- ""Bawo ni mo se le ri amala to dara ni Ibadan?""
- ""Nibo ni mo le ra amala to dun ni Lagos?""
- ""Mo fe amala ati gbegiri, nibo ni mo le ri won?""
- ""Ibo lo dara ju fun amala ni agbegbe yi?""
- ""Wa fun mi ile ounje ti won n ta amala to dara""
- ""Ile ounje yi kun bi? Eniyan wa pupo?"" (CheckBusyness)
- ""Mo wa ni ile ounje yi, o kun pupo"" (SubmitCheckIn)
- ""Nibo ni mo le gbe ile ounje amala sile?"" (BusinessOpportunities)

Common Nigerian locations: Lagos, Abuja, Ibadan, Kano, Port Harcourt, Benin City, Kaduna, Jos, Ilorin, Aba, Onitsha, Warri, Calabar, Akure, Abeokuta, Enugu, Sokoto, Maiduguri, Zaria, Owerri, Uyo, Bauchi, Katsina, Gombe, Yola, Makurdi, Lafia, Lokoja, Asaba, Awka, Ado-Ekiti, Osogbo, Abakaliki, Jalingo, Dutse, Birnin Kebbi, Damaturu, Gusau, Minna, Yenagoa.

Budget indicators:
ENGLISH: ""cheap"", ""budget"", ""affordable"" = under ₦1000 | ""expensive"", ""premium"" = above ₦2500
PIDGIN: ""cheap pass"", ""no cost much"", ""small money"" = under ₦1000 | ""cost well well"", ""big money"" = above ₦2500  
YORUBA: ""owo kekere"", ""ko gbowo"" = under ₦1000 | ""owo nla"", ""gbowo pupọ"" = above ₦2500

Distance indicators:
ENGLISH: ""very close"", ""walking distance"" = 1km | ""nearby"", ""close"" = 5km
PIDGIN: ""close well well"", ""I fit walk"" = 1km | ""for here"", ""around"" = 5km
YORUBA: ""sunmo pupọ"", ""irin ajo kekere"" = 1km | ""nitosi"", ""ni agbegbe"" = 5km

Quality indicators:
ENGLISH: ""good"", ""best"" = 4+ rating | ""excellent"", ""top"" = 4.5+ rating
PIDGIN: ""beta"", ""sweet"" = 4+ rating | ""best pass"", ""number one"" = 4.5+ rating
YORUBA: ""dara"", ""to dara"" = 4+ rating | ""to ga ju"", ""ọga"" = 4.5+ rating

Food terms to recognize: amala, gbegiri, ewedu, abula, obe, stew, soup, buka, mama put, local food, traditional food

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
    
    private UserIntent ParseOpenAIResponse(string responseContent, string originalMessage, string? sessionId, Location? userLocation, string? providedLanguage = null)
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
                return CreateFallbackIntent(originalMessage, sessionId, userLocation, providedLanguage);
            }
            var finalLanguage = providedLanguage ?? aiResponse.Language ?? DetectLanguage(originalMessage);
            
            Logger.LogInformation("Language resolution: Provided={ProvidedLanguage}, AI={AILanguage}, Detected={DetectedLanguage}, Final={FinalLanguage}", 
                providedLanguage, aiResponse.Language, DetectLanguage(originalMessage), finalLanguage);
            
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
                Language = finalLanguage,
                ExtractedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error parsing OpenAI response: {Response}", responseContent);
            return CreateFallbackIntent(originalMessage, sessionId, userLocation, providedLanguage);
        }
    }
    
    private UserIntent CreateFallbackIntent(string userMessage, string? sessionId, Location? userLocation, string? providedLanguage = null)
    {
        Logger.LogInformation("Using fallback intent detection for message: {Message}", userMessage);
        var finalLanguage = providedLanguage ?? DetectLanguage(userMessage);
        
        Logger.LogInformation("Fallback language resolution: Provided={ProvidedLanguage}, Detected={DetectedLanguage}, Final={FinalLanguage}", 
            providedLanguage, DetectLanguage(userMessage), finalLanguage);
        
        var intent = new UserIntent
        {
            OriginalMessage = userMessage,
            SessionId = sessionId,
            TargetLocation = userLocation,
            Language = finalLanguage,
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