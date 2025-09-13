namespace AmalaSpotLocator.Models;

public class AudioValidationResult
{
    public bool IsValid { get; set; }
    
    public string? ErrorMessage { get; set; }
    
    public AudioMetadata? Metadata { get; set; }
    
    public List<string> Warnings { get; set; } = new();
}

public class AudioMetadata
{
    public string Format { get; set; } = string.Empty;
    
    public int SampleRate { get; set; }
    
    public int Channels { get; set; }
    
    public int BitRate { get; set; }
    
    public TimeSpan Duration { get; set; }
    
    public long FileSizeBytes { get; set; }
}