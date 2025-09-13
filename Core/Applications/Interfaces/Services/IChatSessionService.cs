using AmalaSpotLocator.Models.ChatModel;

namespace AmalaSpotLocator.Interfaces;

public interface IChatSessionService
{

    Task<string> CreateSessionAsync(string? userId = null);

    Task<ChatSession?> GetSessionAsync(string sessionId);

    Task UpdateSessionActivityAsync(string sessionId);

    Task AddMessageAsync(string sessionId, ChatMessage message);

    Task UpdateSessionContextAsync(string sessionId, string key, object value);

    Task CleanupExpiredSessionsAsync(TimeSpan maxAge);

    Task<bool> IsSessionValidAsync(string sessionId);
}