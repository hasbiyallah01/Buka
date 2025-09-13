using System.Collections.Concurrent;
using AmalaSpotLocator.Interfaces;
using AmalaSpotLocator.Models.ChatModel;

namespace AmalaSpotLocator.Core.Applications.Services;

public class ChatSessionService : IChatSessionService
{
    private readonly ConcurrentDictionary<string, ChatSession> _sessions = new();
    private readonly ILogger<ChatSessionService> _logger;
    
    public ChatSessionService(ILogger<ChatSessionService> logger)
    {
        _logger = logger;
    }
    
    public Task<string> CreateSessionAsync(string? userId = null)
    {
        var sessionId = Guid.NewGuid().ToString();
        var session = new ChatSession
        {
            SessionId = sessionId,
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            LastActivity = DateTime.UtcNow
        };
        
        _sessions.TryAdd(sessionId, session);
        
        _logger.LogInformation("Created new chat session {SessionId} for user {UserId}", 
            sessionId, userId ?? "anonymous");
        
        return Task.FromResult(sessionId);
    }
    
    public Task<ChatSession?> GetSessionAsync(string sessionId)
    {
        _sessions.TryGetValue(sessionId, out var session);
        return Task.FromResult(session);
    }
    
    public async Task UpdateSessionActivityAsync(string sessionId)
    {
        var session = await GetSessionAsync(sessionId);
        if (session != null)
        {
            session.LastActivity = DateTime.UtcNow;
        }
    }
    
    public async Task AddMessageAsync(string sessionId, ChatMessage message)
    {
        var session = await GetSessionAsync(sessionId);
        if (session != null)
        {
            session.Messages.Add(message);
            session.LastActivity = DateTime.UtcNow;

            if (session.Messages.Count > 50)
            {
                session.Messages.RemoveAt(0);
            }
        }
    }
    
    public async Task UpdateSessionContextAsync(string sessionId, string key, object value)
    {
        var session = await GetSessionAsync(sessionId);
        if (session != null)
        {
            session.Context[key] = value;
            session.LastActivity = DateTime.UtcNow;
        }
    }
    
    public Task CleanupExpiredSessionsAsync(TimeSpan maxAge)
    {
        var cutoffTime = DateTime.UtcNow - maxAge;
        var expiredSessions = _sessions
            .Where(kvp => kvp.Value.LastActivity < cutoffTime)
            .Select(kvp => kvp.Key)
            .ToList();
        
        foreach (var sessionId in expiredSessions)
        {
            _sessions.TryRemove(sessionId, out _);
        }
        
        if (expiredSessions.Count > 0)
        {
            _logger.LogInformation("Cleaned up {Count} expired chat sessions", expiredSessions.Count);
        }
        
        return Task.CompletedTask;
    }
    
    public async Task<bool> IsSessionValidAsync(string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId))
            return false;
            
        var session = await GetSessionAsync(sessionId);
        return session != null;
    }
}