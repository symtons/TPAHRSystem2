using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Cors;
using TPAHRSystemSimple.Data;
using TPAHRSystemSimple.Models;
using TPAHRSystemSimple.Services;

namespace TPAHRSystemSimple.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [EnableCors("AllowReactApp")]
    public class DashboardController : ControllerBase
    {
        private readonly TPADbContext _context;
        private readonly AuthService _authService;
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(TPADbContext context, AuthService authService, ILogger<DashboardController> logger)
        {
            _context = context;
            _authService = authService;
            _logger = logger;
        }

        // =============================================================================
        // AUTHENTICATION HELPER METHODS
        // =============================================================================

        private string? GetSessionToken()
        {
            // Try Bearer token from Authorization header
            var authHeader = Request.Headers.Authorization.FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
            {
                return authHeader["Bearer ".Length..].Trim();
            }

            // Try custom X-Session-Token header
            var sessionHeader = Request.Headers["X-Session-Token"].FirstOrDefault();
            if (!string.IsNullOrEmpty(sessionHeader))
            {
                return sessionHeader;
            }

            return null;
        }

        private async Task<User?> GetCurrentUserAsync()
        {
            var token = GetSessionToken();
            if (string.IsNullOrEmpty(token))
                return null;

            return await _authService.ValidateSessionAsync(token);
        }

        private async Task<Employee?> GetCurrentEmployeeAsync()
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return null;

            return await _context.Employees
                .Include(e => e.Department)
                .FirstOrDefaultAsync(e => e.UserId == user.Id);
        }

        // =============================================================================
        // DASHBOARD ENDPOINTS (100% DATABASE-DRIVEN)
        // =============================================================================

        /// <summary>
        /// Get dashboard statistics for specific role from database
        /// </summary>
        [HttpGet("stats/{role}")]
        public async Task<IActionResult> GetDashboardStats(string role)
        {
            try
            {
                var user = await GetCurrentUserAsync();
                if (user == null)
                    return Unauthorized(new { success = false, message = "Authentication required" });

                _logger.LogInformation($"🔍 Getting dashboard stats for role: {role}");

                // Query DashboardStats table with role filtering
                var statsQuery = _context.DashboardStats
                    .Where(s => s.IsActive &&
                               (string.IsNullOrEmpty(s.ApplicableRoles) ||
                                s.ApplicableRoles.Contains(role)))
                    .OrderBy(s => s.SortOrder);

                var stats = await statsQuery.ToListAsync();

                if (!stats.Any())
                {
                    _logger.LogWarning($"⚠️ No dashboard stats found for role: {role}");
                    return Ok(new
                    {
                        success = true,
                        data = new object[0],
                        message = $"No dashboard statistics configured for role: {role}"
                    });
                }

                // Transform to frontend format
                var result = stats.Select(s => new
                {
                    key = s.StatKey,
                    name = s.StatName,
                    value = s.StatValue,
                    color = s.StatColor,
                    icon = s.IconName,
                    subtitle = s.Subtitle
                }).ToArray();

                _logger.LogInformation($"✅ Found {result.Length} dashboard stats for role: {role}");

                return Ok(new
                {
                    success = true,
                    data = result,
                    message = "Dashboard statistics retrieved from database"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"💥 Error getting dashboard stats for role: {role}");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Failed to retrieve dashboard statistics",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Get quick actions for specific role from database
        /// </summary>
        [HttpGet("quick-actions/{role}")]
        public async Task<IActionResult> GetQuickActions(string role)
        {
            try
            {
                var user = await GetCurrentUserAsync();
                if (user == null)
                    return Unauthorized(new { success = false, message = "Authentication required" });

                _logger.LogInformation($"⚡ Getting quick actions for role: {role}");

                // Query QuickActions table with role filtering
                var actionsQuery = _context.QuickActions
                    .Where(qa => qa.IsActive &&
                                (string.IsNullOrEmpty(qa.ApplicableRoles) ||
                                 qa.ApplicableRoles.Contains(role)))
                    .OrderBy(qa => qa.SortOrder);

                var actions = await actionsQuery.ToListAsync();

                if (!actions.Any())
                {
                    _logger.LogWarning($"⚠️ No quick actions found for role: {role}");
                    return Ok(new
                    {
                        success = true,
                        data = new object[0],
                        message = $"No quick actions configured for role: {role}"
                    });
                }

                // Transform to frontend format
                var result = actions.Select(qa => new
                {
                    key = qa.ActionKey,
                    label = qa.Title,
                    icon = qa.IconName,
                    color = qa.Color,
                    route = qa.Route,
                    description = qa.Description
                }).ToArray();

                _logger.LogInformation($"✅ Found {result.Length} quick actions for role: {role}");

                return Ok(new
                {
                    success = true,
                    data = result,
                    message = "Quick actions retrieved from database"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"💥 Error getting quick actions for role: {role}");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Failed to retrieve quick actions",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Get recent activities for specific user from database
        /// </summary>
        [HttpGet("recent-activities/{userId}")]
        public async Task<IActionResult> GetRecentActivities(int userId, [FromQuery] string? role = null)
        {
            try
            {
                var user = await GetCurrentUserAsync();
                if (user == null)
                    return Unauthorized(new { success = false, message = "Authentication required" });

                _logger.LogInformation($"📋 Getting recent activities for user: {userId}, role: {role}");

                // Query RecentActivities with related data
                var activitiesQuery = _context.RecentActivities
                    .Include(ra => ra.User)
                    .Include(ra => ra.Employee)
                    .Include(ra => ra.ActivityType)
                    .Where(ra => ra.UserId == userId)
                    .OrderByDescending(ra => ra.CreatedAt)
                    .Take(10);

                var activities = await activitiesQuery.ToListAsync();

                if (!activities.Any())
                {
                    _logger.LogInformation($"ℹ️ No recent activities found for user: {userId}");
                    return Ok(new
                    {
                        success = true,
                        data = new object[0],
                        message = "No recent activities found"
                    });
                }

                // Transform to frontend format
                var result = activities.Select(ra => new
                {
                    id = ra.Id,
                    title = ra.Title,
                    description = ra.Description,
                    timestamp = ra.CreatedAt,
                    type = ra.ActivityType.Name,
                    icon = ra.ActivityType.IconName ?? "Event",
                    color = ra.ActivityType.Color,
                    userName = ra.Employee?.FullName ?? ra.User.Email.Split('@')[0],
                    metadata = ra.Metadata
                }).ToArray();

                _logger.LogInformation($"✅ Found {result.Length} recent activities for user: {userId}");

                return Ok(new
                {
                    success = true,
                    data = result,
                    message = "Recent activities retrieved from database"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"💥 Error getting recent activities for user: {userId}");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Failed to retrieve recent activities",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Get complete dashboard summary from database
        /// </summary>
        [HttpGet("summary/{userId}")]
        public async Task<IActionResult> GetDashboardSummary(int userId, [FromQuery] string? role = null)
        {
            try
            {
                var user = await GetCurrentUserAsync();
                if (user == null)
                    return Unauthorized(new { success = false, message = "Authentication required" });

                var userRole = role ?? user.Role;
                _logger.LogInformation($"📊 Getting complete dashboard summary for user: {userId}, role: {userRole}");

                // Get employee information
                var employee = await _context.Employees
                    .Include(e => e.Department)
                    .FirstOrDefaultAsync(e => e.UserId == userId);

                // Get dashboard stats
                var stats = await _context.DashboardStats
                    .Where(s => s.IsActive &&
                               (string.IsNullOrEmpty(s.ApplicableRoles) ||
                                s.ApplicableRoles.Contains(userRole)))
                    .OrderBy(s => s.SortOrder)
                    .ToListAsync();

                // Get quick actions
                var actions = await _context.QuickActions
                    .Where(qa => qa.IsActive &&
                                (string.IsNullOrEmpty(qa.ApplicableRoles) ||
                                 qa.ApplicableRoles.Contains(userRole)))
                    .OrderBy(qa => qa.SortOrder)
                    .ToListAsync();

                // Get recent activities (limited to 5 for summary)
                var recentActivities = await _context.RecentActivities
                    .Include(ra => ra.User)
                    .Include(ra => ra.Employee)
                    .Include(ra => ra.ActivityType)
                    .Where(ra => ra.UserId == userId)
                    .OrderByDescending(ra => ra.CreatedAt)
                    .Take(5)
                    .ToListAsync();

                // Build comprehensive summary
                var summary = new
                {
                    success = true,
                    user = new
                    {
                        id = user.Id,
                        name = employee?.FullName ?? user.Email.Split('@')[0],
                        email = user.Email,
                        role = user.Role,
                        lastLogin = user.LastLogin
                    },
                    employee = employee != null ? new
                    {
                        id = employee.Id,
                        employeeNumber = employee.EmployeeNumber,
                        department = employee.Department?.Name,
                        position = employee.Position,
                        hireDate = employee.HireDate,
                        status = employee.Status
                    } : null,
                    dashboardData = new
                    {
                        stats = stats.Select(s => new
                        {
                            key = s.StatKey,
                            name = s.StatName,
                            value = s.StatValue,
                            color = s.StatColor,
                            icon = s.IconName,
                            subtitle = s.Subtitle
                        }),
                        quickActions = actions.Select(qa => new
                        {
                            key = qa.ActionKey,
                            label = qa.Title,
                            icon = qa.IconName,
                            color = qa.Color,
                            route = qa.Route
                        }),
                        recentActivities = recentActivities.Select(ra => new
                        {
                            id = ra.Id,
                            title = ra.Title,
                            description = ra.Description,
                            timestamp = ra.CreatedAt,
                            type = ra.ActivityType.Name,
                            icon = ra.ActivityType.IconName,
                            color = ra.ActivityType.Color,
                            userName = ra.Employee?.FullName ?? ra.User.Email.Split('@')[0]
                        })
                    },
                    systemInfo = new
                    {
                        currentTime = DateTime.UtcNow,
                        timeZone = TimeZoneInfo.Local.DisplayName,
                        version = "1.0.0",
                        dataSource = "Database-Driven",
                        recordCounts = new
                        {
                            stats = stats.Count,
                            actions = actions.Count,
                            activities = recentActivities.Count
                        }
                    },
                    message = "Complete dashboard summary retrieved from database"
                };

                _logger.LogInformation($"✅ Dashboard summary compiled for user: {userId} - Stats: {stats.Count}, Actions: {actions.Count}, Activities: {recentActivities.Count}");

                return Ok(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"💥 Error getting dashboard summary for user: {userId}");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Failed to retrieve dashboard summary",
                    error = ex.Message
                });
            }
        }

        // =============================================================================
        // TEST AND DEBUG ENDPOINTS
        // =============================================================================

        /// <summary>
        /// Test dashboard controller functionality
        /// </summary>
        [HttpGet("test")]
        public async Task<IActionResult> TestDashboard()
        {
            try
            {
                var dbStats = new
                {
                    dashboardStats = await _context.DashboardStats.CountAsync(ds => ds.IsActive),
                    quickActions = await _context.QuickActions.CountAsync(qa => qa.IsActive),
                    recentActivities = await _context.RecentActivities.CountAsync(),
                    activityTypes = await _context.ActivityTypes.CountAsync(at => at.IsActive)
                };

                return Ok(new
                {
                    success = true,
                    message = "Dashboard controller is working correctly",
                    timestamp = DateTime.UtcNow,
                    database = new
                    {
                        connected = true,
                        tables = dbStats
                    },
                    availableEndpoints = new[]
                    {
                        "GET /api/dashboard/stats/{role}",
                        "GET /api/dashboard/quick-actions/{role}",
                        "GET /api/dashboard/recent-activities/{userId}",
                        "GET /api/dashboard/summary/{userId}",
                        "GET /api/dashboard/test"
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dashboard test failed");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Dashboard test failed",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Get dashboard configuration info
        /// </summary>
        [HttpGet("config")]
        public async Task<IActionResult> GetDashboardConfig()
        {
            try
            {
                var user = await GetCurrentUserAsync();
                if (user == null)
                    return Unauthorized(new { success = false, message = "Authentication required" });

                var config = new
                {
                    success = true,
                    userRole = user.Role,
                    dashboardFeatures = new
                    {
                        databaseDriven = true,
                        roleBasedContent = true,
                        realTimeActivities = true,
                        dynamicStats = true
                    },
                    dataSource = "SQL Server Database",
                    refreshInterval = new
                    {
                        stats = "5 minutes",
                        activities = "30 seconds",
                        actions = "On role change"
                    },
                    message = "Dashboard is fully database-driven"
                };

                return Ok(config);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting dashboard config");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Failed to get dashboard config",
                    error = ex.Message
                });
            }
        }
    }
}
