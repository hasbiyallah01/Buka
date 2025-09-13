using AmalaSpotLocator.Models.DTOs;

namespace AmalaSpotLocator.Models;

public class AgentResponse
{
    public bool Success { get; set; }
    
    public string TextResponse { get; set; } = string.Empty;
    
    public List<SpotDto>? Spots { get; set; }
    
    public string? MapUrl { get; set; }
    
    public string? ErrorMessage { get; set; }
    
    public string SessionId { get; set; } = string.Empty;
    
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class VoiceResponse : AgentResponse
{
    public byte[]? AudioData { get; set; }
    
    public string AudioFormat { get; set; } = "wav";
}