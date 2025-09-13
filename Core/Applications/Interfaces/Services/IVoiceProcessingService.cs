using AmalaSpotLocator.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AmalaSpotLocator.Core.Applications.Interfaces.Services;

public interface IVoiceProcessingService
{

    Task<string> SpeechToTextAsync(byte[] audioData, string audioFormat = "wav");

    Task<byte[]> TextToSpeechAsync(string text, string language = "en");

    Task<AudioValidationResult> ValidateAudioInputAsync(byte[] audioData, string audioFormat);

    Task<byte[]> ConvertAudioFormatAsync(byte[] audioData, string sourceFormat, string targetFormat);
}