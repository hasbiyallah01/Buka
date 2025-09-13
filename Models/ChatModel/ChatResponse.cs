using AmalaSpotLocator.Models.DTOs;

namespace AmalaSpotLocator.Models.ChatModel;

public class ChatResponse
{
    public bool Success { get; set; }
    
    public string Reply { get; set; } = string.Empty;
    
    public List<SpotDto>? Spots { get; set; }
    
    public string? MapUrl { get; set; }
    
    public string SessionId { get; set; } = string.Empty;
    
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    public string? ErrorMessage { get; set; }
    
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class VoiceChatResponse : ChatResponse
{
    public byte[]? AudioData { get; set; }
    
    public string AudioFormat { get; set; } = "wav";
    
    public string? AudioContentType { get; set; } = "audio/wav";
}