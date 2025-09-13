
using System;
using AmalaSpotLocator.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NetTopologySuite.Geometries;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AmalaSpotLocator.Migrations
{
    [DbContext(typeof(AmalaSpotContext))]
    partial class AmalaSpotContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.15")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.HasPostgresExtension(modelBuilder, "postgis");
            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("AmalaSpotLocator.Models.AmalaSpot", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("Address")
                        .IsRequired()
                        .HasMaxLength(500)
                        .HasColumnType("character varying(500)");

                    b.Property<decimal>("AverageRating")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("decimal(3,2)")
                        .HasDefaultValue(0m);

                    b.Property<TimeSpan?>("ClosingTime")
                        .HasColumnType("interval");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<Guid?>("CreatedByUserId")
                        .HasColumnType("uuid");

                    b.Property<string>("Description")
                        .HasMaxLength(1000)
                        .HasColumnType("character varying(1000)");

                    b.Property<bool>("IsVerified")
                        .HasColumnType("boolean");

                    b.Property<Point>("Location")
                        .IsRequired()
                        .HasColumnType("geography (point)");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)");

                    b.Property<TimeSpan?>("OpeningTime")
                        .HasColumnType("interval");

                    b.Property<string>("PhoneNumber")
                        .HasMaxLength(20)
                        .HasColumnType("character varying(20)");

                    b.Property<int>("PriceRange")
                        .HasColumnType("integer");

                    b.Property<int>("ReviewCount")
                        .HasColumnType("integer");

                    b.Property<string>("Specialties")
                        .IsRequired()
                        .HasColumnType("jsonb");

                    b.Property<DateTime>("UpdatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.HasKey("Id");

                    b.HasIndex("AverageRating");

                    b.HasIndex("CreatedAt");

                    b.HasIndex("CreatedByUserId");

                    b.HasIndex("IsVerified");

                    b.HasIndex("Location");

                    NpgsqlIndexBuilderExtensions.HasMethod(b.HasIndex("Location"), "gist");

                    b.HasIndex("PriceRange");

                    b.ToTable("AmalaSpots");
                });

            modelBuilder.Entity("AmalaSpotLocator.Models.Review", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("Comment")
                        .HasMaxLength(1000)
                        .HasColumnType("character varying(1000)");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<bool>("IsHidden")
                        .HasColumnType("boolean");

                    b.Property<bool>("IsModerated")
                        .HasColumnType("boolean");

                    b.Property<int>("Rating")
                        .HasColumnType("integer");

                    b.Property<Guid>("SpotId")
                        .HasColumnType("uuid");

                    b.Property<DateTime>("UpdatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid");

                    b.HasKey("Id");

                    b.HasIndex("CreatedAt");

                    b.HasIndex("Rating");

                    b.HasIndex("SpotId");

                    b.HasIndex("UserId");

                    b.HasIndex("SpotId", "UserId")
                        .IsUnique();

                    b.ToTable("Reviews");
                });

            modelBuilder.Entity("AmalaSpotLocator.Models.User", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Email")
                        .IsRequired()
                        .HasMaxLength(255)
                        .HasColumnType("character varying(255)");

                    b.Property<string>("FirstName")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<bool>("IsActive")
                        .HasColumnType("boolean");

                    b.Property<DateTime?>("LastLoginAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("LastName")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<string>("PasswordHash")
                        .IsRequired()
                        .HasMaxLength(255)
                        .HasColumnType("character varying(255)");

                    b.Property<string>("PhoneNumber")
                        .HasMaxLength(20)
                        .HasColumnType("character varying(20)");

                    b.Property<string>("PreferredLanguage")
                        .IsRequired()
                        .HasMaxLength(10)
                        .HasColumnType("character varying(10)");

                    b.Property<decimal?>("PreferredMaxBudget")
                        .HasColumnType("decimal(10,2)");

                    b.Property<decimal?>("PreferredMinRating")
                        .HasColumnType("decimal(3,2)");

                    b.Property<int>("Role")
                        .HasColumnType("integer");

                    b.Property<DateTime>("UpdatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.HasKey("Id");

                    b.HasIndex("CreatedAt");

                    b.HasIndex("Email")
                        .IsUnique();

                    b.HasIndex("IsActive");

                    b.ToTable("Users");
                });

            modelBuilder.Entity("AmalaSpotLocator.Models.AmalaSpot", b =>
                {
                    b.HasOne("AmalaSpotLocator.Models.User", "CreatedByUser")
                        .WithMany("CreatedSpots")
                        .HasForeignKey("CreatedByUserId")
                        .OnDelete(DeleteBehavior.SetNull);

                    b.Navigation("CreatedByUser");
                });

            modelBuilder.Entity("AmalaSpotLocator.Models.Review", b =>
                {
                    b.HasOne("AmalaSpotLocator.Models.AmalaSpot", "Spot")
                        .WithMany("Reviews")
                        .HasForeignKey("SpotId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("AmalaSpotLocator.Models.User", "User")
                        .WithMany("Reviews")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Spot");

                    b.Navigation("User");
                });

            modelBuilder.Entity("AmalaSpotLocator.Models.AmalaSpot", b =>
                {
                    b.Navigation("Reviews");
                });

            modelBuilder.Entity("AmalaSpotLocator.Models.User", b =>
                {
                    b.Navigation("CreatedSpots");

                    b.Navigation("Reviews");
                });
#pragma warning restore 612, 618
        }
    }
}
