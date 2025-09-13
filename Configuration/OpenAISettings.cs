namespace AmalaSpotLocator.Configuration;

public class OpenAISettings
{
    public const string SectionName = "OpenAI";
    
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-4";
    public string WhisperModel { get; set; } = "whisper-1";
}