namespace AmalaSpotLocator.Configuration;

public class GoogleMapsSettings
{
    public const string SectionName = "GoogleMaps";
    
    public string ApiKey { get; set; } = string.Empty;
    public string PlacesApiKey { get; set; } = string.Empty;
}