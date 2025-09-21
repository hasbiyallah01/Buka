using AmalaSpotLocator.Core.Domain.Entities;
using AmalaSpotLocator.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Text.Json;

namespace AmalaSpotLocator.Infrastructure
{
    public class AmalaSpotContext : DbContext
    {
        public AmalaSpotContext(DbContextOptions<AmalaSpotContext> options) : base(options)
        {
        }

        public DbSet<AmalaSpot> AmalaSpots { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Review> Reviews { get; set; }
        public DbSet<SpotCandidate> SpotCandidates { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.HasPostgresExtension("postgis");

            modelBuilder.Entity<AmalaSpot>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Location)
                    .HasColumnType("geography (point)")
                    .IsRequired();

                entity.Property(e => e.AverageRating)
                    .HasColumnType("decimal(3,2)")
                    .HasDefaultValue(0);

                entity.Property(e => e.Specialties)
                    .HasColumnType("jsonb")
                    .HasConversion(
                        v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                        v => System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<string>())
                    .Metadata.SetValueComparer(new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<List<string>>(
                        (c1, c2) => c1!.SequenceEqual(c2!),
                        c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                        c => c.ToList()));
                    
                entity.HasIndex(e => e.Location)
                    .HasMethod("gist");

                entity.HasIndex(e => e.AverageRating);
                entity.HasIndex(e => e.PriceRange);
                entity.HasIndex(e => e.IsVerified);
                entity.HasIndex(e => e.CreatedAt);

                entity.HasOne(e => e.CreatedByUser)
                    .WithMany(u => u.CreatedSpots)
                    .HasForeignKey(e => e.CreatedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Email)
                    .IsRequired()
                    .HasMaxLength(255);

                entity.Property(e => e.PreferredMaxBudget)
                    .HasColumnType("decimal(10,2)");

                entity.Property(e => e.PreferredMinRating)
                    .HasColumnType("decimal(3,2)");

                entity.HasIndex(e => e.Email)
                    .IsUnique();

                entity.HasIndex(e => e.CreatedAt);
                entity.HasIndex(e => e.IsActive);
            });

            modelBuilder.Entity<Review>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.HasOne(e => e.Spot)
                    .WithMany(s => s.Reviews)
                    .HasForeignKey(e => e.SpotId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.User)
                    .WithMany(u => u.Reviews)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.SpotId);
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.Rating);
                entity.HasIndex(e => e.CreatedAt);
                entity.HasIndex(e => new { e.SpotId, e.UserId })
                    .IsUnique(); 
            });


            modelBuilder.Entity<SpotCandidate>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Location)
                    .HasColumnType("geography (point)");

                entity.Property(e => e.ConfidenceScore)
                    .HasColumnType("decimal(3,2)");

                entity.Property(e => e.QualityScore)
                    .HasColumnType("decimal(3,2)");

                entity.Property(e => e.Specialties)
                    .HasColumnType("jsonb")
                    .HasConversion(
                        v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                        v => System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<string>())
                    .Metadata.SetValueComparer(new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<List<string>>(
                        (c1, c2) => c1!.SequenceEqual(c2!),
                        c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                        c => c.ToList()));

                entity.Property(e => e.SourceData)
                    .HasColumnType("jsonb")
                    .HasConversion(
                        v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                        v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new Dictionary<string, object>());

                entity.HasIndex(e => e.Location)
                    .HasMethod("gist");

                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.Source);
                entity.HasIndex(e => e.ConfidenceScore);
                entity.HasIndex(e => e.QualityScore);
                entity.HasIndex(e => e.DiscoveredAt);

                entity.HasOne(e => e.ExistingSpot)
                    .WithMany()
                    .HasForeignKey(e => e.ExistingSpotId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<AmalaSpot>()
                .Property(e => e.PriceRange)
                .HasConversion<int>();

            modelBuilder.Entity<User>()
                .Property(e => e.Role)
                .HasConversion<int>();

            modelBuilder.Entity<SpotCandidate>()
                .Property(e => e.EstimatedPriceRange)
                .HasConversion<int>();

            modelBuilder.Entity<SpotCandidate>()
                .Property(e => e.Source)
                .HasConversion<int>();

            modelBuilder.Entity<SpotCandidate>()
                .Property(e => e.Status)
                .HasConversion<int>();
        }
    }
}
