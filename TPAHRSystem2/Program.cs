// =============================================================================
// TPAHRSystem2/Program.cs - FIXED CORS VERSION
// File: TPAHRSystem2/Program.cs (Replace existing)
// This fixes the CORS "Missing Allow Origin" issue
// =============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Serilog;
using TPAHRSystemSimple.Data;
using TPAHRSystemSimple.Services;

var builder = WebApplication.CreateBuilder(args);

// =============================================================================
// BASIC SERVICES CONFIGURATION
// =============================================================================

// Add controllers with minimal configuration
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Prevent circular reference issues
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

// Database context
builder.Services.AddDbContext<TPADbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add services
builder.Services.AddScoped<AuthService>();

// =============================================================================
// FIXED CORS CONFIGURATION
// =============================================================================

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        policy.WithOrigins(
                "http://localhost:3000",
                "https://localhost:3000",
                "http://127.0.0.1:3000",
                "https://127.0.0.1:3000"
            )
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials()
            .WithExposedHeaders("Authorization", "X-Session-Token", "Content-Type");
    });

    // Development-only policy for testing
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// =============================================================================
// SWAGGER CONFIGURATION
// =============================================================================

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "TPA HR System API",
        Version = "v1.0",
        Description = "HR Management System API"
    });

    // Add Bearer token authentication
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement()
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new List<string>()
        }
    });
});

var app = builder.Build();

// =============================================================================
// MIDDLEWARE PIPELINE (ORDER IS CRITICAL!)
// =============================================================================

// 1. CORS must be first
app.UseCors("AllowReactApp");

// 2. Enable Swagger
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "TPA HR System API v1");
    c.RoutePrefix = string.Empty; // Serve Swagger at root
});

// 3. HTTPS redirection
app.UseHttpsRedirection();

// 4. Add explicit OPTIONS handling for CORS preflight
app.Use(async (context, next) =>
{
    if (context.Request.Method == "OPTIONS")
    {
        context.Response.StatusCode = 200;
        context.Response.Headers.Add("Access-Control-Allow-Origin", "http://localhost:3000");
        context.Response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
        context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization, X-Session-Token");
        context.Response.Headers.Add("Access-Control-Allow-Credentials", "true");
        await context.Response.WriteAsync("");
        return;
    }
    await next();
});

// 5. Request logging
app.Use(async (context, next) =>
{
    Console.WriteLine($"🌐 {context.Request.Method} {context.Request.Path} from {context.Request.Headers.Origin}");
    await next();
    Console.WriteLine($"📤 Response: {context.Response.StatusCode}");
});

// 6. Map controllers
app.MapControllers();

// =============================================================================
// HEALTH CHECK ENDPOINTS
// =============================================================================

app.MapGet("/health", () => new
{
    status = "healthy",
    timestamp = DateTime.UtcNow,
    service = "TPA HR System API",
    version = "1.0.0",
    cors = "enabled"
}).WithTags("Health");

app.MapGet("/api/test", () => new
{
    success = true,
    message = "API is working",
    timestamp = DateTime.UtcNow,
    port = 7169,
    cors = "configured"
}).WithTags("Test");

// CORS test endpoint
app.MapGet("/api/cors-test", () => new
{
    success = true,
    message = "CORS is working",
    timestamp = DateTime.UtcNow,
    allowedOrigins = new[] { "http://localhost:3000", "https://localhost:3000" }
}).WithTags("CORS");

// =============================================================================
// STARTUP
// =============================================================================

Console.WriteLine("🚀 TPA HR System API Starting...");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine("📖 Swagger UI: https://localhost:7169/");
Console.WriteLine("❤️  Health: https://localhost:7169/health");
Console.WriteLine("🧪 Test: https://localhost:7169/api/test");
Console.WriteLine("🌐 CORS Test: https://localhost:7169/api/cors-test");
Console.WriteLine("🔗 React Origin: http://localhost:3000 ✅ ALLOWED");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

app.Run();