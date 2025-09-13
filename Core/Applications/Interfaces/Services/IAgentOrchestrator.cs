using System.Collections.Generic;
using AmalaSpotLocator.Models.UserModel;
using System.Threading.Tasks;
using AmalaSpotLocator.Models;

namespace AmalaSpotLocator.Interfaces;

public interface IAgentOrchestrator
{
    Task<AgentResponse> ProcessUserInput(UserInput input);
    Task<VoiceResponse> ProcessVoiceInput(VoiceInput input);
    Task<AgentResponse> ProcessUserIntent(UserIntent intent);
}