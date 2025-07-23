using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TPAHRSystemSimple.Data;
using TPAHRSystemSimple.Models;
using TPAHRSystemSimple.Services;

namespace TPAHRSystemSimple.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [EnableCors("AllowReactApp")]
    public class MenuController : ControllerBase
    {
        private readonly TPADbContext _context;
        private readonly AuthService _authService;
        private readonly ILogger<MenuController> _logger;

        public MenuController(TPADbContext context, AuthService authService, ILogger<MenuController> logger)
        {
            _context = context;
            _authService = authService;
            _logger = logger;
        }

        // =============================================================================
        // AUTHENTICATION HELPERS (MISSING METHODS)
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
        // MENU ENDPOINTS
        // =============================================================================

        /// <summary>
        /// Get menu items based on user role (DATABASE-DRIVEN)
        /// </summary>
        [HttpGet("items")]
        public async Task<IActionResult> GetMenuItems([FromQuery] string? role = null)
        {
            try
            {
                var user = await GetCurrentUserAsync();
                if (user == null)
                    return Unauthorized(ApiResponse<object>.ErrorResult("Authentication required"));

                var userRole = role ?? user.Role;

                // Get menu items from database with role-based permissions
                var menuItems = await _context.MenuItems
                    .Where(m => m.IsActive && m.ParentId == null) // Top-level items only
                    .Include(m => m.Children.Where(c => c.IsActive))
                    .Include(m => m.RolePermissions.Where(rp => rp.Role == userRole))
                    .OrderBy(m => m.SortOrder)
                    .ToListAsync();

                // Filter based on permissions and convert to DTOs
                var allowedMenuItems = new List<MenuItemDto>();

                foreach (var menuItem in menuItems)
                {
                    var permission = menuItem.RolePermissions.FirstOrDefault();
                    if (permission?.CanView == true)
                    {
                        var menuDto = new MenuItemDto
                        {
                            Id = menuItem.Id,
                            Name = menuItem.Name,
                            Route = menuItem.Route,
                            Icon = menuItem.Icon,
                            ParentId = menuItem.ParentId,
                            SortOrder = menuItem.SortOrder,
                            IsActive = menuItem.IsActive,
                            Permissions = new MenuPermissionDto
                            {
                                CanView = permission.CanView,
                                CanEdit = permission.CanEdit,
                                CanDelete = permission.CanDelete
                            },
                            Children = await GetChildMenuItems(menuItem.Children, userRole)
                        };
                        allowedMenuItems.Add(menuDto);
                    }
                }

                return Ok(ApiResponse<object>.SuccessResult(allowedMenuItems, "Menu items retrieved from database"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting menu items from database");
                return StatusCode(500, ApiResponse<object>.ErrorResult("Failed to retrieve menu items"));
            }
        }

        /// <summary>
        /// Check if user can access a specific menu (DATABASE-DRIVEN)
        /// </summary>
        [HttpGet("access/{menuName}")]
        public async Task<IActionResult> CheckMenuAccess(string menuName)
        {
            try
            {
                var user = await GetCurrentUserAsync();
                if (user == null)
                    return Unauthorized(ApiResponse<object>.ErrorResult("Authentication required"));

                // Use the database function to check permission
                var hasAccess = await CheckUserMenuPermissionAsync(user.Role, menuName, "VIEW");

                return Ok(ApiResponse<object>.SuccessResult(new { hasAccess, menuName, userRole = user.Role }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking menu access for: {menuName}");
                return StatusCode(500, ApiResponse<object>.ErrorResult("Failed to check menu access"));
            }
        }

        // =============================================================================
        // DATABASE HELPER METHODS
        // =============================================================================

        private async Task<List<MenuItemDto>> GetChildMenuItems(IEnumerable<MenuItem> children, string userRole)
        {
            var childDtos = new List<MenuItemDto>();

            foreach (var child in children.OrderBy(c => c.SortOrder))
            {
                var permission = child.RolePermissions.FirstOrDefault(rp => rp.Role == userRole);
                if (permission?.CanView == true)
                {
                    var childDto = new MenuItemDto
                    {
                        Id = child.Id,
                        Name = child.Name,
                        Route = child.Route,
                        Icon = child.Icon,
                        ParentId = child.ParentId,
                        SortOrder = child.SortOrder,
                        IsActive = child.IsActive,
                        Permissions = new MenuPermissionDto
                        {
                            CanView = permission.CanView,
                            CanEdit = permission.CanEdit,
                            CanDelete = permission.CanDelete
                        }
                    };
                    childDtos.Add(childDto);
                }
            }

            return childDtos;
        }

        private async Task<bool> CheckUserMenuPermissionAsync(string userRole, string menuName, string permissionType)
        {
            try
            {
                // Query the database directly using raw SQL to use the existing function
                var sql = "SELECT dbo.fn_UserHasMenuPermission(@p0, @p1, @p2)";
                var result = await _context.Database.SqlQueryRaw<bool>(sql, userRole, menuName, permissionType).FirstOrDefaultAsync();
                return result;
            }
            catch
            {
                // Fallback to EF query if the function doesn't exist
                var menuItem = await _context.MenuItems
                    .Include(m => m.RolePermissions)
                    .FirstOrDefaultAsync(m => m.Name == menuName && m.IsActive);

                if (menuItem == null) return false;

                var permission = menuItem.RolePermissions.FirstOrDefault(rp => rp.Role == userRole);
                return permissionType.ToUpper() switch
                {
                    "VIEW" => permission?.CanView ?? false,
                    "EDIT" => permission?.CanEdit ?? false,
                    "DELETE" => permission?.CanDelete ?? false,
                    _ => false
                };
            }
        }

        /// <summary>
        /// Get dashboard configuration
        /// </summary>
        [HttpGet("dashboard-config")]
        public async Task<IActionResult> GetDashboardConfig()
        {
            try
            {
                var user = await GetCurrentUserAsync();
                if (user == null)
                    return Unauthorized(ApiResponse<object>.ErrorResult("Authentication required"));

                var config = new
                {
                    showSidebar = true,
                    defaultRoute = "/dashboard",
                    allowedRoutes = GetAllowedRoutesForRole(user.Role),
                    navigationStyle = "tabs", // or "sidebar"
                    theme = "default"
                };

                return Ok(ApiResponse<object>.SuccessResult(config, "Dashboard configuration retrieved"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting dashboard config");
                return StatusCode(500, ApiResponse<object>.ErrorResult("Failed to get dashboard configuration"));
            }
        }

        /// <summary>
        /// Get breadcrumbs for current route (FRONTEND COMPATIBILITY)
        /// </summary>
        [HttpGet("breadcrumbs")]
        public async Task<IActionResult> GetBreadcrumbs([FromQuery] string currentRoute)
        {
            try
            {
                var user = await GetCurrentUserAsync();
                if (user == null)
                    return Unauthorized(new { success = false, message = "Authentication required" });

                // Simple breadcrumb generation based on route
                var breadcrumbs = GenerateBreadcrumbs(currentRoute);

                return Ok(new
                {
                    success = true,
                    data = breadcrumbs,
                    message = "Breadcrumbs generated"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating breadcrumbs for route: {currentRoute}");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Failed to generate breadcrumbs"
                });
            }
        }

        // =============================================================================
        // BREADCRUMB GENERATION HELPER
        // =============================================================================

        private object[] GenerateBreadcrumbs(string currentRoute)
        {
            var breadcrumbs = new List<object>
            {
                new { name = "Dashboard", route = "/dashboard", icon = "Dashboard" }
            };

            if (string.IsNullOrEmpty(currentRoute) || currentRoute == "/dashboard")
            {
                return breadcrumbs.ToArray();
            }

            // Add breadcrumbs based on route
            switch (currentRoute.ToLower())
            {
                case "/employees":
                    breadcrumbs.Add(new { name = "Employees", route = "/employees", icon = "People" });
                    break;
                case "/time-attendance":
                    breadcrumbs.Add(new { name = "Time & Attendance", route = "/time-attendance", icon = "Schedule" });
                    break;
                case "/leave":
                    breadcrumbs.Add(new { name = "Leave Management", route = "/leave", icon = "EventAvailable" });
                    break;
                case "/onboarding":
                    breadcrumbs.Add(new { name = "Onboarding", route = "/onboarding", icon = "PersonAdd" });
                    break;
                case "/reports":
                    breadcrumbs.Add(new { name = "Reports", route = "/reports", icon = "Assessment" });
                    break;
                case "/settings":
                    breadcrumbs.Add(new { name = "Settings", route = "/settings", icon = "Settings" });
                    break;
                case "/menu-management":
                    breadcrumbs.Add(new { name = "Menu Management", route = "/menu-management", icon = "Settings" });
                    break;
                default:
                    breadcrumbs.Add(new { name = "Page", route = currentRoute, icon = "Page" });
                    break;
            }

            return breadcrumbs.ToArray();
        }

        private string[] GetAllowedRoutesForRole(string role)
        {
            return role.ToLower() switch
            {
                "admin" or "superadmin" => new[]
                {
                    "/dashboard", "/employees", "/time-attendance", "/leave", "/onboarding", "/reports", "/settings"
                },
                "hradmin" or "hr_manager" => new[]
                {
                    "/dashboard", "/employees", "/leave", "/onboarding", "/reports"
                },
                "employee" => new[]
                {
                    "/dashboard", "/time-attendance", "/leave", "/profile"
                },
                _ => new[]
                {
                    "/dashboard", "/profile"
                }
            };
        }

        /// <summary>
        /// Test menu functionality
        /// </summary>
        [HttpGet("test")]
        public IActionResult TestMenu()
        {
            return Ok(new
            {
                success = true,
                message = "Menu controller is working",
                timestamp = DateTime.UtcNow,
                endpoints = new[]
                {
                    "/api/menu/items",
                    "/api/menu/access/{menuName}",
                    "/api/menu/dashboard-config"
                }
            });
        }
    }
}