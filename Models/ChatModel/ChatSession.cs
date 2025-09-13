namespace AmalaSpotLocator.Models.ChatModel;

public class ChatSession
{
    public string SessionId { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime LastActivity { get; set; } = DateTime.UtcNow;
    
    public List<ChatMessage> Messages { get; set; } = new();
    
    public Dictionary<string, object> Context { get; set; } = new();
    
    public string? UserId { get; set; }
    
    public Location? LastKnownLocation { get; set; }
    
    public string PreferredLanguage { get; set; } = "en";
}

public class ChatMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    public string Content { get; set; } = string.Empty;
    
    public bool IsFromUser { get; set; }
    
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    public MessageType Type { get; set; } = MessageType.Text;
    
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public enum MessageType
{
    Text,
    Voice,
    System
}