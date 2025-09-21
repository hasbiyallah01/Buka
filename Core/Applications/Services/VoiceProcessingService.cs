using AmalaSpotLocator.Models.Exceptions;
using AmalaSpotLocator.Configuration;
using Microsoft.Extensions.Options;
using OpenAI;
using NAudio.Wave;
using System.Text;
using System.Collections.Generic;
using AmalaSpotLocator.Core.Applications.Interfaces.Services;
using AmalaSpotLocator.Models;

namespace AmalaSpotLocator.Core.Applications.Services;

public class VoiceProcessingService : IVoiceProcessingService
{
    private readonly OpenAIClient _openAIClient;
    private readonly OpenAISettings _openAISettings;
    private readonly ILogger<VoiceProcessingService> _logger;

    private readonly HashSet<string> _supportedFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "wav", "mp3", "mp4", "mpeg", "mpga", "m4a", "webm", "flac"
    };

    private const long MaxFileSizeBytes = 25 * 1024 * 1024;

    private static readonly TimeSpan MaxDuration = TimeSpan.FromMinutes(10);

    public VoiceProcessingService(
        IOptions<OpenAISettings> openAISettings,
        ILogger<VoiceProcessingService> logger)
    {
        _openAISettings = openAISettings.Value;
        _openAIClient = new OpenAIClient(_openAISettings.ApiKey);
        _logger = logger;
    }

    public async Task<string> SpeechToTextAsync(byte[] audioData, string audioFormat = "wav")
    {
        try
        {
            _logger.LogInformation("Starting speech-to-text conversion for {Size} bytes of {Format} audio", 
                audioData.Length, audioFormat);

            var validationResult = await ValidateAudioInputAsync(audioData, audioFormat);
            if (!validationResult.IsValid)
            {
                throw new VoiceProcessingException($"Audio validation failed: {validationResult.ErrorMessage}");
            }

            var processedAudio = audioData;
            var processedFormat = audioFormat.ToLowerInvariant();
            
            if (!_supportedFormats.Contains(processedFormat))
            {
                _logger.LogInformation("Converting audio from {SourceFormat} to wav", audioFormat);
                processedAudio = await ConvertAudioFormatAsync(audioData, audioFormat, "wav");
                processedFormat = "wav";
            }

            _logger.LogWarning("Voice processing is not fully implemented. Returning placeholder text.");
            
            await Task.Delay(1000); 
            
            return "I'm looking for amala spots near me"; 
        }
        catch (Exception ex) when (!(ex is VoiceProcessingException))
        {
            _logger.LogError(ex, "Error during speech-to-text conversion");
            throw new VoiceProcessingException("Failed to convert speech to text", ex);
        }
    }

    public async Task<byte[]> TextToSpeechAsync(string text, string language = "en")
    {
        try
        {
            _logger.LogInformation("Starting text-to-speech conversion for {Length} characters", text.Length);

            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException("Text cannot be null or empty", nameof(text));
            }

            if (text.Length > 4000)
            {
                text = text.Substring(0, 4000) + "...";
                _logger.LogWarning("Text truncated to 4000 characters for TTS processing");
            }

            _logger.LogWarning("Text-to-speech is not fully implemented. Returning placeholder audio data.");
            
            await Task.Delay(1000); 

            var placeholderWav = new byte[] 
            {
                0x52, 0x49, 0x46, 0x46, 
                0x24, 0x00, 0x00, 0x00, 
                0x57, 0x41, 0x56, 0x45, 
                0x66, 0x6D, 0x74, 0x20, 
                0x10, 0x00, 0x00, 0x00, 
                0x01, 0x00,             
                0x01, 0x00,             
                0x44, 0xAC, 0x00, 0x00, 
                0x88, 0x58, 0x01, 0x00, 
                0x02, 0x00,             
                0x10, 0x00,             
                0x64, 0x61, 0x74, 0x61, 
                0x00, 0x00, 0x00, 0x00  
            };
            
            return placeholderWav;
        }
        catch (Exception ex) when (!(ex is VoiceProcessingException))
        {
            _logger.LogError(ex, "Error during text-to-speech conversion");
            throw new VoiceProcessingException("Failed to convert text to speech", ex);
        }
    }

    public async Task<AudioValidationResult> ValidateAudioInputAsync(byte[] audioData, string audioFormat)
    {
        try
        {
            var result = new AudioValidationResult();

            if (audioData == null || audioData.Length == 0)
            {
                result.IsValid = false;
                result.ErrorMessage = "Audio data is null or empty";
                return result;
            }

            if (audioData.Length > MaxFileSizeBytes)
            {
                result.IsValid = false;
                result.ErrorMessage = $"Audio file size ({audioData.Length} bytes) exceeds maximum allowed size ({MaxFileSizeBytes} bytes)";
                return result;
            }

            if (!_supportedFormats.Contains(audioFormat.ToLowerInvariant()))
            {
                result.Warnings.Add($"Audio format '{audioFormat}' is not directly supported and will be converted");
            }

            try
            {
                var metadata = await ExtractAudioMetadataAsync(audioData, audioFormat);
                result.Metadata = metadata;

                if (metadata.Duration > MaxDuration)
                {
                    result.IsValid = false;
                    result.ErrorMessage = $"Audio duration ({metadata.Duration}) exceeds maximum allowed duration ({MaxDuration})";
                    return result;
                }

                if (metadata.SampleRate < 8000)
                {
                    result.Warnings.Add($"Low sample rate ({metadata.SampleRate} Hz) may affect transcription quality");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not extract audio metadata for validation");
                result.Warnings.Add("Could not validate audio metadata");
            }

            result.IsValid = true;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during audio validation");
            return new AudioValidationResult
            {
                IsValid = false,
                ErrorMessage = "Audio validation failed due to internal error"
            };
        }
    }

    public async Task<byte[]> ConvertAudioFormatAsync(byte[] audioData, string sourceFormat, string targetFormat)
    {
        try
        {
            _logger.LogInformation("Converting audio from {SourceFormat} to {TargetFormat}", sourceFormat, targetFormat);

            
            using var inputStream = new MemoryStream(audioData);
            using var outputStream = new MemoryStream();

            if (sourceFormat.ToLowerInvariant() == "wav" && targetFormat.ToLowerInvariant() == "wav")
            {

                return audioData;
            }

            _logger.LogWarning("Audio format conversion from {SourceFormat} to {TargetFormat} not fully implemented", 
                sourceFormat, targetFormat);
            
            return audioData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during audio format conversion");
            throw new VoiceProcessingException("Failed to convert audio format", ex);
        }
    }

    private async Task<AudioMetadata> ExtractAudioMetadataAsync(byte[] audioData, string audioFormat)
    {
        try
        {
            using var stream = new MemoryStream(audioData);

            if (audioFormat.ToLowerInvariant() == "wav")
            {
                using var waveFileReader = new WaveFileReader(stream);
                
                return new AudioMetadata
                {
                    Format = audioFormat,
                    SampleRate = waveFileReader.WaveFormat.SampleRate,
                    Channels = waveFileReader.WaveFormat.Channels,
                    BitRate = waveFileReader.WaveFormat.AverageBytesPerSecond * 8,
                    Duration = waveFileReader.TotalTime,
                    FileSizeBytes = audioData.Length
                };
            }

            return new AudioMetadata
            {
                Format = audioFormat,
                SampleRate = 44100, 
                Channels = 1, 
                BitRate = 128000, 
                Duration = TimeSpan.FromSeconds(audioData.Length / 16000.0), 
                FileSizeBytes = audioData.Length
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not extract detailed audio metadata");
            throw;
        }
    }
}