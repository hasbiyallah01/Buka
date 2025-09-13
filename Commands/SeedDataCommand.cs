using AmalaSpotLocator.Core.Applications.Services;
using AmalaSpotLocator.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.CommandLine;
using System.Threading.Tasks;

namespace AmalaSpotLocator.Commands;

public static class SeedDataCommand
{
    public static Command CreateCommand()
    {
        var command = new Command("seed", "Seed the database with test data");

        var dataSetOption = new Option<string>(
            "--dataset",
            description: "Type of dataset to seed (production, minimal, integration, performance, edgecases)")
        {
            IsRequired = false
        };
        dataSetOption.SetDefaultValue("production");

        var forceOption = new Option<bool>(
            "--force",
            description: "Force reseed by clearing existing data")
        {
            IsRequired = false
        };

        var validateOption = new Option<bool>(
            "--validate",
            description: "Validate data after seeding")
        {
            IsRequired = false
        };
        validateOption.SetDefaultValue(true);

        var userCountOption = new Option<int>(
            "--users",
            description: "Number of users for performance testing")
        {
            IsRequired = false
        };
        userCountOption.SetDefaultValue(1000);

        var spotCountOption = new Option<int>(
            "--spots",
            description: "Number of spots for performance testing")
        {
            IsRequired = false
        };
        spotCountOption.SetDefaultValue(5000);

        command.AddOption(dataSetOption);
        command.AddOption(forceOption);
        command.AddOption(validateOption);
        command.AddOption(userCountOption);
        command.AddOption(spotCountOption);

        command.SetHandler(async (dataSet, force, validate, userCount, spotCount) =>
        {
            await ExecuteSeedCommand(dataSet, force, validate, userCount, spotCount);
        }, dataSetOption, forceOption, validateOption, userCountOption, spotCountOption);

        return command;
    }

    private static async Task ExecuteSeedCommand(
        string dataSet, 
        bool force, 
        bool validate, 
        int userCount, 
        int spotCount)
    {
        Console.WriteLine($"Starting database seeding with dataset: {dataSet}");

        try
        {

            if (!Enum.TryParse<DataSetType>(dataSet, true, out var dataSetType))
            {
                Console.WriteLine($"Invalid dataset type: {dataSet}");
                Console.WriteLine("Valid options: production, minimal, integration, performance, edgecases");
                return;
            }

            var config = new SeederConfiguration
            {
                DataSetType = dataSetType,
                ForceReseed = force,
                ValidateAfterSeeding = validate,
                SkipIfDataExists = !force,
                PerformanceUserCount = userCount,
                PerformanceSpotCount = spotCount,
                RandomSeed = 42
            };

            var services = new ServiceCollection();
            services.AddDbContext<AmalaSpotContext>(options =>
            {
                var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnections");
                options.UseNpgsql(connectionString, x => x.UseNetTopologySuite());
            });
            services.AddDatabaseSeeding();
            services.AddLogging(builder => builder.AddConsole());

            var serviceProvider = services.BuildServiceProvider();

            await serviceProvider.SeedDatabaseAsync(config);

            Console.WriteLine("Database seeding completed successfully!");

            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AmalaSpotContext>();
            
            var userCount_actual = await context.Users.CountAsync();
            var spotCount_actual = await context.AmalaSpots.CountAsync();
            var reviewCount = await context.Reviews.CountAsync();

            Console.WriteLine($"Summary:");
            Console.WriteLine($"  Users: {userCount_actual}");
            Console.WriteLine($"  Spots: {spotCount_actual}");
            Console.WriteLine($"  Reviews: {reviewCount}");

            if (validate)
            {
                Console.WriteLine("\nValidating database...");
                var report = await serviceProvider.ValidateDatabaseAsync();
                
                if (report.IsValid)
                {
                    Console.WriteLine("✓ Database validation passed");
                }
                else
                {
                    Console.WriteLine($"✗ Database validation failed with {report.ErrorCount} errors");
                    foreach (var issue in report.Issues.Where(i => i.Severity == ValidationSeverity.Error))
                    {
                        Console.WriteLine($"  ERROR: {issue.Category} - {issue.Message}");
                    }
                }

                if (report.WarningCount > 0)
                {
                    Console.WriteLine($"⚠ {report.WarningCount} warnings found");
                    foreach (var issue in report.Issues.Where(i => i.Severity == ValidationSeverity.Warning))
                    {
                        Console.WriteLine($"  WARNING: {issue.Category} - {issue.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during seeding: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
            }
            Environment.Exit(1);
        }
    }
}

public class SeedProgram
{
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("AmalaSpotLocator Database Seeder");
        rootCommand.AddCommand(SeedDataCommand.CreateCommand());

        return await rootCommand.InvokeAsync(args);
    }
}