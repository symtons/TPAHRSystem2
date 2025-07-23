// =============================================================================
// TPAHRSystem2/Controllers/DashboardController.cs - DATABASE-DRIVEN VERSION
// File: TPAHRSystem2/Controllers/DashboardController.cs (Replace existing)
// This replicates the exact functionality from TPAHRSystem
// =============================================================================

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Cors;
using TPAHRSystemSimple.Data;
using TPAHRSystemSimple.Models;
using TPAHRSystemSimple.Services;
using Swashbuckle.AspNetCore.Annotations;

namespace TPAHRSystemSimple.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [EnableCors("AllowReactApp")]
    [SwaggerTag("Database-driven dashboard endpoints")]
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
        // AUTHENTICATION HELPERS
        // =============================================================================

        private string? GetSessionToken()
        {
            var authHeader = Request.Headers.Authorization.FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
            {
                return authHeader["Bearer ".Length..].Trim();
            }
            return Request.Headers["X-Session-Token"].FirstOrDefault();
        }

        private async Task<User?> GetCurrentUserAsync()
        {
            var token = GetSessionToken();
            if (string.IsNullOrEmpty(token)) return null;
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
        // DATABASE-DRIVEN DASHBOARD ENDPOINTS (EXACT REPLICA OF TPAHRSystem)
        // =============================================================================

        /// <summary>
        /// Get dashboard statistics from database based on role
        /// </summary>
        [HttpGet("stats/{role}")]
        [SwaggerOperation(Summary = "Get dashboard statistics", Description = "Retrieve role-based dashboard statistics from database")]
        [SwaggerResponse(200, "Statistics retrieved successfully")]
        public async Task<IActionResult> GetDashboardStats(string role)
        {
            try
            {
                _logger.LogInformation($"📊 Getting dashboard stats for role: {role}");

                var stats = await _context.DashboardStats
                    .Where(s => s.IsActive &&
                               (s.ApplicableRoles == null ||
                                s.ApplicableRoles.Contains(role)))
                    .OrderBy(s => s.SortOrder)
                    .ToListAsync();

                var result = stats.Select(s => new
                {
                    title = s.StatName,
                    value = s.StatValue,
                    subtitle = s.Subtitle,
                    icon = s.IconName,
                    color = s.StatColor
                });

                _logger.LogInformation($"✅ Found {result.Count()} stats for role: {role}");
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error getting dashboard stats for role: {Role}", role);
                return StatusCode(500, new { success = false, message = $"Failed to get dashboard stats: {ex.Message}" });
            }
        }

        /// <summary>
        /// Get quick actions from database based on role
        /// </summary>
        [HttpGet("quick-actions/{role}")]
        [SwaggerOperation(Summary = "Get quick actions", Description = "Retrieve role-based quick actions from database")]
        [SwaggerResponse(200, "Quick actions retrieved successfully")]
        public async Task<IActionResult> GetQuickActions(string role)
        {
            try
            {
                _logger.LogInformation($"⚡ Getting quick actions for role: {role}");

                var actions = await _context.QuickActions
                    .Where(qa => qa.IsActive &&
                                (qa.ApplicableRoles == null ||
                                 qa.ApplicableRoles.Contains(role)))
                    .OrderBy(qa => qa.SortOrder)
                    .ToListAsync();

                var result = actions.Select(qa => new
                {
                    key = qa.ActionKey,
                    label = qa.Title,
                    icon = qa.IconName,
                    color = qa.Color,
                    route = qa.Route,
                    description = qa.Description
                });

                _logger.LogInformation($"✅ Found {result.Count()} actions for role: {role}");
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error getting quick actions for role: {Role}", role);
                return StatusCode(500, new { success = false, message = $"Failed to get quick actions: {ex.Message}" });
            }
        }

        /// <summary>
        /// Get recent activities from database
        /// </summary>
        [HttpGet("recent-activities/{userId}")]
        [SwaggerOperation(Summary = "Get recent activities", Description = "Retrieve recent activities from database")]
        [SwaggerResponse(200, "Recent activities retrieved successfully")]
        public async Task<IActionResult> GetRecentActivities(int userId, [FromQuery] string role)
        {
            try
            {
                _logger.LogInformation($"🔄 Getting recent activities for user: {userId}, role: {role}");

                // Get recent activities with related data (avoiding Employee for now)
                var activities = await _context.RecentActivities
                    .Include(ra => ra.User)
                    .Include(ra => ra.ActivityType)
                    .OrderByDescending(ra => ra.CreatedAt)
                    .Take(10)
                    .Select(ra => new
                    {
                        id = ra.Id,
                        description = ra.Description,
                        timestamp = ra.CreatedAt,
                        activityType = ra.ActivityType != null ? ra.ActivityType.Name : "General",
                        icon = ra.ActivityType != null ? ra.ActivityType.IconName : "Info",
                        color = ra.ActivityType != null ? ra.ActivityType.Color : "#2196f3",
                        userName = ra.User != null ? ra.User.Email : "System"
                    })
                    .ToListAsync();

                _logger.LogInformation($"✅ Found {activities.Count} activities for user: {userId}");
                return Ok(new { success = true, data = activities });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error getting recent activities for user: {UserId}", userId);
                return StatusCode(500, new { success = false, message = $"Failed to get recent activities: {ex.Message}" });
            }
        }

        /// <summary>
        /// Get complete dashboard summary from database
        /// </summary>
        [HttpGet("summary/{userId}")]
        [SwaggerOperation(Summary = "Get dashboard summary", Description = "Retrieve complete dashboard summary from database")]
        [SwaggerResponse(200, "Dashboard summary retrieved successfully")]
        public async Task<IActionResult> GetDashboardSummary(int userId, [FromQuery] string role)
        {
            try
            {
                _logger.LogInformation($"📋 Getting dashboard summary for user: {userId}, role: {role}");

                // Get stats from database
                var stats = await _context.DashboardStats
                    .Where(s => s.IsActive &&
                               (s.ApplicableRoles == null ||
                                s.ApplicableRoles.Contains(role)))
                    .OrderBy(s => s.SortOrder)
                    .ToListAsync();

                // Get quick actions from database
                var actions = await _context.QuickActions
                    .Where(qa => qa.IsActive &&
                                (qa.ApplicableRoles == null ||
                                 qa.ApplicableRoles.Contains(role)))
                    .OrderBy(qa => qa.SortOrder)
                    .ToListAsync();

                // Get recent activities from database
                var recentActivities = await _context.RecentActivities
                    .Include(ra => ra.User)
                    .Include(ra => ra.ActivityType)
                    .OrderByDescending(ra => ra.CreatedAt)
                    .Take(5)
                    .ToListAsync();

                var summary = new
                {
                    success = true,
                    data = new
                    {
                        stats = stats.Select(s => new
                        {
                            title = s.StatName,
                            value = s.StatValue,
                            subtitle = s.Subtitle,
                            icon = s.IconName,
                            color = s.StatColor
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
                            description = ra.Description,
                            timestamp = ra.CreatedAt,
                            activityType = ra.ActivityType?.Name ?? "General",
                            userName = ra.User?.Email.Split('@')[0] ?? "System"
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
        [SwaggerOperation(Summary = "Test dashboard controller", Description = "Test dashboard functionality")]
        public async Task<IActionResult> Test()
        {
            try
            {
                // Test database connectivity
                var statsCount = await _context.DashboardStats.CountAsync();
                var actionsCount = await _context.QuickActions.CountAsync();
                var activitiesCount = await _context.RecentActivities.CountAsync();

                return Ok(new
                {
                    success = true,
                    message = "Dashboard controller is working with database",
                    timestamp = DateTime.UtcNow,
                    database = new
                    {
                        connected = true,
                        dashboardStats = statsCount,
                        quickActions = actionsCount,
                        recentActivities = activitiesCount
                    },
                    endpoints = new[]
                    {
                        "GET /api/dashboard/stats/{role}",
                        "GET /api/dashboard/quick-actions/{role}",
                        "GET /api/dashboard/recent-activities/{userId}",
                        "GET /api/dashboard/summary/{userId}"
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error in dashboard test");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Dashboard test failed",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Get dashboard configuration
        /// </summary>
        [HttpGet("config")]
        [SwaggerOperation(Summary = "Get dashboard config", Description = "Get dashboard configuration")]
        public IActionResult GetDashboardConfig()
        {
            return Ok(new
            {
                success = true,
                data = new
                {
                    refreshInterval = 30000, // 30 seconds
                    showNotifications = true,
                    autoRefresh = true,
                    theme = "default",
                    dateFormat = "MM/dd/yyyy",
                    timeFormat = "12-hour",
                    isDatabaseDriven = true
                },
                message = "Dashboard configuration retrieved"
            });
        }
    }
}