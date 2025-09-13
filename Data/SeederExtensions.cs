using AmalaSpotLocator.Core.Applications.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AmalaSpotLocator.Data;

public static class SeederExtensions
{

    public static async Task<IServiceProvider> SeedDatabaseAsync(
        this IServiceProvider serviceProvider, 
        SeederConfiguration? config = null)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AmalaSpotContext>();
        var loggerFactory = scope.ServiceProvider.GetService<ILoggerFactory>();
        var logger = loggerFactory?.CreateLogger("DatabaseSeeder");

        try
        {
            logger?.LogInformation("Starting database seeding with configuration: {DataSetType}", 
                config?.DataSetType ?? DataSetType.Production);

            await DatabaseSeeder.SeedAsync(context, config);

            logger?.LogInformation("Database seeding completed successfully");
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Database seeding failed");
            throw;
        }

        return serviceProvider;
    }

    public static async Task<IServiceProvider> SeedDevelopmentDataAsync(this IServiceProvider serviceProvider)
    {
        return await serviceProvider.SeedDatabaseAsync(SeederConfiguration.Presets.Development);
    }

    public static async Task<IServiceProvider> SeedTestDataAsync(this IServiceProvider serviceProvider)
    {
        return await serviceProvider.SeedDatabaseAsync(SeederConfiguration.Presets.Testing);
    }

    public static async Task<IServiceProvider> SeedPerformanceDataAsync(
        this IServiceProvider serviceProvider,
        int userCount = 1000,
        int spotCount = 5000)
    {
        var config = SeederConfiguration.Presets.Performance;
        config.PerformanceUserCount = userCount;
        config.PerformanceSpotCount = spotCount;
        
        return await serviceProvider.SeedDatabaseAsync(config);
    }

    public static async Task<ValidationReport> ValidateDatabaseAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var validationService = scope.ServiceProvider.GetRequiredService<IDataValidationService>();
        
        return await validationService.ValidateDatabase();
    }

    public static IServiceCollection AddDatabaseSeeding(this IServiceCollection services)
    {
        services.AddScoped<IDataValidationService, DataValidationService>();
        return services;
    }

    public static async Task<IServiceProvider> EnsureDatabaseAsync(
        this IServiceProvider serviceProvider,
        bool seedData = true,
        SeederConfiguration? config = null)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AmalaSpotContext>();
        var loggerFactory = scope.ServiceProvider.GetService<ILoggerFactory>();
        var logger = loggerFactory?.CreateLogger("DatabaseSeeder");

        try
        {

            await context.Database.EnsureCreatedAsync();
            logger?.LogInformation("Database ensured");

            if ((await context.Database.GetPendingMigrationsAsync()).Any())
            {
                await context.Database.MigrateAsync();
                logger?.LogInformation("Database migrations applied");
            }

            if (seedData)
            {
                await serviceProvider.SeedDatabaseAsync(config);
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Database initialization failed");
            throw;
        }

        return serviceProvider;
    }
}