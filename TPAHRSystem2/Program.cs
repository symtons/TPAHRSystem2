using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Serilog;
using TPAHRSystemSimple.Data;
using TPAHRSystemSimple.Services;

var builder = WebApplication.CreateBuilder(args);

// =============================================================================
// LOGGING CONFIGURATION
// =============================================================================

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/tpa-hr-api-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// =============================================================================
// SERVICES CONFIGURATION
// =============================================================================

// Add controllers
builder.Services.AddControllers();

// Add API documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "TPA HR System API",
        Version = "v1.0",
        Description = "Simple HR Management System API with Session-based Authentication",
        Contact = new OpenApiContact
        {
            Name = "TPA HR System",
            Email = "support@tpahr.com"
        }
    });

    // Add Bearer token authentication to Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Session-based authentication. Enter your session token (without 'Bearer' prefix)",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "Session Token"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
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
            Array.Empty<string>()
        }
    });

    // Enable annotations for Swagger documentation
    c.EnableAnnotations();
});

// =============================================================================
// DATABASE CONFIGURATION
// =============================================================================

builder.Services.AddDbContext<TPADbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// =============================================================================
// BUSINESS SERVICES (Simple classes, no interfaces)
// =============================================================================

builder.Services.AddScoped<AuthService>();
// Note: Dashboard and Menu logic is handled directly in controllers for simplicity

// =============================================================================
// CORS CONFIGURATION (Fixed for credentials)
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
            .AllowCredentials()   // This requires specific origins, not AllowAnyOrigin
            .WithExposedHeaders("Authorization", "X-Session-Token");
    });

    // Development only - very permissive (no credentials)
    options.AddPolicy("DevelopmentCORS", policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

var app = builder.Build();

// =============================================================================
// MIDDLEWARE PIPELINE (Order is important!)
// =============================================================================

// Enable CORS FIRST (before other middleware)
app.UseCors("AllowReactApp");

// Enable Swagger in all environments for now
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "TPA HR System API v1");
    c.RoutePrefix = string.Empty; // Serve Swagger at root (https://localhost:7169/)
    c.DocumentTitle = "TPA HR System API Documentation";
    c.DefaultModelsExpandDepth(-1); // Hide schemas section by default
});

// Enable HTTPS redirection
app.UseHttpsRedirection();

// Add global OPTIONS handler for CORS preflight
app.Use(async (context, next) =>
{
    if (context.Request.Method == "OPTIONS")
    {
        context.Response.Headers.Add("Access-Control-Allow-Origin", "http://localhost:3000");
        context.Response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
        context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization, X-Session-Token");
        context.Response.Headers.Add("Access-Control-Allow-Credentials", "true");
        context.Response.StatusCode = 200;
        return;
    }
    await next();
});

// Add request logging middleware
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogInformation($"🌐 {context.Request.Method} {context.Request.Path}");

    await next();

    logger.LogInformation($"📤 Response: {context.Response.StatusCode}");
});

// Map controllers
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
    database = "Connected" // TODO: Add actual DB health check
}).WithTags("Health");

app.MapGet("/api/info", () => new
{
    name = "TPA HR Management System API",
    version = "1.0.0",
    description = "Simple HR Management System with Session-based Authentication",
    features = new[]
    {
        "✅ Session-based Authentication",
        "✅ Employee Management",
        "✅ Onboarding Workflow",
        "✅ Role-based Access Control",
        "✅ Dashboard Analytics",
        "🔄 Direct Database Integration"
    },
    endpoints = new
    {
        authentication = "/api/auth",
        swagger = "/swagger"
    },
    frontend_compatibility = "React (localhost:3000)",
    database = "SQL Server (existing TPAHRSystem database)"
}).WithTags("Info");

// =============================================================================
// STARTUP MESSAGES
// =============================================================================

Console.WriteLine("🚀 TPA HR System API (Simple Edition) Starting...");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine("📋 Features:");
Console.WriteLine("   ✅ Simple single-project architecture");
Console.WriteLine("   ✅ Session-based authentication");
Console.WriteLine("   ✅ Existing SQL Server database integration");
Console.WriteLine("   ✅ React frontend compatibility");
Console.WriteLine("   ✅ Swagger UI documentation");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine("🌐 Endpoints:");
Console.WriteLine("   📖 Swagger UI: https://localhost:7169/");
Console.WriteLine("   🔐 Auth API: https://localhost:7169/api/auth");
Console.WriteLine("   📊 Dashboard API: https://localhost:7169/api/dashboard");
Console.WriteLine("   🧭 Menu API: https://localhost:7169/api/menu");
Console.WriteLine("   ❤️  Health: https://localhost:7169/health");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

app.Run();