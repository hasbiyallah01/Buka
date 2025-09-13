using System.Collections.Generic;
using AmalaSpotLocator.Models;
using NetTopologySuite.Geometries;

namespace AmalaSpotLocator.Data;

public static class TestDataSets
{
    private static readonly GeometryFactory GeometryFactory = new(new PrecisionModel(), 4326);

    public static class Minimal
    {
        public static List<User> GetUsers()
        {
            return new List<User>
            {
                new User
                {
                    Id = new Guid("11111111-1111-1111-1111-111111111111"),
                    FirstName = "Test",
                    LastName = "User",
                    Email = "test@example.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("TestPass123!"),
                    Role = UserRole.User,
                    PreferredLanguage = "en",
                    IsActive = true
                }
            };
        }

        public static List<AmalaSpot> GetSpots(List<User> users)
        {
            return new List<AmalaSpot>
            {
                new AmalaSpot
                {
                    Id = new Guid("22222222-2222-2222-2222-222222222222"),
                    Name = "Test Amala Spot",
                    Description = "Test spot for unit testing",
                    Address = "Test Address, Lagos",
                    Location = GeometryFactory.CreatePoint(new Coordinate(3.4000, 6.5000)),
                    PhoneNumber = "+2348012345678",
                    OpeningTime = new TimeSpan(9, 0, 0),
                    ClosingTime = new TimeSpan(18, 0, 0),
                    PriceRange = PriceRange.Budget,
                    Specialties = new List<string> { "Amala", "Ewedu" },
                    IsVerified = true,
                    CreatedByUserId = users[0].Id
                }
            };
        }

        public static List<Review> GetReviews(List<AmalaSpot> spots, List<User> users)
        {
            return new List<Review>
            {
                new Review
                {
                    Id = new Guid("33333333-3333-3333-3333-333333333333"),
                    SpotId = spots[0].Id,
                    UserId = users[0].Id,
                    Rating = 4,
                    Comment = "Good test spot",
                    IsModerated = true
                }
            };
        }
    }

    public static class Performance
    {
        public static List<User> GetUsers(int count = 1000)
        {
            var users = new List<User>();
            var random = new Random(42);

            for (int i = 0; i < count; i++)
            {
                users.Add(new User
                {
                    Id = Guid.NewGuid(),
                    FirstName = $"User{i}",
                    LastName = $"Test{i}",
                    Email = $"user{i}@example.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("TestPass123!"),
                    PhoneNumber = $"+23480{random.Next(10000000, 99999999)}",
                    Role = UserRole.User,
                    PreferredLanguage = random.Next(2) == 0 ? "en" : "yo",
                    PreferredMaxBudget = random.Next(1000, 5000),
                    PreferredMinRating = random.Next(3, 5),
                    IsActive = true
                });
            }

            return users;
        }

        public static List<AmalaSpot> GetSpots(List<User> users, int count = 5000)
        {
            var spots = new List<AmalaSpot>();
            var random = new Random(42);
            var priceRanges = Enum.GetValues<PriceRange>();
            var specialties = new[] { "Amala", "Ewedu", "Gbegiri", "Okra Soup", "Efo Riro", "Fish", "Meat", "Chicken", "Ponmo" };

            var minLat = 6.4000;
            var maxLat = 6.7000;
            var minLng = 3.2000;
            var maxLng = 3.6000;

            for (int i = 0; i < count; i++)
            {
                var lat = minLat + (maxLat - minLat) * random.NextDouble();
                var lng = minLng + (maxLng - minLng) * random.NextDouble();

                spots.Add(new AmalaSpot
                {
                    Id = Guid.NewGuid(),
                    Name = $"Amala Spot {i + 1}",
                    Description = $"Performance test spot number {i + 1}",
                    Address = $"{random.Next(1, 999)} Test Street, Lagos",
                    Location = GeometryFactory.CreatePoint(new Coordinate(lng, lat)),
                    PhoneNumber = $"+23480{random.Next(10000000, 99999999)}",
                    OpeningTime = new TimeSpan(random.Next(6, 10), 0, 0),
                    ClosingTime = new TimeSpan(random.Next(18, 23), 0, 0),
                    PriceRange = priceRanges[random.Next(priceRanges.Length)],
                    Specialties = specialties.OrderBy(x => random.Next()).Take(random.Next(2, 6)).ToList(),
                    IsVerified = random.Next(10) < 7, // 70% verified
                    CreatedByUserId = users[random.Next(users.Count)].Id
                });
            }

            return spots;
        }

        public static List<Review> GetReviews(List<AmalaSpot> spots, List<User> users, int reviewsPerSpot = 5)
        {
            var reviews = new List<Review>();
            var random = new Random(42);
            var comments = new[]
            {
                "Great amala spot!",
                "Good food and service",
                "Average experience",
                "Could be better",
                "Excellent quality",
                "Fresh ingredients",
                "Quick service",
                "Value for money"
            };

            foreach (var spot in spots)
            {
                var reviewCount = random.Next(1, reviewsPerSpot + 1);
                var reviewers = users.OrderBy(x => random.Next()).Take(reviewCount);

                foreach (var reviewer in reviewers)
                {
                    reviews.Add(new Review
                    {
                        Id = Guid.NewGuid(),
                        SpotId = spot.Id,
                        UserId = reviewer.Id,
                        Rating = random.Next(2, 6),
                        Comment = comments[random.Next(comments.Length)],
                        CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 365)),
                        IsModerated = true,
                        IsHidden = false
                    });
                }
            }

            return reviews;
        }
    }

    public static class Integration
    {
        public static List<User> GetUsers()
        {
            return new List<User>
            {
                new User
                {
                    Id = new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                    FirstName = "Integration",
                    LastName = "Tester",
                    Email = "integration@example.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("IntegrationPass123!"),
                    PhoneNumber = "+2348012345678",
                    Role = UserRole.User,
                    PreferredLanguage = "en",
                    PreferredMaxBudget = 2000m,
                    PreferredMinRating = 3.5m,
                    IsActive = true
                },
                new User
                {
                    Id = new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                    FirstName = "Moderator",
                    LastName = "Test",
                    Email = "moderator@example.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("ModeratorPass123!"),
                    PhoneNumber = "+2348023456789",
                    Role = UserRole.Moderator,
                    PreferredLanguage = "yo",
                    IsActive = true
                }
            };
        }

        public static List<AmalaSpot> GetSpots(List<User> users)
        {
            return new List<AmalaSpot>
            {
                new AmalaSpot
                {
                    Id = new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                    Name = "Integration Test Spot 1",
                    Description = "First integration test spot with all features",
                    Address = "1 Integration Street, Lagos",
                    Location = GeometryFactory.CreatePoint(new Coordinate(3.4000, 6.5000)),
                    PhoneNumber = "+2348012345678",
                    OpeningTime = new TimeSpan(8, 0, 0),
                    ClosingTime = new TimeSpan(20, 0, 0),
                    PriceRange = PriceRange.Budget,
                    Specialties = new List<string> { "Amala", "Ewedu", "Gbegiri" },
                    IsVerified = true,
                    CreatedByUserId = users[0].Id
                },
                new AmalaSpot
                {
                    Id = new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                    Name = "Integration Test Spot 2",
                    Description = "Second integration test spot for filtering tests",
                    Address = "2 Integration Avenue, Lagos",
                    Location = GeometryFactory.CreatePoint(new Coordinate(3.4100, 6.5100)),
                    PhoneNumber = "+2348023456789",
                    OpeningTime = new TimeSpan(9, 0, 0),
                    ClosingTime = new TimeSpan(21, 0, 0),
                    PriceRange = PriceRange.Expensive,
                    Specialties = new List<string> { "Amala", "Okra Soup", "Fish" },
                    IsVerified = false,
                    CreatedByUserId = users[1].Id
                }
            };
        }

        public static List<Review> GetReviews(List<AmalaSpot> spots, List<User> users)
        {
            return new List<Review>
            {
                new Review
                {
                    Id = new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
                    SpotId = spots[0].Id,
                    UserId = users[0].Id,
                    Rating = 5,
                    Comment = "Excellent integration test review",
                    CreatedAt = DateTime.UtcNow.AddDays(-10),
                    IsModerated = true,
                    IsHidden = false
                },
                new Review
                {
                    Id = new Guid("ffffffff-ffff-ffff-ffff-ffffffffffff"),
                    SpotId = spots[0].Id,
                    UserId = users[1].Id,
                    Rating = 4,
                    Comment = "Good for testing purposes",
                    CreatedAt = DateTime.UtcNow.AddDays(-5),
                    IsModerated = true,
                    IsHidden = false
                },
                new Review
                {
                    Id = new Guid("gggggggg-gggg-gggg-gggg-gggggggggggg"),
                    SpotId = spots[1].Id,
                    UserId = users[0].Id,
                    Rating = 3,
                    Comment = "Average test spot",
                    CreatedAt = DateTime.UtcNow.AddDays(-3),
                    IsModerated = false,
                    IsHidden = false
                }
            };
        }
    }

    public static class EdgeCases
    {
        public static List<User> GetValidUsers()
        {
            return new List<User>
            {
                new User
                {
                    Id = Guid.NewGuid(),
                    FirstName = "Edge",
                    LastName = "Case",
                    Email = "edge@example.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("EdgePass123!"),
                    Role = UserRole.User,
                    PreferredLanguage = "en",
                    IsActive = true
                }
            };
        }

        public static List<AmalaSpot> GetEdgeCaseSpots(List<User> users)
        {
            return new List<AmalaSpot>
            {

                new AmalaSpot
                {
                    Id = Guid.NewGuid(),
                    Name = "Min",
                    Address = "1",
                    Location = GeometryFactory.CreatePoint(new Coordinate(3.0, 6.0)),
                    PriceRange = PriceRange.Budget,
                    Specialties = new List<string> { "A" },
                    CreatedByUserId = users[0].Id
                },

                new AmalaSpot
                {
                    Id = Guid.NewGuid(),
                    Name = new string('A', 200), // Max length name
                    Description = new string('B', 1000), // Max length description
                    Address = new string('C', 500), // Max length address
                    Location = GeometryFactory.CreatePoint(new Coordinate(180.0, 90.0)), // Max coordinates
                    PhoneNumber = "+2348012345678",
                    OpeningTime = new TimeSpan(0, 0, 0), // Midnight
                    ClosingTime = new TimeSpan(23, 59, 59), // Just before midnight
                    PriceRange = PriceRange.Expensive,
                    Specialties = Enumerable.Range(1, 10).Select(i => $"Specialty{i}").ToList(),
                    CreatedByUserId = users[0].Id
                }
            };
        }

        public static List<Review> GetEdgeCaseReviews(List<AmalaSpot> spots, List<User> users)
        {
            return new List<Review>
            {

                new Review
                {
                    Id = Guid.NewGuid(),
                    SpotId = spots[0].Id,
                    UserId = users[0].Id,
                    Rating = 1,
                    Comment = null, // No comment
                    IsModerated = true
                },

                new Review
                {
                    Id = Guid.NewGuid(),
                    SpotId = spots[1].Id,
                    UserId = users[0].Id,
                    Rating = 5,
                    Comment = new string('X', 1000), // Max length comment
                    IsModerated = false,
                    IsHidden = true
                }
            };
        }
    }
}