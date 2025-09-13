using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using System.Collections.Generic;
using AmalaSpotLocator.Models;

namespace AmalaSpotLocator.Data;

public static class DatabaseSeeder
{
    private static readonly GeometryFactory GeometryFactory = new(new PrecisionModel(), 4326);
    
    public static async Task SeedAsync(AmalaSpotContext context, SeederConfiguration? config = null)
    {
        config ??= SeederConfiguration.Presets.Production;

        await context.Database.EnsureCreatedAsync();

        if (config.SkipIfDataExists && await context.AmalaSpots.AnyAsync())
        {
            return; // Database has been seeded
        }

        if (config.ForceReseed)
        {
            await ClearExistingData(context);
        }

        try
        {
            List<User> users;
            List<AmalaSpot> spots;
            List<Review> reviews;

            switch (config.DataSetType)
            {
                case DataSetType.Minimal:
                    users = await SeedMinimalData(context);
                    spots = TestDataSets.Minimal.GetSpots(users);
                    reviews = TestDataSets.Minimal.GetReviews(spots, users);
                    break;

                case DataSetType.Integration:
                    users = await SeedIntegrationData(context);
                    spots = TestDataSets.Integration.GetSpots(users);
                    reviews = TestDataSets.Integration.GetReviews(spots, users);
                    break;

                case DataSetType.Performance:
                    users = await SeedPerformanceData(context, config);
                    spots = TestDataSets.Performance.GetSpots(users, config.PerformanceSpotCount);
                    reviews = TestDataSets.Performance.GetReviews(spots, users, config.PerformanceReviewsPerSpot);
                    break;

                case DataSetType.EdgeCases:
                    users = await SeedEdgeCaseData(context);
                    spots = TestDataSets.EdgeCases.GetEdgeCaseSpots(users);
                    reviews = TestDataSets.EdgeCases.GetEdgeCaseReviews(spots, users);
                    break;

                case DataSetType.Production:
                default:
                    users = await SeedUsers(context);
                    spots = await SeedAmalaSpots(context, users, config.IncludeDevelopmentData);
                    reviews = new List<Review>();
                    await context.AmalaSpots.AddRangeAsync(spots);
                    await context.SaveChangesAsync();
                    await SeedReviews(context, spots, users);
                    break;
            }

            if (config.DataSetType != DataSetType.Production)
            {
                await context.AmalaSpots.AddRangeAsync(spots);
                await context.SaveChangesAsync();
                
                await context.Reviews.AddRangeAsync(reviews);
                await context.SaveChangesAsync();
            }

            await UpdateCalculatedFields(context);

            if (config.ValidateAfterSeeding)
            {
                await ValidateDataIntegrity(context);
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Database seeding failed: {ex.Message}", ex);
        }
    }

    public static async Task SeedAsync(AmalaSpotContext context, bool isDevelopment = false)
    {
        var config = isDevelopment 
            ? SeederConfiguration.Presets.Development 
            : SeederConfiguration.Presets.Production;
        
        await SeedAsync(context, config);
    }

    private static async Task<List<User>> SeedUsers(AmalaSpotContext context)
    {
        var users = new List<User>
        {

            new User
            {
                Id = Guid.NewGuid(),
                FirstName = "Adebayo",
                LastName = "Ogundimu",
                Email = "adebayo.ogundimu@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("SecurePass123!"),
                PhoneNumber = "+2348012345678",
                Role = UserRole.User,
                PreferredLanguage = "yo",
                PreferredMaxBudget = 2000m,
                PreferredMinRating = 3.5m,
                IsActive = true
            },
            new User
            {
                Id = Guid.NewGuid(),
                FirstName = "Fatima",
                LastName = "Ibrahim",
                Email = "fatima.ibrahim@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("SecurePass123!"),
                PhoneNumber = "+2348023456789",
                Role = UserRole.User,
                PreferredLanguage = "en",
                PreferredMaxBudget = 1500m,
                PreferredMinRating = 4.0m,
                IsActive = true
            },
            new User
            {
                Id = Guid.NewGuid(),
                FirstName = "Chinedu",
                LastName = "Okwu",
                Email = "chinedu.okwu@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("SecurePass123!"),
                PhoneNumber = "+2348034567890",
                Role = UserRole.User,
                PreferredLanguage = "en",
                PreferredMaxBudget = 3000m,
                PreferredMinRating = 3.0m,
                IsActive = true
            },
            new User
            {
                Id = Guid.NewGuid(),
                FirstName = "Kemi",
                LastName = "Adeyemi",
                Email = "kemi.adeyemi@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("SecurePass123!"),
                PhoneNumber = "+2348045678901",
                Role = UserRole.Moderator,
                PreferredLanguage = "yo",
                PreferredMaxBudget = 2500m,
                PreferredMinRating = 4.0m,
                IsActive = true
            },
            new User
            {
                Id = Guid.NewGuid(),
                FirstName = "Admin",
                LastName = "User",
                Email = "admin@amalaspotlocator.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("AdminPass123!"),
                PhoneNumber = "+2348056789012",
                Role = UserRole.Admin,
                PreferredLanguage = "en",
                IsActive = true
            }
        };

        foreach (var user in users)
        {
            ValidateEntity(user);
        }

        await context.Users.AddRangeAsync(users);
        await context.SaveChangesAsync();
        
        return users;
    }

    private static async Task<List<AmalaSpot>> SeedAmalaSpots(AmalaSpotContext context, List<User> users, bool isDevelopment)
    {
        var spots = new List<AmalaSpot>();

        spots.AddRange(GetLagosSpots(users));

        spots.AddRange(GetOyoSpots(users));

        spots.AddRange(GetOgunSpots(users));

        spots.AddRange(GetFCTSpots(users));

        spots.AddRange(GetOsunSpots(users));

        if (isDevelopment)
        {

            spots.AddRange(GetAdditionalTestSpots(users));
        }

        foreach (var spot in spots)
        {
            ValidateEntity(spot);
        }

        await context.AmalaSpots.AddRangeAsync(spots);
        await context.SaveChangesAsync();
        
        return spots;
    }

    private static List<AmalaSpot> GetLagosSpots(List<User> users)
    {
        return new List<AmalaSpot>
        {
            new AmalaSpot
            {
                Id = Guid.NewGuid(),
                Name = "Mama Cass Amala Joint",
                Description = "Famous for delicious amala and ewedu with assorted meat. A Lagos institution serving authentic Yoruba cuisine since 1995.",
                Address = "123 Ikorodu Road, Yaba, Lagos State",
                Location = GeometryFactory.CreatePoint(new Coordinate(3.3792, 6.5244)),
                PhoneNumber = "+2348012345678",
                OpeningTime = new TimeSpan(8, 0, 0),
                ClosingTime = new TimeSpan(22, 0, 0),
                PriceRange = PriceRange.Budget,
                Specialties = new List<string> { "Amala", "Ewedu", "Gbegiri", "Assorted Meat", "Ponmo" },
                IsVerified = true,
                CreatedByUserId = users[0].Id
            },
            new AmalaSpot
            {
                Id = Guid.NewGuid(),
                Name = "Iya Basira Bukka",
                Description = "Traditional Yoruba cuisine with authentic amala and various soups. Known for fresh ingredients and generous portions.",
                Address = "45 Allen Avenue, Ikeja, Lagos State",
                Location = GeometryFactory.CreatePoint(new Coordinate(3.3515, 6.6018)),
                PhoneNumber = "+2348023456789",
                OpeningTime = new TimeSpan(7, 30, 0),
                ClosingTime = new TimeSpan(21, 0, 0),
                PriceRange = PriceRange.Moderate,
                Specialties = new List<string> { "Amala", "Okra Soup", "Efo Riro", "Fish", "Chicken" },
                IsVerified = true,
                CreatedByUserId = users[1].Id
            },
            new AmalaSpot
            {
                Id = Guid.NewGuid(),
                Name = "Abula Spot Victoria Island",
                Description = "Home of the famous abula (amala, gbegiri, and ewedu combo). Premium location with modern ambiance.",
                Address = "78 Ahmadu Bello Way, Victoria Island, Lagos State",
                Location = GeometryFactory.CreatePoint(new Coordinate(3.4219, 6.4281)),
                PhoneNumber = "+2348034567890",
                OpeningTime = new TimeSpan(9, 0, 0),
                ClosingTime = new TimeSpan(23, 0, 0),
                PriceRange = PriceRange.Expensive,
                Specialties = new List<string> { "Abula", "Amala", "Gbegiri", "Ewedu", "Goat Meat", "Turkey" },
                IsVerified = true,
                CreatedByUserId = users[2].Id
            },
            new AmalaSpot
            {
                Id = Guid.NewGuid(),
                Name = "Surulere Amala House",
                Description = "Local favorite in Surulere serving hearty portions at affordable prices.",
                Address = "12 Adeniran Ogunsanya Street, Surulere, Lagos State",
                Location = GeometryFactory.CreatePoint(new Coordinate(3.3567, 6.4969)),
                PhoneNumber = "+2348045678901",
                OpeningTime = new TimeSpan(8, 0, 0),
                ClosingTime = new TimeSpan(20, 0, 0),
                PriceRange = PriceRange.Budget,
                Specialties = new List<string> { "Amala", "Ewedu", "Gbegiri", "Beef", "Ponmo" },
                IsVerified = false,
                CreatedByUserId = users[0].Id
            }
        };
    }

    private static List<AmalaSpot> GetOyoSpots(List<User> users)
    {
        return new List<AmalaSpot>
        {
            new AmalaSpot
            {
                Id = Guid.NewGuid(),
                Name = "Iya Agba Amala Ibadan",
                Description = "Authentic Ibadan-style amala with traditional preparation methods passed down through generations.",
                Address = "15 Dugbe Market Road, Ibadan, Oyo State",
                Location = GeometryFactory.CreatePoint(new Coordinate(3.9470, 7.3775)),
                PhoneNumber = "+2348056789012",
                OpeningTime = new TimeSpan(7, 0, 0),
                ClosingTime = new TimeSpan(21, 0, 0),
                PriceRange = PriceRange.Budget,
                Specialties = new List<string> { "Amala Isu", "Ewedu", "Gbegiri", "Bush Meat", "Snail" },
                IsVerified = true,
                CreatedByUserId = users[1].Id
            },
            new AmalaSpot
            {
                Id = Guid.NewGuid(),
                Name = "Bodija Amala Spot",
                Description = "Popular spot near University of Ibadan, serving students and locals with quality amala.",
                Address = "23 Bodija Market, Ibadan, Oyo State",
                Location = GeometryFactory.CreatePoint(new Coordinate(3.9180, 7.4340)),
                PhoneNumber = "+2348067890123",
                OpeningTime = new TimeSpan(8, 30, 0),
                ClosingTime = new TimeSpan(22, 0, 0),
                PriceRange = PriceRange.Budget,
                Specialties = new List<string> { "Amala", "Ewedu", "Okra Soup", "Fish", "Meat" },
                IsVerified = true,
                CreatedByUserId = users[2].Id
            }
        };
    }

    private static List<AmalaSpot> GetOgunSpots(List<User> users)
    {
        return new List<AmalaSpot>
        {
            new AmalaSpot
            {
                Id = Guid.NewGuid(),
                Name = "Abeokuta Amala Palace",
                Description = "Traditional amala house in the heart of Abeokuta, known for its rich flavors and cultural ambiance.",
                Address = "8 Ake Palace Road, Abeokuta, Ogun State",
                Location = GeometryFactory.CreatePoint(new Coordinate(3.3515, 7.1475)),
                PhoneNumber = "+2348078901234",
                OpeningTime = new TimeSpan(9, 0, 0),
                ClosingTime = new TimeSpan(20, 0, 0),
                PriceRange = PriceRange.Moderate,
                Specialties = new List<string> { "Amala", "Ewedu", "Gbegiri", "Goat Meat", "Chicken" },
                IsVerified = true,
                CreatedByUserId = users[0].Id
            }
        };
    }

    private static List<AmalaSpot> GetFCTSpots(List<User> users)
    {
        return new List<AmalaSpot>
        {
            new AmalaSpot
            {
                Id = Guid.NewGuid(),
                Name = "Abuja Yoruba Kitchen",
                Description = "Bringing authentic Yoruba cuisine to the capital city. Popular among civil servants and visitors.",
                Address = "45 Gimbiya Street, Area 11, Garki, FCT Abuja",
                Location = GeometryFactory.CreatePoint(new Coordinate(7.4951, 9.0579)),
                PhoneNumber = "+2348089012345",
                OpeningTime = new TimeSpan(8, 0, 0),
                ClosingTime = new TimeSpan(21, 30, 0),
                PriceRange = PriceRange.Moderate,
                Specialties = new List<string> { "Amala", "Ewedu", "Gbegiri", "Assorted Meat", "Fish" },
                IsVerified = true,
                CreatedByUserId = users[3].Id
            },
            new AmalaSpot
            {
                Id = Guid.NewGuid(),
                Name = "Wuse Market Amala Joint",
                Description = "Busy spot in Wuse Market serving quick and delicious amala to market traders and shoppers.",
                Address = "Wuse Market, Zone 5, FCT Abuja",
                Location = GeometryFactory.CreatePoint(new Coordinate(7.4898, 9.0765)),
                PhoneNumber = "+2348090123456",
                OpeningTime = new TimeSpan(7, 0, 0),
                ClosingTime = new TimeSpan(19, 0, 0),
                PriceRange = PriceRange.Budget,
                Specialties = new List<string> { "Amala", "Ewedu", "Okra Soup", "Beef", "Ponmo" },
                IsVerified = false,
                CreatedByUserId = users[1].Id
            }
        };
    }

    private static List<AmalaSpot> GetOsunSpots(List<User> users)
    {
        return new List<AmalaSpot>
        {
            new AmalaSpot
            {
                Id = Guid.NewGuid(),
                Name = "Osogbo Traditional Amala",
                Description = "Cultural heritage spot serving traditional amala in the spiritual city of Osogbo.",
                Address = "12 Oja Oba Market, Osogbo, Osun State",
                Location = GeometryFactory.CreatePoint(new Coordinate(4.5560, 7.7719)),
                PhoneNumber = "+2348101234567",
                OpeningTime = new TimeSpan(8, 0, 0),
                ClosingTime = new TimeSpan(20, 0, 0),
                PriceRange = PriceRange.Budget,
                Specialties = new List<string> { "Amala", "Ewedu", "Gbegiri", "Local Fish", "Bush Meat" },
                IsVerified = true,
                CreatedByUserId = users[2].Id
            }
        };
    }

    private static List<AmalaSpot> GetAdditionalTestSpots(List<User> users)
    {
        return new List<AmalaSpot>
        {
            new AmalaSpot
            {
                Id = Guid.NewGuid(),
                Name = "Test Amala Spot 1",
                Description = "Test spot for development and testing purposes.",
                Address = "Test Address 1, Lagos State",
                Location = GeometryFactory.CreatePoint(new Coordinate(3.4000, 6.5000)),
                PhoneNumber = "+2348112345678",
                OpeningTime = new TimeSpan(9, 0, 0),
                ClosingTime = new TimeSpan(18, 0, 0),
                PriceRange = PriceRange.Budget,
                Specialties = new List<string> { "Amala", "Test Soup" },
                IsVerified = false,
                CreatedByUserId = users[0].Id
            }
        };
    }

    private static async Task SeedReviews(AmalaSpotContext context, List<AmalaSpot> spots, List<User> users)
    {
        var reviews = new List<Review>();
        var random = new Random(42); // Fixed seed for consistent test data

        foreach (var spot in spots.Take(8)) // Add reviews to first 8 spots
        {
            var reviewCount = random.Next(2, 6); // 2-5 reviews per spot
            var reviewers = users.OrderBy(x => random.Next()).Take(reviewCount).ToList();

            foreach (var reviewer in reviewers)
            {
                var rating = random.Next(3, 6); // Ratings between 3-5 for realistic data
                var comments = GetRandomComment(rating, spot.Name);

                reviews.Add(new Review
                {
                    Id = Guid.NewGuid(),
                    SpotId = spot.Id,
                    UserId = reviewer.Id,
                    Rating = rating,
                    Comment = comments,
                    CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 365)),
                    IsModerated = true,
                    IsHidden = false
                });
            }
        }

        foreach (var review in reviews)
        {
            ValidateEntity(review);
        }

        await context.Reviews.AddRangeAsync(reviews);
        await context.SaveChangesAsync();
    }

    private static string GetRandomComment(int rating, string spotName)
    {
        var positiveComments = new[]
        {
            "Excellent amala! The taste is authentic and the service is great.",
            "Best amala in the area. Fresh ingredients and good portions.",
            "Love coming here. The ewedu is always fresh and the meat is well seasoned.",
            "Authentic Yoruba cuisine. Reminds me of home cooking.",
            "Great value for money. Clean environment and friendly staff.",
            "The abula here is perfect. Good combination of all three soups.",
            "Fresh and tasty. The ponmo is well prepared.",
            "Consistent quality every time I visit. Highly recommended."
        };

        var moderateComments = new[]
        {
            "Good food but can get crowded during lunch hours.",
            "Decent amala, though sometimes the wait time is long.",
            "Nice place but parking can be challenging.",
            "Food is good but could use more seasoning sometimes.",
            "Average experience. Food is okay but nothing exceptional.",
            "Good location and clean but food could be better."
        };

        return rating >= 4 ? positiveComments[new Random().Next(positiveComments.Length)] 
                          : moderateComments[new Random().Next(moderateComments.Length)];
    }

    private static async Task UpdateCalculatedFields(AmalaSpotContext context)
    {
        var spots = await context.AmalaSpots.Include(s => s.Reviews).ToListAsync();

        foreach (var spot in spots)
        {
            if (spot.Reviews.Any())
            {
                spot.AverageRating = (decimal)spot.Reviews.Average(r => r.Rating);
                spot.ReviewCount = spot.Reviews.Count;
            }
            else
            {
                spot.AverageRating = 0;
                spot.ReviewCount = 0;
            }
            spot.UpdatedAt = DateTime.UtcNow;
        }

        await context.SaveChangesAsync();
    }

    private static async Task ValidateDataIntegrity(AmalaSpotContext context)
    {

        var orphanedReviews = await context.Reviews
            .Where(r => !context.AmalaSpots.Any(s => s.Id == r.SpotId) || 
                       !context.Users.Any(u => u.Id == r.UserId))
            .CountAsync();

        if (orphanedReviews > 0)
        {
            throw new InvalidOperationException($"Found {orphanedReviews} orphaned reviews");
        }

        var invalidLocationSpots = await context.AmalaSpots
            .Where(s => s.Location == null)
            .CountAsync();

        if (invalidLocationSpots > 0)
        {
            throw new InvalidOperationException($"Found {invalidLocationSpots} spots with invalid locations");
        }

        var invalidEmailUsers = await context.Users
            .Where(u => !u.Email.Contains("@"))
            .CountAsync();

        if (invalidEmailUsers > 0)
        {
            throw new InvalidOperationException($"Found {invalidEmailUsers} users with invalid email formats");
        }
    }

    private static async Task ClearExistingData(AmalaSpotContext context)
    {

        context.Reviews.RemoveRange(context.Reviews);
        context.AmalaSpots.RemoveRange(context.AmalaSpots);
        context.Users.RemoveRange(context.Users);
        
        await context.SaveChangesAsync();
    }

    private static async Task<List<User>> SeedMinimalData(AmalaSpotContext context)
    {
        var users = TestDataSets.Minimal.GetUsers();
        
        foreach (var user in users)
        {
            ValidateEntity(user);
        }

        await context.Users.AddRangeAsync(users);
        await context.SaveChangesAsync();
        
        return users;
    }

    private static async Task<List<User>> SeedIntegrationData(AmalaSpotContext context)
    {
        var users = TestDataSets.Integration.GetUsers();
        
        foreach (var user in users)
        {
            ValidateEntity(user);
        }

        await context.Users.AddRangeAsync(users);
        await context.SaveChangesAsync();
        
        return users;
    }

    private static async Task<List<User>> SeedPerformanceData(AmalaSpotContext context, SeederConfiguration config)
    {
        var users = TestDataSets.Performance.GetUsers(config.PerformanceUserCount);

        var sampleUsers = users.Take(10);
        foreach (var user in sampleUsers)
        {
            ValidateEntity(user);
        }

        await context.Users.AddRangeAsync(users);
        await context.SaveChangesAsync();
        
        return users;
    }

    private static async Task<List<User>> SeedEdgeCaseData(AmalaSpotContext context)
    {
        var users = TestDataSets.EdgeCases.GetValidUsers();
        
        foreach (var user in users)
        {
            ValidateEntity(user);
        }

        await context.Users.AddRangeAsync(users);
        await context.SaveChangesAsync();
        
        return users;
    }

    private static void ValidateEntity<T>(T entity) where T : class
    {
        var validationContext = new ValidationContext(entity);
        var validationResults = new List<ValidationResult>();
        
        if (!Validator.TryValidateObject(entity, validationContext, validationResults, true))
        {
            var errors = string.Join(", ", validationResults.Select(vr => vr.ErrorMessage));
            throw new ValidationException($"Validation failed for {typeof(T).Name}: {errors}");
        }
    }
}