using System.ComponentModel.DataAnnotations;

namespace AmalaSpotLocator.Models.UserModel;

public class UserInput
{
    [Required]
    public string Message { get; set; } = string.Empty;
    
    public string? SessionId { get; set; }
    
    public Location? UserLocation { get; set; }
    
    public string Language { get; set; } = "en";
    
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class VoiceInput : UserInput
{
    [Required]
    public byte[] AudioData { get; set; } = Array.Empty<byte>();
    
    public string AudioFormat { get; set; } = "wav";
}