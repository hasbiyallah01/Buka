using System.Collections.Generic;
using AmalaSpotLocator.Models.UserModel;
using System.Threading.Tasks;
using AmalaSpotLocator.Models;

namespace AmalaSpotLocator.Interfaces;

public interface INLUAgent
{
    Task<UserIntent> ExtractIntent(string userMessage, string? sessionId = null, Location? userLocation = null, string? language = null);
    Task<UserIntent> ExtractIntentFromVoice(byte[] audioData, string? sessionId = null, Location? userLocation = null, string? language = null);
    Task<bool> ValidateIntent(UserIntent intent);
}