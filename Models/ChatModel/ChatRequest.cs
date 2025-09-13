using System.ComponentModel.DataAnnotations;
using AmalaSpotLocator.Attributes;

namespace AmalaSpotLocator.Models.ChatModel;

public class TextChatRequest
{
    [Required(ErrorMessage = "Message is required")]
    [StringLength(1000, ErrorMessage = "Message cannot exceed 1000 characters")]
    [SafeString]
    public string Message { get; set; } = string.Empty;
    
    [StringLength(50)]
    public string? SessionId { get; set; }
    
    public Location? UserLocation { get; set; }
    
    [StringLength(10, ErrorMessage = "Language code cannot exceed 10 characters")]
    [RegularExpression(@"^[a-z]{2}(-[A-Z]{2})?$", ErrorMessage = "Invalid language code format")]
    public string Language { get; set; } = "en";
}

public class VoiceChatRequest
{
    [Required(ErrorMessage = "Audio data is required")]
    public IFormFile AudioFile { get; set; } = null!;
    
    [StringLength(50)]
    public string? SessionId { get; set; }
    
    public Location? UserLocation { get; set; }
    
    [StringLength(10, ErrorMessage = "Language code cannot exceed 10 characters")]
    [RegularExpression(@"^[a-z]{2}(-[A-Z]{2})?$", ErrorMessage = "Invalid language code format")]
    public string Language { get; set; } = "en";
    
    [StringLength(10, ErrorMessage = "Audio format cannot exceed 10 characters")]
    [RegularExpression(@"^(wav|mp3|m4a|ogg|flac)$", ErrorMessage = "Unsupported audio format")]
    public string AudioFormat { get; set; } = "wav";
}