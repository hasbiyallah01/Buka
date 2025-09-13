using System;

namespace AmalaSpotLocator.Models.Exceptions;

public class VoiceProcessingException : AmalaSpotException
{
    public VoiceProcessingException(string message) 
        : base(message, "VOICE_PROCESSING_ERROR") { }
    
    public VoiceProcessingException(string message, Exception innerException) 
        : base(message, "VOICE_PROCESSING_ERROR", innerException) { }
}