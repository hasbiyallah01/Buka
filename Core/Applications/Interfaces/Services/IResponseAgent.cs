using System.Collections.Generic;
using AmalaSpotLocator.Models;
using AmalaSpotLocator.Models.UserModel;

namespace AmalaSpotLocator.Interfaces;

public interface IResponseAgent
{
    Task<string> GenerateTextResponse(UserIntent intent, QueryResult result);
    Task<byte[]> GenerateVoiceResponse(string textResponse);
    Task<string> GenerateErrorResponse(string errorMessage, string language = "en");
    Task<string> GenerateClarificationResponse(UserIntent intent);
}