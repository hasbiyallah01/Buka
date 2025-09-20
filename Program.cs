using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using AmalaSpotLocator.Configuration;
using AmalaSpotLocator.Data;
using AmalaSpotLocator.Middleware;
using AmalaSpotLocator.Core.Applications.Services;
using AmalaSpotLocator.Core.Applications.Interfaces.Services;
using AmalaSpotLocator.Interfaces;
using AmalaSpotLocator.Agents;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();
builder.Services.Configure<GoogleMapsSettings>(
    builder.Configuration.GetSection(GoogleMapsSettings.SectionName));
builder.Services.Configure<OpenAISettings>(
    builder.Configuration.GetSection(OpenAISettings.SectionName));
builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection(JwtSettings.SectionName));
builder.Services.Configure<SecuritySettings>(
    builder.Configuration.GetSection(SecuritySettings.SectionName));


builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals;
    });

builder.Services.AddDbContext<AmalaSpotContext>(options =>
{
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnections"),
        x => x.UseNetTopologySuite());
});


var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>();
if (jwtSettings?.SecretKey != null)
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidAudience = jwtSettings.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey))
            };
        });
}
builder.Services.AddCors(cors =>
{
    cors.AddPolicy("AmalaSpot", pol =>
    {
        pol.WithOrigins(
            "http://localhost:3000/")
           .AllowAnyHeader()
           .AllowAnyMethod()
           .AllowCredentials();
    });
});
builder.Services.AddAuthorization(options =>
{

    options.AddPolicy("RequireUser", policy =>
        policy.RequireAuthenticatedUser());

    options.AddPolicy("RequireModerator", policy =>
        policy.RequireRole("Moderator", "Admin"));

    options.AddPolicy("RequireAdmin", policy =>
        policy.RequireRole("Admin"));
});

builder.Services.AddDataProtection();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Amala Spot Locator API",
        Version = "v1.0.0",
        Description = "API documentation for Amala Spot Locator"
    });

    c.AddSecurityDefinition("Bearer", new()
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter 'Bearer {your-token}'"
    });

    c.AddSecurityRequirement(new()
    {
        {
            new()
            {
                Reference = new()
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});


builder.Services.AddHttpClient();

builder.Services.AddMemoryCache();

builder.Services.AddScoped<IGeospatialService, GeospatialService>();
builder.Services.AddScoped<ISpotService, SpotService>();
builder.Services.AddScoped<IVoiceProcessingService, VoiceProcessingService>();
builder.Services.AddScoped<IGoogleMapsService, GoogleMapsService> ();
builder.Services.AddScoped<IMapService, MapService> ();
builder.Services.AddScoped<IBusynessService, BusynessService>();
builder.Services.AddScoped<ISpotMappingService, SpotMappingService>();
builder.Services.AddScoped<IHeatmapService, HeatmapService>();

builder.Services.AddScoped <IAuthenticationService, AuthenticationService > ();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped <IReviewService, ReviewService > ();

builder.Services.AddSingleton <IChatSessionService, ChatSessionService>();
builder.Services.AddSingleton<IRateLimitingService, RateLimitingService>();

builder.Services.AddScoped<INLUAgent, NLUAgent > ();
builder.Services.AddScoped<IQueryAgent, QueryAgent > ();
builder.Services.AddScoped<IResponseAgent, ResponseAgent > ();
builder.Services.AddScoped<IAgentOrchestrator, AgentOrchestrator > ();

builder.Services.Configure<DiscoverySettings>(
    builder.Configuration.GetSection(DiscoverySettings.SectionName));
builder.Services.AddScoped<IWebScrapingService, WebScrapingService>();
builder.Services.AddScoped<ICandidateExtractionService, CandidateExtractionService>();
builder.Services.AddScoped<ISpotDiscoveryService, SpotDiscoveryService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.RoutePrefix = string.Empty;
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Amala Spot Locator API v1");
    });
}

app.UseHttpsRedirection();

app.UseMiddleware<SecurityHeadersMiddleware>();

app.UseMiddleware<RequestValidationMiddleware>();
app.UseMiddleware<InputSanitizationMiddleware>();

app.UseMiddleware<RateLimitingMiddleware>();

app.UseCors("SecureCorsPolicy");

app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<GlobalExceptionMiddleware>();

app.MapControllers();

app.Run();




