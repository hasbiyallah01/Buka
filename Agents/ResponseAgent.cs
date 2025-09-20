using AmalaSpotLocator.Interfaces;
using AmalaSpotLocator.Models.Exceptions;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using AmalaSpotLocator.Models.UserModel;
using AmalaSpotLocator.Core.Applications.Interfaces.Services;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using AmalaSpotLocator.Models;
using AmalaSpotLocator.Models.DTOs;

namespace AmalaSpotLocator.Agents;

public class ResponseAgent : BaseAgent, IResponseAgent
{
    private readonly Dictionary<string, CulturalResponseTemplates> _culturalTemplates;
    private readonly IVoiceProcessingService _voiceProcessingService;
    
    public ResponseAgent(
        ILogger<ResponseAgent> logger,
        IVoiceProcessingService voiceProcessingService) : base(logger)
    {
        _culturalTemplates = InitializeCulturalTemplates();
        _voiceProcessingService = voiceProcessingService ?? throw new ArgumentNullException(nameof(voiceProcessingService));
    }
    
    public async Task<string> GenerateTextResponse(UserIntent intent, QueryResult result)
    {
        return await ExecuteWithErrorHandling(
            async () => await GenerateTextResponseInternal(intent, result),
            nameof(GenerateTextResponse),
            ex => new ResponseAgentException($"Failed to generate text response: {ex.Message}", ex));
    }
    
    public async Task<byte[]> GenerateVoiceResponse(string textResponse)
    {
        return await ExecuteWithErrorHandling(
            async () => await GenerateVoiceResponseInternal(textResponse),
            nameof(GenerateVoiceResponse),
            ex => new ResponseAgentException($"Failed to generate voice response: {ex.Message}", ex));
    }
    
    public async Task<string> GenerateErrorResponse(string errorMessage, string language = "en")
    {
        return await ExecuteWithErrorHandling(
            async () => await GenerateErrorResponseInternal(errorMessage, language),
            nameof(GenerateErrorResponse),
            ex => new ResponseAgentException($"Failed to generate error response: {ex.Message}", ex));
    }
    
    public async Task<string> GenerateClarificationResponse(UserIntent intent)
    {
        return await ExecuteWithErrorHandling(
            async () => await GenerateClarificationResponseInternal(intent),
            nameof(GenerateClarificationResponse),
            ex => new ResponseAgentException($"Failed to generate clarification response: {ex.Message}", ex));
    }
    
    private async Task<string> GenerateTextResponseInternal(UserIntent intent, QueryResult result)
    {
        ValidateInput(intent, nameof(intent));
        ValidateInput(result, nameof(result));
        
        if (!result.Success)
        {
            return await GenerateErrorResponseInternal(
                result.ErrorMessage ?? "An error occurred while processing your request", 
                intent.Language);
        }
        
        return intent.Type switch
        {
            IntentType.FindNearbySpots => await GenerateSpotSearchResponse(intent, result),
            IntentType.GetSpotDetails => await GenerateSpotDetailsResponse(intent, result),
            IntentType.FilterSpots => await GenerateSpotSearchResponse(intent, result),
            IntentType.AddNewSpot => await GenerateAddSpotResponse(intent, result),
            IntentType.AddReview => await GenerateReviewResponse(intent, result),
            IntentType.GetDirections => await GenerateDirectionsResponse(intent, result),
            _ => await GenerateGenericResponse(intent, result)
        };
    }
    
    private async Task<string> GenerateSpotSearchResponse(UserIntent intent, QueryResult result)
    {
        var response = new StringBuilder();
        
        if (!result.Spots.Any())
        {
            response.Append(GetLocalizedMessage("no_spots_found", intent.Language));
            if (result.Metadata.ContainsKey("searchRadius"))
            {
                var radius = result.Metadata["searchRadius"];
                response.Append($" {GetLocalizedMessage("try_expanding_search", intent.Language)} {radius}km.");
            }
        }
        else
        {
            var count = result.Spots.Count;
            var transition = GetRandomFromArray(_culturalTemplates[intent.Language].Transitions);
            response.AppendLine($"{transition} {GetLocalizedMessage("found_spots", intent.Language, count)}");
            response.AppendLine();

            Location? userLocation = null;
            if (result.Metadata.ContainsKey("searchLocation") && 
                result.Metadata["searchLocation"] is Location searchLoc)
            {
                userLocation = searchLoc;
            }

            var topSpots = result.Spots.Take(5);
            foreach (var spot in topSpots)
            {
                var formattedSpot = FormatSpotWithCulturalFlair(spot, intent.Language, userLocation);
                response.AppendLine(formattedSpot);
                response.AppendLine(); // Add spacing between spots
            }
            
            if (result.Spots.Count > 5)
            {
                response.AppendLine(GetLocalizedMessage("more_spots_available", intent.Language, result.Spots.Count - 5));
            }
        }
        
        await Task.CompletedTask;
        var baseResponse = response.ToString();
        return PersonalizeResponse(baseResponse, intent, result);
    }
    
    private async Task<string> GenerateSpotDetailsResponse(UserIntent intent, QueryResult result)
    {
        if (result.SingleSpot == null)
        {
            return GetLocalizedMessage("spot_not_found", intent.Language);
        }
        
        var spot = result.SingleSpot;

        Location? userLocation = null;
        if (result.Metadata.ContainsKey("userLocation") && 
            result.Metadata["userLocation"] is Location loc)
        {
            userLocation = loc;
        }

        var formattedDetails = FormatSpotWithCulturalFlair(spot, intent.Language, userLocation);
        var response = new StringBuilder(formattedDetails);

        if (!string.IsNullOrEmpty(spot.PhoneNumber))
        {
            var phoneText = intent.Language switch
            {
                "pidgin" => $"📞 You fit call dem for: {spot.PhoneNumber}",
                "yoruba" => $"📞 O le pe won ni: {spot.PhoneNumber}",
                _ => $"📞 Contact: {spot.PhoneNumber}"
            };
            response.AppendLine(phoneText);
        }
        
        if (spot.Specialties.Any())
        {
            var specialtyText = intent.Language switch
            {
                "pidgin" => $"🍽️ Wetin dem sabi cook pass: {string.Join(", ", spot.Specialties)}",
                "yoruba" => $"🍽️ Ohun ti won mo daradara: {string.Join(", ", spot.Specialties)}",
                _ => $"🍽️ Specialties: {string.Join(", ", spot.Specialties)}"
            };
            response.AppendLine(specialtyText);
        }
        
        if (!string.IsNullOrEmpty(spot.Description))
        {
            var descText = intent.Language switch
            {
                "pidgin" => $"📝 About dis place: {spot.Description}",
                "yoruba" => $"📝 Nipa ibi yii: {spot.Description}",
                _ => $"📝 About: {spot.Description}"
            };
            response.AppendLine(descText);
        }
        
        await Task.CompletedTask;
        var baseResponse = response.ToString();
        return PersonalizeResponse(baseResponse, intent, result);
    }
    
    private async Task<string> GenerateAddSpotResponse(UserIntent intent, QueryResult result)
    {
        return GetLocalizedMessage("add_spot_success", intent.Language);
    }
    
    private async Task<string> GenerateReviewResponse(UserIntent intent, QueryResult result)
    {
        return GetLocalizedMessage("review_success", intent.Language);
    }
    
    private async Task<string> GenerateDirectionsResponse(UserIntent intent, QueryResult result)
    {
        return GetLocalizedMessage("directions_not_implemented", intent.Language);
    }
    
    private async Task<string> GenerateGenericResponse(UserIntent intent, QueryResult result)
    {
        return GetLocalizedMessage("generic_response", intent.Language);
    }
    
    private async Task<byte[]> GenerateVoiceResponseInternal(string textResponse)
    {
        ValidateStringInput(textResponse, nameof(textResponse));
        
        Logger.LogInformation("Generating voice response for text of length: {Length}", textResponse.Length);
        
        try
        {

            var audioData = await _voiceProcessingService.TextToSpeechAsync(textResponse, "en");
            Logger.LogInformation("Successfully generated voice response: {Size} bytes", audioData.Length);
            
            return audioData;
        }
        catch (VoiceProcessingException ex)
        {
            Logger.LogError(ex, "Voice processing failed for text-to-speech");
            throw new ResponseAgentException($"Failed to generate voice response: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error during voice response generation");
            throw new ResponseAgentException($"Failed to generate voice response: {ex.Message}", ex);
        }
    }
    
    private async Task<string> GenerateErrorResponseInternal(string errorMessage, string language)
    {
        ValidateStringInput(errorMessage, nameof(errorMessage));
        
        var localizedPrefix = GetLocalizedMessage("error_prefix", language);
        
        await Task.CompletedTask;
        return $"{localizedPrefix} {errorMessage}";
    }
    
    private async Task<string> GenerateClarificationResponseInternal(UserIntent intent)
    {
        ValidateInput(intent, nameof(intent));
        
        var response = intent.Type switch
        {
            IntentType.FindNearbySpots when intent.TargetLocation == null => 
                GetLocalizedMessage("need_location", intent.Language),
            IntentType.Unknown => 
                GetLocalizedMessage("clarify_request", intent.Language),
            _ => 
                GetLocalizedMessage("need_more_info", intent.Language)
        };
        
        await Task.CompletedTask;
        return response;
    }
    
    private string GetLocalizedMessage(string key, string language, params object[] args)
    {

        var messages = language switch
        {
            "pcm" => GetPidginMessages(),
            "yo" => GetYorubaMessages(),
            _ => GetEnglishMessages()
        };
        
        if (messages.TryGetValue(key, out var template))
        {
            return args.Length > 0 ? string.Format(template, args) : template;
        }
        
        return messages.GetValueOrDefault("default", "I understand your request.");
    }
    
    private Dictionary<string, string> GetEnglishMessages()
    {
        return new Dictionary<string, string>
        {
            ["no_spots_found"] = "I couldn't find any amala spots in your area.",
            ["try_expanding_search"] = "Try expanding your search radius beyond",
            ["found_spots"] = "I found {0} amala spots for you:",
            ["more_spots_available"] = "...and {0} more spots available.",
            ["spot_not_found"] = "Sorry, I couldn't find details for that spot.",
            ["add_spot_success"] = "Great! I've added the new amala spot to our database.",
            ["review_success"] = "Thank you for your review! It helps others find great amala spots.",
            ["directions_not_implemented"] = "Directions feature is coming soon!",
            ["generic_response"] = "I've processed your request.",
            ["error_prefix"] = "Sorry, there was an issue:",
            ["need_location"] = "I need to know your location to find nearby amala spots. Can you share your location or tell me the area you're interested in?",
            ["clarify_request"] = "I'm not sure what you're looking for. Could you please be more specific? For example, you can ask me to find amala spots near you or get details about a specific restaurant.",
            ["need_more_info"] = "I need a bit more information to help you better.",
            ["default"] = "I understand your request."
        };
    }
    
    private Dictionary<string, string> GetPidginMessages()
    {
        return new Dictionary<string, string>
        {
            ["no_spots_found"] = "I no see any amala spot for your area o.",
            ["try_expanding_search"] = "Try make you expand your search pass",
            ["found_spots"] = "I find {0} amala spots for you:",
            ["more_spots_available"] = "...and {0} more spots dey available.",
            ["spot_not_found"] = "Sorry, I no fit find details for that spot.",
            ["add_spot_success"] = "Nice one! I don add the new amala spot for our database.",
            ["review_success"] = "Thank you for your review! E go help other people find good amala spots.",
            ["directions_not_implemented"] = "Directions feature dey come soon!",
            ["generic_response"] = "I don process your request.",
            ["error_prefix"] = "Sorry, wahala dey:",
            ["need_location"] = "I need to know where you dey so I fit find amala spots near you. You fit share your location or tell me the area wey you want?",
            ["clarify_request"] = "I no too understand wetin you dey find. You fit talk am more clear? Like you fit ask me to find amala spots near you or get details about one particular restaurant.",
            ["need_more_info"] = "I need small more information to help you better.",
            ["default"] = "I understand your request."
        };
    }
    
    private Dictionary<string, string> GetYorubaMessages()
    {
        return new Dictionary<string, string>
        {
            ["no_spots_found"] = "Mi o ri amala spot kankan ni agbegbe yin.",
            ["try_expanding_search"] = "E gbiyanju lati fa iwadi yin jade ju",
            ["found_spots"] = "Mo ri amala spots {0} fun yin:",
            ["more_spots_available"] = "...ati awon spots {0} miiran lo wa.",
            ["spot_not_found"] = "Ma binu, mi o le ri alaye fun spot yen.",
            ["add_spot_success"] = "O dara! Mo ti fi amala spot tuntun naa si database wa.",
            ["review_success"] = "E se fun review yin! Yoo ran awon miiran lowo lati ri amala spots to dara.",
            ["directions_not_implemented"] = "Directions feature n bo laipe!",
            ["generic_response"] = "Mo ti se request yin.",
            ["error_prefix"] = "Ma binu, isoro kan wa:",
            ["need_location"] = "Mo nilo lati mo ibi ti e wa ki n le wa amala spots ti o sunmọ yin. E le pin location yin tabi so agbegbe ti e fẹ?",
            ["clarify_request"] = "Mi o loye ohun ti e n wa. E le so ni kedere si? Fun apẹẹrẹ, e le beere ki n wa amala spots nitosi yin tabi ki n gba alaye nipa ile ounjẹ kan pato.",
            ["need_more_info"] = "Mo nilo alaye diẹ sii lati ran yin lowo daradara.",
            ["default"] = "Mo loye request yin."
        };
    }
    
    private string GetPriceRangeText(PriceRange priceRange, string language)
    {
        return language switch
        {
            "pcm" => priceRange switch
            {
                PriceRange.Budget => "Cheap (Under ₦1000)",
                PriceRange.Moderate => "Moderate (₦1000 - ₦2500)",
                PriceRange.Expensive => "Expensive (Above ₦2500)",
                _ => "Price no dey clear"
            },
            "yo" => priceRange switch
            {
                PriceRange.Budget => "Poku (Kere ju ₦1000)",
                PriceRange.Moderate => "Iwontunwonsi (₦1000 - ₦2500)",
                PriceRange.Expensive => "Gbowolori (Ju ₦2500 lo)",
                _ => "Idiyele ko han"
            },
            _ => priceRange switch
            {
                PriceRange.Budget => "Budget (Under ₦1000)",
                PriceRange.Moderate => "Moderate (₦1000 - ₦2500)",
                PriceRange.Expensive => "Premium (Above ₦2500)",
                _ => "Price range unclear"
            }
        };
    }
    
    private double CalculateDistance(Location loc1, Location loc2)
    {

        const double R = 6371; // Earth's radius in kilometers
        
        var lat1Rad = loc1.Latitude * Math.PI / 180;
        var lat2Rad = loc2.Latitude * Math.PI / 180;
        var deltaLatRad = (loc2.Latitude - loc1.Latitude) * Math.PI / 180;
        var deltaLonRad = (loc2.Longitude - loc1.Longitude) * Math.PI / 180;
        
        var a = Math.Sin(deltaLatRad / 2) * Math.Sin(deltaLatRad / 2) +
                Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                Math.Sin(deltaLonRad / 2) * Math.Sin(deltaLonRad / 2);
        
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        
        return R * c;
    }
    
    private Dictionary<string, CulturalResponseTemplates> InitializeCulturalTemplates()
    {
        return new Dictionary<string, CulturalResponseTemplates>
        {
            ["en"] = new CulturalResponseTemplates
            {
                Greetings = new[] { "Hello!", "Hi there!", "Good day!" },
                Expressions = new[] { "Great!", "Awesome!", "Perfect!", "Excellent!" },
                Encouragements = new[] { "You're welcome!", "Happy to help!", "Enjoy your meal!" },
                Transitions = new[] { "Here's what I found:", "Let me show you:", "Check this out:" }
            },
            ["pcm"] = new CulturalResponseTemplates
            {
                Greetings = new[] { "How far!", "Wetin dey happen!", "How you dey!" },
                Expressions = new[] { "E choke!", "Na so!", "Correct!", "Sharp sharp!" },
                Encouragements = new[] { "You welcome o!", "I dey for you!", "Chop belleful!" },
                Transitions = new[] { "See wetin I find for you:", "Make I show you:", "Check am:" }
            },
            ["yo"] = new CulturalResponseTemplates
            {
                Greetings = new[] { "Bawo!", "Pele o!", "Eku aaro!" },
                Expressions = new[] { "O dara!", "Alafia!", "Gan-an!", "Bẹẹni!" },
                Encouragements = new[] { "O ku ise!", "Mo dupe!", "Jeun daradara!" },
                Transitions = new[] { "Eyi ni mo ri fun yin:", "Jẹ ki n fi han yin:", "Wo eyi:" }
            }
        };
    }
    
    private string PersonalizeResponse(string baseResponse, UserIntent intent, QueryResult result)
    {
        var personalizedResponse = new StringBuilder(baseResponse);

        var greeting = GetContextualGreeting(intent.Language);
        if (!string.IsNullOrEmpty(greeting))
        {
            personalizedResponse.Insert(0, $"{greeting} ");
        }

        if (result.Success && result.Spots.Any())
        {
            var templates = _culturalTemplates.GetValueOrDefault(intent.Language, _culturalTemplates["en"]);
            var encouragement = GetRandomFromArray(templates.Encouragements);
            personalizedResponse.AppendLine($"\n{encouragement}");
        }

        if (result.Spots.Count > 3)
        {
            var templates = _culturalTemplates.GetValueOrDefault(intent.Language, _culturalTemplates["en"]);
            var expression = GetRandomFromArray(templates.Expressions);
            var foundIndex = baseResponse.IndexOf("I found");
            if (foundIndex >= 0)
            {
                personalizedResponse.Insert(foundIndex + "I found".Length, $" {expression}");
            }
        }
        
        return personalizedResponse.ToString();
    }
    
    private string GetContextualGreeting(string language)
    {
        var hour = DateTime.Now.Hour;
        var templates = _culturalTemplates.GetValueOrDefault(language, _culturalTemplates["en"]);
        
        return language switch
        {
            "pcm" => hour switch
            {
                >= 5 and < 12 => "Good morning o!",
                >= 12 and < 17 => "Good afternoon!",
                >= 17 and < 21 => "Good evening!",
                _ => "How you dey!"
            },
            "yo" => hour switch
            {
                >= 5 and < 12 => "Eku aaro!",
                >= 12 and < 17 => "Eku osan!",
                >= 17 and < 21 => "Eku irole!",
                _ => "Bawo!"
            },
            _ => hour switch
            {
                >= 5 and < 12 => "Good morning!",
                >= 12 and < 17 => "Good afternoon!",
                >= 17 and < 21 => "Good evening!",
                _ => "Hello!"
            }
        };
    }
    
    private string GetRandomFromArray(string[] array)
    {
        if (array.Length == 0) return string.Empty;
        var random = new Random();
        return array[random.Next(array.Length)];
    }
    
    private string FormatSpotWithCulturalFlair(SpotDto spot, string language, Location? userLocation = null)
    {
        var response = new StringBuilder();

        var intro = language switch
        {
            "pcm" => "🏪 Na dis place be:",
            "yo" => "🏪 Ibi yii ni:",
            _ => "🏪 Here's a great spot:"
        };
        
        response.AppendLine($"{intro} {spot.Name}");
        response.AppendLine($"📍 {spot.Address}");

        var ratingText = GetCulturalRatingDescription(spot.AverageRating, language);
        response.AppendLine($"⭐ {spot.AverageRating:F1}/5 - {ratingText} ({spot.ReviewCount} reviews)");

        var priceText = GetCulturalPriceDescription(spot.PriceRange, language);
        response.AppendLine($"💰 {priceText}");

        if (userLocation != null)
        {
            var distance = spot.DistanceKm ?? CalculateDistance(userLocation, new Location(spot.Location.Latitude, spot.Location.Longitude));
            var distanceText = language switch
            {
                "pidgin" => $"📏 E dey {distance:F1}km from where you dey",
                "yoruba" => $"📏 O jinna {distance:F1}km lati ibi ti o wa",
                _ => $"📏 {distance:F1}km from your location"
            };
            response.AppendLine(distanceText);
        }

        if (!string.IsNullOrEmpty(spot.OpeningTime) && !string.IsNullOrEmpty(spot.ClosingTime))
        {
            var hoursText = language switch
            {
                "pidgin" => $"🕐 Dem dey open from {spot.OpeningTime} to {spot.ClosingTime}",
                "yoruba" => $"🕐 Won n ṣi lati {spot.OpeningTime} si {spot.ClosingTime}",
                _ => $"🕐 Open {spot.OpeningTime} - {spot.ClosingTime}"
            };
            response.AppendLine(hoursText);
        }
        
        return response.ToString();
    }
    
    private string GetCulturalRatingDescription(decimal rating, string language)
    {
        return language switch
        {
            "pidgin" => rating switch
            {
                >= 4.5m => "E too sweet!",
                >= 4.0m => "E dey very correct",
                >= 3.5m => "E dey okay",
                >= 3.0m => "E fit do",
                _ => "Manage am"
            },
            "yoruba" => rating switch
            {
                >= 4.5m => "O dun pupọ!",
                >= 4.0m => "O dara pupọ",
                >= 3.5m => "O wa bẹẹ",
                >= 3.0m => "O le ṣe",
                _ => "Ko dara to"
            },
            _ => rating switch
            {
                >= 4.5m => "Excellent!",
                >= 4.0m => "Very good",
                >= 3.5m => "Good",
                >= 3.0m => "Fair",
                _ => "Below average"
            }
        };
    }
    
    private string GetCulturalPriceDescription(PriceRange priceRange, string language)
    {
        return language switch
        {
            "pidgin" => priceRange switch
            {
                PriceRange.Budget => "Cheap price - under ₦1000 (pocket friendly o!)",
                PriceRange.Moderate => "Moderate price - ₦1000-₦2500 (e dey reasonable)",
                PriceRange.Expensive => "Premium price - above ₦2500 (na big boy/girl level)",
                _ => "Price no clear"
            },
            "yoruba" => priceRange switch
            {
                PriceRange.Budget => "Owo kekere - kere ju ₦1000 (ko wọn pupọ)",
                PriceRange.Moderate => "Owo iwọntunwọnsi - ₦1000-₦2500 (o bọ sọtọ)",
                PriceRange.Expensive => "Owo nla - ju ₦2500 lọ (fun awọn ọlọrọ)",
                _ => "Owo ko han"
            },
            _ => priceRange switch
            {
                PriceRange.Budget => "Budget-friendly - under ₦1000",
                PriceRange.Moderate => "Moderate pricing - ₦1000-₦2500",
                PriceRange.Expensive => "Premium pricing - above ₦2500",
                _ => "Price range unclear"
            }
        };
    }
}

public class CulturalResponseTemplates
{
    public string[] Greetings { get; set; } = Array.Empty<string>();
    public string[] Expressions { get; set; } = Array.Empty<string>();
    public string[] Encouragements { get; set; } = Array.Empty<string>();
    public string[] Transitions { get; set; } = Array.Empty<string>();
}