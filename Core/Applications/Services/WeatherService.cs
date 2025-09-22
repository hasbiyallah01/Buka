using AmalaSpotLocator.Core.Applications.Interfaces;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace AmalaSpotLocator.Core.Applications.Services;

public class WeatherService : IWeatherService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public WeatherService(HttpClient httpClient, IOptions<WeatherApiSettings> settings)
    {
        _httpClient = httpClient;
        _apiKey = settings.Value.ApiKey;
    }

    public async Task<WeatherForecast?> GetWeatherAsync(double latitude, double longitude, int days = 3)
    {
        var url = $"http://api.weatherapi.com/v1/forecast.json?key={_apiKey}&q={latitude},{longitude}&days={days}&aqi=no&alerts=no";

        var response = await _httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync();
        var weatherData = JsonSerializer.Deserialize<WeatherForecast>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return weatherData;
    }
}

public class WeatherApiSettings
{
    public string ApiKey { get; set; } = null!;
}

public class WeatherForecast
{
    public Locations Location { get; set; } = default!;
    public CurrentWeather Current { get; set; } = default!;
    public ForecastData Forecast { get; set; } = default!;
}

public class Locations
{
    public string Name { get; set; } = "";
    public string Region { get; set; } = "";
    public string Country { get; set; } = "";
    public double Lat { get; set; }
    public double Lon { get; set; }
}

public class CurrentWeather
{
    public double TempC { get; set; }
    public double TempF { get; set; }
    public Condition Condition { get; set; } = default!;
}

public class Condition
{
    public string Text { get; set; } = "";
    public string Icon { get; set; } = "";
    public int Code { get; set; }
}

public class ForecastData
{
    public List<ForecastDay> Forecastday { get; set; } = new();
}

public class ForecastDay
{
    public string Date { get; set; } = "";
    public Day Day { get; set; } = default!;
}

public class Day
{
    public double MaxtempC { get; set; }
    public double MintempC { get; set; }
    public Condition Condition { get; set; } = default!;
}
