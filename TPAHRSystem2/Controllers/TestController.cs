using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TPAHRSystemSimple.Data;
using TPAHRSystemSimple.Models;
using TPAHRSystemSimple.Services;

namespace TPAHRSystemSimple.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestController : ControllerBase
    {
        private readonly TPADbContext _context;
        private readonly AuthService _authService;
        private readonly ILogger<TestController> _logger;

        public TestController(TPADbContext context, AuthService authService, ILogger<TestController> logger)
        {
            _context = context;
            _authService = authService;
            _logger = logger;
        }

        /// <summary>
        /// Test API health and database connection
        /// </summary>
        [HttpGet("health")]
        public async Task<IActionResult> Health()
        {
            try
            {
                // Test database connection
                var canConnect = await _context.Database.CanConnectAsync();
                var userCount = 0;
                var employeeCount = 0;

                if (canConnect)
                {
                    userCount = await _context.Users.CountAsync();
                    employeeCount = await _context.Employees.CountAsync();
                }

                return Ok(new
                {
                    success = true,
                    message = "API is healthy",
                    timestamp = DateTime.UtcNow,
                    database = new
                    {
                        connected = canConnect,
                        userCount,
                        employeeCount
                    },
                    version = "1.0.0",
                    environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check failed");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Health check failed",
                    error = ex.Message,
                    timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// Test authenticated endpoint
        /// </summary>
        [HttpGet("auth-test")]
        public async Task<IActionResult> AuthTest()
        {
            try
            {
                var sessionToken = GetSessionToken();
                if (string.IsNullOrEmpty(sessionToken))
                {
                    return Unauthorized(new { success = false, message = "No session token provided" });
                }

                var user = await _authService.ValidateSessionAsync(sessionToken);
                if (user == null)
                {
                    return Unauthorized(new { success = false, message = "Invalid or expired session" });
                }

                return Ok(new
                {
                    success = true,
                    message = "Authentication test successful",
                    user = new
                    {
                        id = user.Id,
                        email = user.Email,
                        role = user.Role,
                        lastLogin = user.LastLogin
                    },
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Auth test failed");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Auth test failed",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Get database statistics
        /// </summary>
        [HttpGet("db-stats")]
        public async Task<IActionResult> DatabaseStats()
        {
            try
            {
                var stats = new
                {
                    users = await _context.Users.CountAsync(),
                    activeUsers = await _context.Users.CountAsync(u => u.IsActive),
                    employees = await _context.Employees.CountAsync(),
                    activeEmployees = await _context.Employees.CountAsync(e => e.Status == "Active"),
                    departments = await _context.Departments.CountAsync(),
                    activeSessions = await _context.UserSessions.CountAsync(s => s.IsActive && s.ExpiresAt > DateTime.UtcNow),
                    timestamp = DateTime.UtcNow
                };

                return Ok(ApiResponse<object>.SuccessResult(stats, "Database statistics retrieved"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get database stats");
                return StatusCode(500, ApiResponse<object>.ErrorResult("Failed to retrieve database statistics"));
            }
        }

        /// <summary>
        /// Create a test user with known password (Development only)
        /// </summary>
        [HttpPost("create-simple-test-user")]
        public async Task<IActionResult> CreateSimpleTestUser()
        {
            try
            {
                // Only allow in development
                if (!Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")!.Equals("Development", StringComparison.OrdinalIgnoreCase))
                {
                    return Forbid("This endpoint is only available in development environment");
                }

                var testEmail = "testuser@tpa.com";
                var testPassword = "Test123!";

                var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == testEmail);

                if (existingUser != null)
                {
                    return Ok(new
                    {
                        success = true,
                        message = "Test user already exists",
                        credentials = new { email = testEmail, password = testPassword }
                    });
                }

                var salt = AuthService.GenerateSalt();
                var passwordHash = AuthService.ComputeHash(testPassword, salt);

                var testUser = new User
                {
                    Email = testEmail,
                    PasswordHash = passwordHash,
                    Salt = salt,
                    Role = "Admin",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Users.Add(testUser);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "New test user created successfully",
                    credentials = new
                    {
                        email = testEmail,
                        password = testPassword
                    },
                    note = "Use these exact credentials to login"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create simple test user");
                return StatusCode(500, new { success = false, message = "Failed to create test user", error = ex.Message });
            }
        }

        private string? GetSessionToken()
        {
            var authHeader = Request.Headers.Authorization.FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
            {
                return authHeader["Bearer ".Length..].Trim();
            }

            var sessionHeader = Request.Headers["X-Session-Token"].FirstOrDefault();
            if (!string.IsNullOrEmpty(sessionHeader))
            {
                return sessionHeader;
            }

            return null;
        }
    }
}