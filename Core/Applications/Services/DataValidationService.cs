using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using AmalaSpotLocator.Models;
using AmalaSpotLocator.Infrastructure;

namespace AmalaSpotLocator.Core.Applications.Services;

public interface IDataValidationService
{
    Task<ValidationReport> ValidateDatabase();
    Task<ValidationReport> ValidateEntity<T>(T entity) where T : class;
    Task<bool> ValidateSpotLocation(double latitude, double longitude);
    Task<bool> ValidatePhoneNumber(string phoneNumber);
    Task<bool> ValidateEmailUniqueness(string email, Guid? excludeUserId = null);
}

public class DataValidationService : IDataValidationService
{
    private readonly AmalaSpotContext _context;

    public DataValidationService(AmalaSpotContext context)
    {
        _context = context;
    }

    public async Task<ValidationReport> ValidateDatabase()
    {
        var report = new ValidationReport();

        try
        {

            await ValidateUsers(report);

            await ValidateSpots(report);

            await ValidateReviews(report);

            await ValidateRelationships(report);

            await ValidateCalculatedFields(report);
        }
        catch (Exception ex)
        {
            report.AddError("Database", $"Validation failed: {ex.Message}");
        }

        return report;
    }

    public async Task<ValidationReport> ValidateEntity<T>(T entity) where T : class
    {
        var report = new ValidationReport();
        
        if (entity == null)
        {
            report.AddError(typeof(T).Name, "Entity cannot be null");
            return report;
        }

        var validationContext = new ValidationContext(entity);
        var validationResults = new List<ValidationResult>();
        
        if (!Validator.TryValidateObject(entity, validationContext, validationResults, true))
        {
            foreach (var result in validationResults)
            {
                report.AddError(typeof(T).Name, result.ErrorMessage ?? "Validation error");
            }
        }

        switch (entity)
        {
            case User user:
                await ValidateUserEntity(user, report);
                break;
            case AmalaSpot spot:
                await ValidateSpotEntity(spot, report);
                break;
            case Review review:
                await ValidateReviewEntity(review, report);
                break;
        }

        return report;
    }

    public async Task<bool> ValidateSpotLocation(double latitude, double longitude)
    {

        var nigeriaBounds = new
        {
            MinLat = 4.0,
            MaxLat = 14.0,
            MinLng = 2.5,
            MaxLng = 15.0
        };

        if (latitude < nigeriaBounds.MinLat || latitude > nigeriaBounds.MaxLat ||
            longitude < nigeriaBounds.MinLng || longitude > nigeriaBounds.MaxLng)
        {
            return false;
        }

        return await Task.FromResult(true);
    }

    public async Task<bool> ValidatePhoneNumber(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return true; 

        var patterns = new[]
        {
            @"^\+234[789][01]\d{8}$", 
            @"^0[789][01]\d{8}$"      
        };

        return await Task.FromResult(patterns.Any(pattern => 
            System.Text.RegularExpressions.Regex.IsMatch(phoneNumber, pattern)));
    }

    public async Task<bool> ValidateEmailUniqueness(string email, Guid? excludeUserId = null)
    {
        var query = _context.Users.Where(u => u.Email.ToLower() == email.ToLower());
        
        if (excludeUserId.HasValue)
        {
            query = query.Where(u => u.Id != excludeUserId.Value);
        }

        return !await query.AnyAsync();
    }

    private async Task ValidateUsers(ValidationReport report)
    {
        var users = await _context.Users.ToListAsync();

        foreach (var user in users)
        {

            var duplicateEmails = users.Count(u => u.Email.ToLower() == user.Email.ToLower());
            if (duplicateEmails > 1)
            {
                report.AddError("User", $"Duplicate email found: {user.Email}");
            }

            if (!string.IsNullOrEmpty(user.PhoneNumber) && !await ValidatePhoneNumber(user.PhoneNumber))
            {
                report.AddError("User", $"Invalid phone number format: {user.PhoneNumber}");
            }

            if (string.IsNullOrEmpty(user.PasswordHash))
            {
                report.AddError("User", $"User {user.Email} has no password hash");
            }
        }

        report.AddInfo("Users", $"Validated {users.Count} users");
    }

    private async Task ValidateSpots(ValidationReport report)
    {
        var spots = await _context.AmalaSpots.ToListAsync();

        foreach (var spot in spots)
        {

            if (spot.Location != null)
            {
                var isValidLocation = await ValidateSpotLocation(spot.Location.Y, spot.Location.X);
                if (!isValidLocation)
                {
                    report.AddError("AmalaSpot", $"Invalid location for spot: {spot.Name}");
                }
            }

            if (spot.OpeningTime.HasValue && spot.ClosingTime.HasValue)
            {
                if (spot.OpeningTime >= spot.ClosingTime)
                {
                    report.AddWarning("AmalaSpot", $"Opening time is after closing time for spot: {spot.Name}");
                }
            }

            if (!string.IsNullOrEmpty(spot.PhoneNumber) && !await ValidatePhoneNumber(spot.PhoneNumber))
            {
                report.AddError("AmalaSpot", $"Invalid phone number for spot: {spot.Name}");
            }

            if (spot.Specialties?.Count > 20)
            {
                report.AddWarning("AmalaSpot", $"Too many specialties for spot: {spot.Name}");
            }
        }

        report.AddInfo("AmalaSpots", $"Validated {spots.Count} spots");
    }

    private async Task ValidateReviews(ValidationReport report)
    {
        var reviews = await _context.Reviews.ToListAsync();

        foreach (var review in reviews)
        {

            if (review.Rating < 1 || review.Rating > 5)
            {
                report.AddError("Review", $"Invalid rating {review.Rating} for review {review.Id}");
            }

            var sameUserSpotReviews = reviews.Where(r => 
                r.UserId == review.UserId && 
                r.SpotId == review.SpotId && 
                r.CreatedAt.Date == review.CreatedAt.Date).Count();

            if (sameUserSpotReviews > 1)
            {
                report.AddWarning("Review", $"Multiple reviews from same user on same day for review {review.Id}");
            }
        }

        report.AddInfo("Reviews", $"Validated {reviews.Count} reviews");
    }

    private async Task ValidateRelationships(ValidationReport report)
    {

        var orphanedReviews = await _context.Reviews
            .Where(r => !_context.AmalaSpots.Any(s => s.Id == r.SpotId) || 
                       !_context.Users.Any(u => u.Id == r.UserId))
            .CountAsync();

        if (orphanedReviews > 0)
        {
            report.AddError("Relationships", $"Found {orphanedReviews} orphaned reviews");
        }

        var spotsWithoutCreators = await _context.AmalaSpots
            .Where(s => s.CreatedByUserId.HasValue && 
                       !_context.Users.Any(u => u.Id == s.CreatedByUserId))
            .CountAsync();

        if (spotsWithoutCreators > 0)
        {
            report.AddError("Relationships", $"Found {spotsWithoutCreators} spots with invalid creators");
        }

        report.AddInfo("Relationships", "Relationship validation completed");
    }

    private async Task ValidateCalculatedFields(ValidationReport report)
    {
        var spots = await _context.AmalaSpots.Include(s => s.Reviews).ToListAsync();

        foreach (var spot in spots)
        {
            if (spot.Reviews.Any())
            {
                var calculatedRating = (decimal)spot.Reviews.Average(r => r.Rating);
                var calculatedCount = spot.Reviews.Count;

                if (Math.Abs(spot.AverageRating - calculatedRating) > 0.01m)
                {
                    report.AddError("CalculatedFields", 
                        $"Average rating mismatch for spot {spot.Name}: stored={spot.AverageRating}, calculated={calculatedRating}");
                }

                if (spot.ReviewCount != calculatedCount)
                {
                    report.AddError("CalculatedFields", 
                        $"Review count mismatch for spot {spot.Name}: stored={spot.ReviewCount}, calculated={calculatedCount}");
                }
            }
            else if (spot.AverageRating != 0 || spot.ReviewCount != 0)
            {
                report.AddError("CalculatedFields", 
                    $"Spot {spot.Name} has no reviews but non-zero rating/count");
            }
        }

        report.AddInfo("CalculatedFields", "Calculated fields validation completed");
    }

    private async Task ValidateUserEntity(User user, ValidationReport report)
    {

        if (!await ValidateEmailUniqueness(user.Email, user.Id))
        {
            report.AddError("User", "Email address is already in use");
        }

        if (!string.IsNullOrEmpty(user.PhoneNumber) && !await ValidatePhoneNumber(user.PhoneNumber))
        {
            report.AddError("User", "Invalid phone number format");
        }

        if (user.PreferredMaxBudget.HasValue && user.PreferredMaxBudget <= 0)
        {
            report.AddError("User", "Preferred max budget must be positive");
        }

        if (user.PreferredMinRating.HasValue && 
            (user.PreferredMinRating < 0 || user.PreferredMinRating > 5))
        {
            report.AddError("User", "Preferred min rating must be between 0 and 5");
        }
    }

    private async Task ValidateSpotEntity(AmalaSpot spot, ValidationReport report)
    {

        if (spot.Location != null)
        {
            var isValidLocation = await ValidateSpotLocation(spot.Location.Y, spot.Location.X);
            if (!isValidLocation)
            {
                report.AddError("AmalaSpot", "Location coordinates are outside Nigeria");
            }
        }

        if (!string.IsNullOrEmpty(spot.PhoneNumber) && !await ValidatePhoneNumber(spot.PhoneNumber))
        {
            report.AddError("AmalaSpot", "Invalid phone number format");
        }

        if (spot.OpeningTime.HasValue && spot.ClosingTime.HasValue)
        {
            if (spot.OpeningTime >= spot.ClosingTime)
            {
                report.AddWarning("AmalaSpot", "Opening time should be before closing time");
            }
        }

        if (spot.Specialties?.Count > 20)
        {
            report.AddWarning("AmalaSpot", "Consider reducing the number of specialties");
        }
    }

    private async Task ValidateReviewEntity(Review review, ValidationReport report)
    {

        var spotExists = await _context.AmalaSpots.AnyAsync(s => s.Id == review.SpotId);
        if (!spotExists)
        {
            report.AddError("Review", "Referenced spot does not exist");
        }

        var userExists = await _context.Users.AnyAsync(u => u.Id == review.UserId);
        if (!userExists)
        {
            report.AddError("Review", "Referenced user does not exist");
        }

        if (review.Rating < 1 || review.Rating > 5)
        {
            report.AddError("Review", "Rating must be between 1 and 5");
        }
    }
}

public class ValidationReport
{
    public List<ValidationIssue> Issues { get; } = new();
    public bool IsValid => !Issues.Any(i => i.Severity == ValidationSeverity.Error);
    public int ErrorCount => Issues.Count(i => i.Severity == ValidationSeverity.Error);
    public int WarningCount => Issues.Count(i => i.Severity == ValidationSeverity.Warning);
    public int InfoCount => Issues.Count(i => i.Severity == ValidationSeverity.Info);

    public void AddError(string category, string message)
    {
        Issues.Add(new ValidationIssue(ValidationSeverity.Error, category, message));
    }

    public void AddWarning(string category, string message)
    {
        Issues.Add(new ValidationIssue(ValidationSeverity.Warning, category, message));
    }

    public void AddInfo(string category, string message)
    {
        Issues.Add(new ValidationIssue(ValidationSeverity.Info, category, message));
    }

    public override string ToString()
    {
        var summary = $"Validation Report: {ErrorCount} errors, {WarningCount} warnings, {InfoCount} info";
        if (Issues.Any())
        {
            summary += "\n" + string.Join("\n", Issues.Select(i => $"[{i.Severity}] {i.Category}: {i.Message}"));
        }
        return summary;
    }
}

public record ValidationIssue(ValidationSeverity Severity, string Category, string Message);

public enum ValidationSeverity
{
    Info,
    Warning,
    Error
}