using AmalaSpotLocator.Core.Applications.Services;

namespace AmalaSpotLocator.Core.Applications.Interfaces
{
    public interface IWeatherService
    {
        Task<WeatherForecast?> GetWeatherAsync(double latitude, double longitude, int days = 3);
    }
}