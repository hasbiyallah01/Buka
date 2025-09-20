using AmalaSpotLocator.Models.DTOs;

namespace AmalaSpotLocator.Models;

public class QueryResult
{
    public bool Success { get; set; }
    
    public string? ErrorMessage { get; set; }
    
    public List<SpotDto> Spots { get; set; } = new();
    
    public List<Review> Reviews { get; set; } = new();
    
    public SpotDto? SingleSpot { get; set; }
    
    public int TotalCount { get; set; }
    
    public string? MapUrl { get; set; }
    
    public Dictionary<string, object> Metadata { get; set; } = new();
    
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
    
    public string Message { get; set; } = string.Empty;
    
    public object? Data { get; set; }
    
    public string QueryType { get; set; } = string.Empty;
}