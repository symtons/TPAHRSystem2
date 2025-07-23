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
        /// Get database statistics (DATABASE-DRIVEN)
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

                    // Dashboard system stats
                    dashboardStats = await _context.DashboardStats.CountAsync(ds => ds.IsActive),
                    quickActions = await _context.QuickActions.CountAsync(qa => qa.IsActive),
                    menuItems = await _context.MenuItems.CountAsync(mi => mi.IsActive),
                    rolePermissions = await _context.RoleMenuPermissions.CountAsync(),
                    recentActivities = await _context.RecentActivities.CountAsync(),
                    activityTypes = await _context.ActivityTypes.CountAsync(at => at.IsActive),

                    timestamp = DateTime.UtcNow,
                    message = "All data from database tables"
                };

                return Ok(ApiResponse<object>.SuccessResult(stats, "Complete database statistics retrieved"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get database stats");
                return StatusCode(500, ApiResponse<object>.ErrorResult("Failed to retrieve database statistics"));
            }
        }

        /// <summary>
        /// Test database-driven dashboard functionality
        /// </summary>
        [HttpGet("dashboard-data/{role}")]
        public async Task<IActionResult> TestDashboardData(string role)
        {
            try
            {
                var user = await GetCurrentUserAsync();
                if (user == null)
                    return Unauthorized(new { success = false, message = "Authentication required" });

                // Test all database-driven dashboard components
                var stats = await _context.DashboardStats
                    .Where(s => s.IsActive &&
                               (s.ApplicableRoles == null || s.ApplicableRoles.Contains(role)))
                    .OrderBy(s => s.SortOrder)
                    .ToListAsync();

                var actions = await _context.QuickActions
                    .Where(qa => qa.IsActive &&
                                (qa.ApplicableRoles == null || qa.ApplicableRoles.Contains(role)))
                    .OrderBy(qa => qa.SortOrder)
                    .ToListAsync();

                var menuItems = await _context.MenuItems
                    .Where(m => m.IsActive && m.ParentId == null)
                    .Include(m => m.RolePermissions.Where(rp => rp.Role == role && rp.CanView))
                    .OrderBy(m => m.SortOrder)
                    .ToListAsync();

                var recentActivities = await _context.RecentActivities
                    .Include(ra => ra.ActivityType)
                    .Include(ra => ra.User)
                    .Include(ra => ra.Employee)
                    .OrderByDescending(ra => ra.CreatedAt)
                    .Take(5)
                    .ToListAsync();

                var testResults = new
                {
                    success = true,
                    role = role,
                    databaseDriven = new
                    {
                        stats = new
                        {
                            count = stats.Count,
                            items = stats.Select(s => new { s.StatKey, s.StatName, s.StatValue, s.StatColor })
                        },
                        quickActions = new
                        {
                            count = actions.Count,
                            items = actions.Select(qa => new { qa.ActionKey, qa.Title, qa.Color })
                        },
                        menuItems = new
                        {
                            count = menuItems.Where(m => m.RolePermissions.Any()).Count(),
                            items = menuItems.Where(m => m.RolePermissions.Any())
                                            .Select(m => new { m.Name, m.Route, m.Icon })
                        },
                        recentActivities = new
                        {
                            count = recentActivities.Count,
                            items = recentActivities.Select(ra => new { ra.Title, ra.ActivityType.Name, ra.CreatedAt })
                        }
                    },
                    message = "All dashboard data retrieved from database tables",
                    timestamp = DateTime.UtcNow
                };

                return Ok(testResults);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to test dashboard data for role: {role}");
                return StatusCode(500, new { success = false, message = "Failed to test dashboard data", error = ex.Message });
            }
        }

        private async Task<User?> GetCurrentUserAsync()
        {
            var token = GetSessionToken();
            if (string.IsNullOrEmpty(token)) return null;
            return await _authService.ValidateSessionAsync(token);
        }

        /// <summary>
        /// Create sample dashboard data for testing (Development only)
        /// </summary>
        [HttpPost("create-sample-data")]
        public async Task<IActionResult> CreateSampleData()
        {
            try
            {
                // Only allow in development
                if (!Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")!.Equals("Development", StringComparison.OrdinalIgnoreCase))
                {
                    return Forbid("This endpoint is only available in development environment");
                }

                _logger.LogInformation("Creating sample dashboard data...");

                // Create Activity Types if they don't exist
                var activityTypes = new[]
                {
                    new ActivityType { Name = "Login", Description = "User login event", IconName = "Login", Color = "#4caf50", IsActive = true },
                    new ActivityType { Name = "Profile Update", Description = "Profile information updated", IconName = "Person", Color = "#2196f3", IsActive = true },
                    new ActivityType { Name = "Task Completed", Description = "Task completion", IconName = "CheckCircle", Color = "#ff9800", IsActive = true },
                    new ActivityType { Name = "Document Upload", Description = "Document uploaded", IconName = "Upload", Color = "#9c27b0", IsActive = true }
                };

                foreach (var activityType in activityTypes)
                {
                    var existing = await _context.ActivityTypes.FirstOrDefaultAsync(at => at.Name == activityType.Name);
                    if (existing == null)
                    {
                        _context.ActivityTypes.Add(activityType);
                    }
                }

                // Create Dashboard Stats if they don't exist
                var dashboardStats = new[]
                {
                    new DashboardStat
                    {
                        StatKey = "total_employees", StatName = "Total Employees", StatValue = "42",
                        StatColor = "#1976d2", IconName = "People", Subtitle = "Active employees",
                        ApplicableRoles = "Admin,SuperAdmin,HRAdmin", SortOrder = 1, IsActive = true
                    },
                    new DashboardStat
                    {
                        StatKey = "pending_tasks", StatName = "Pending Tasks", StatValue = "8",
                        StatColor = "#f57c00", IconName = "Assignment", Subtitle = "Requires attention",
                        ApplicableRoles = "Admin,SuperAdmin,HRAdmin", SortOrder = 2, IsActive = true
                    },
                    new DashboardStat
                    {
                        StatKey = "active_sessions", StatName = "Active Sessions", StatValue = "15",
                        StatColor = "#388e3c", IconName = "Security", Subtitle = "Logged in users",
                        ApplicableRoles = "SuperAdmin", SortOrder = 3, IsActive = true
                    },
                    new DashboardStat
                    {
                        StatKey = "system_health", StatName = "System Health", StatValue = "98%",
                        StatColor = "#4caf50", IconName = "Health", Subtitle = "All systems operational",
                        ApplicableRoles = "SuperAdmin", SortOrder = 4, IsActive = true
                    }
                };

                foreach (var stat in dashboardStats)
                {
                    var existing = await _context.DashboardStats.FirstOrDefaultAsync(ds => ds.StatKey == stat.StatKey);
                    if (existing == null)
                    {
                        _context.DashboardStats.Add(stat);
                    }
                }

                // Create Quick Actions if they don't exist
                var quickActions = new[]
                {
                    new QuickAction
                    {
                        ActionKey = "manage_employees", Title = "Manage Employees", Description = "View and manage employee records",
                        IconName = "People", Route = "/employees", Color = "#1976d2",
                        ApplicableRoles = "Admin,SuperAdmin,HRAdmin", SortOrder = 1, IsActive = true
                    },
                    new QuickAction
                    {
                        ActionKey = "employee_onboarding", Title = "Employee Onboarding", Description = "Onboard new employees",
                        IconName = "PersonAdd", Route = "/onboarding", Color = "#388e3c",
                        ApplicableRoles = "Admin,SuperAdmin,HRAdmin", SortOrder = 2, IsActive = true
                    },
                    new QuickAction
                    {
                        ActionKey = "system_reports", Title = "System Reports", Description = "Generate and view reports",
                        IconName = "Assessment", Route = "/reports", Color = "#f57c00",
                        ApplicableRoles = "Admin,SuperAdmin", SortOrder = 3, IsActive = true
                    },
                    new QuickAction
                    {
                        ActionKey = "system_settings", Title = "System Settings", Description = "Configure system settings",
                        IconName = "Settings", Route = "/settings", Color = "#7b1fa2",
                        ApplicableRoles = "SuperAdmin", SortOrder = 4, IsActive = true
                    }
                };

                foreach (var action in quickActions)
                {
                    var existing = await _context.QuickActions.FirstOrDefaultAsync(qa => qa.ActionKey == action.ActionKey);
                    if (existing == null)
                    {
                        _context.QuickActions.Add(action);
                    }
                }

                await _context.SaveChangesAsync();

                // Get counts to return
                var counts = new
                {
                    activityTypes = await _context.ActivityTypes.CountAsync(),
                    dashboardStats = await _context.DashboardStats.CountAsync(),
                    quickActions = await _context.QuickActions.CountAsync()
                };

                return Ok(new
                {
                    success = true,
                    message = "Sample dashboard data created successfully",
                    data = counts,
                    note = "Data has been added to support dashboard functionality"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create sample data");
                return StatusCode(500, new { success = false, message = "Failed to create sample data", error = ex.Message });
            }
        }
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