# Amala Spot Locator API

A .NET 8 Web API for discovering and locating amala restaurants across Nigeria with AI-powered conversational interfaces.

## Project Structure

```
AmalaSpotLocator/
├── Agents/                 # AI agent implementations
├── Configuration/          # Configuration classes
├── Controllers/           # API controllers
├── Data/                  # Entity Framework DbContext
├── Interfaces/            # Service interfaces
├── Middleware/            # Custom middleware
├── Models/                # Entity models
├── Services/              # Business logic services
├── Program.cs             # Application entry point
└── appsettings.json       # Configuration settings
```

## Dependencies

- .NET 8.0
- Entity Framework Core 8.0.15
- PostgreSQL with PostGIS (Npgsql.EntityFrameworkCore.PostgreSQL 8.0.4)
- OpenAI SDK 2.1.0
- Google Maps API (GoogleApi 5.0.0)
- JWT Authentication (Microsoft.AspNetCore.Authentication.JwtBearer 8.0.15)

## Configuration

Update the following settings in `appsettings.json`:

1. **Database Connection**: Update the PostgreSQL connection string
2. **Google Maps API**: Add your Google Maps API keys
3. **OpenAI API**: Add your OpenAI API key
4. **JWT Settings**: Configure JWT secret key and settings

## Getting Started

1. Install PostgreSQL with PostGIS extension
2. Update connection strings in appsettings files
3. Add your API keys for Google Maps and OpenAI
4. Run the application:
   ```bash
   dotnet run
   ```