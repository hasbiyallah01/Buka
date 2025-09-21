using System;
using System.Collections.Generic;

namespace AmalaSpotLocator.Configuration;

public class SecuritySettings
{
    public const string SectionName = "Security";
    
    public RateLimitSettings RateLimit { get; set; } = new();
    public CorsSettings Cors { get; set; } = new();
    public ValidationSettings Validation { get; set; } = new();
    public EncryptionSettings Encryption { get; set; } = new();
}

public class RateLimitSettings
{
    public int DefaultLimit { get; set; } = 100;
    public TimeSpan WindowSize { get; set; } = TimeSpan.FromMinutes(1);
    public int ChatTextLimit { get; set; } = 100;
    public int ChatVoiceLimit { get; set; } = 20;
    public int SpotCreateLimit { get; set; } = 10;
    public int AuthLimit { get; set; } = 5;
}

public class CorsSettings
{
    public List<string> AllowedOrigins { get; set; } = new();
    public List<string> AllowedMethods { get; set; } = new() { "GET", "POST", "PUT", "DELETE", "OPTIONS" };
    public List<string> AllowedHeaders { get; set; } = new() { "Content-Type", "Authorization", "X-Requested-With" };
    public bool AllowCredentials { get; set; } = true;
    public TimeSpan PreflightMaxAge { get; set; } = TimeSpan.FromMinutes(10);
}

public class ValidationSettings
{
    public int MaxRequestSizeBytes { get; set; } = 10 * 1024 * 1024; 
    public int MaxMessageLength { get; set; } = 1000;
    public int MaxUrlLength { get; set; } = 2048;
    public int MaxHeaderLength { get; set; } = 500;
    public List<string> AllowedContentTypes { get; set; } = new()
    {
        "application/json",
        "multipart/form-data",
        "application/x-www-form-urlencoded"
    };
}

public class EncryptionSettings
{
    public string DataProtectionKeyPath { get; set; } = "./keys";
    public string ApplicationName { get; set; } = "AmalaSpotLocator";
    public TimeSpan KeyLifetime { get; set; } = TimeSpan.FromDays(90);
}