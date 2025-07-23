// =============================================================================
// TPAHRSystem2/Controllers/MenuController.cs - CLEAN VERSION
// File: TPAHRSystem2/Controllers/MenuController.cs (Replace existing)
// =============================================================================

using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TPAHRSystemSimple.Data;
using TPAHRSystemSimple.Models;
using TPAHRSystemSimple.Services;
using Swashbuckle.AspNetCore.Annotations;

namespace TPAHRSystemSimple.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [EnableCors("AllowReactApp")]
    [SwaggerTag("Menu endpoints for navigation and access control")]
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

        /// <summary>
        /// Test menu controller functionality
        /// </summary>
        [HttpGet("test")]
        [SwaggerOperation(Summary = "Test menu controller", Description = "Simple health check for menu controller")]
        [SwaggerResponse(200, "Controller is working", typeof(MenuTestResponse))]
        public IActionResult Test()
        {
            return Ok(new MenuTestResponse
            {
                Success = true,
                Message = "Menu controller is working",
                Timestamp = DateTime.UtcNow,
                Endpoints = new[]
                {
                    "/api/menu/items",
                    "/api/menu/access/{menuName}",
                    "/api/menu/dashboard-config",
                    "/api/menu/breadcrumbs"
                }
            });
        }

        /// <summary>
        /// Get menu items based on user role
        /// </summary>
        [HttpGet("items")]
        [SwaggerOperation(Summary = "Get menu items", Description = "Retrieve menu items for current user role")]
        [SwaggerResponse(200, "Menu items retrieved successfully", typeof(MenuItemsResponse))]
        public async Task<IActionResult> GetMenuItems([FromQuery] string? role = null)
        {
            try
            {
                _logger.LogInformation($"Getting menu items for role: {role ?? "default"}");

                // Simple hardcoded menu structure based on role
                var menuItems = new List<SimpleMenuDto>();

                // Common menu items for all roles
                menuItems.Add(new SimpleMenuDto
                {
                    Id = 1,
                    Name = "Dashboard",
                    Route = "/dashboard",
                    Icon = "Dashboard",
                    SortOrder = 1,
                    IsActive = true
                });

                // Role-specific menu items
                if (role?.ToLower() == "admin" || role?.ToLower() == "superadmin")
                {
                    menuItems.AddRange(new[]
                    {
                        new SimpleMenuDto { Id = 2, Name = "Employees", Route = "/employees", Icon = "People", SortOrder = 2, IsActive = true },
                        new SimpleMenuDto { Id = 3, Name = "Time & Attendance", Route = "/time", Icon = "Schedule", SortOrder = 3, IsActive = true },
                        new SimpleMenuDto { Id = 4, Name = "Reports", Route = "/reports", Icon = "Assessment", SortOrder = 4, IsActive = true },
                        new SimpleMenuDto { Id = 5, Name = "Administration", Route = "/admin", Icon = "Settings", SortOrder = 5, IsActive = true }
                    });
                }
                else if (role?.ToLower() == "hr" || role?.ToLower() == "hradmin")
                {
                    menuItems.AddRange(new[]
                    {
                        new SimpleMenuDto { Id = 2, Name = "Employees", Route = "/employees", Icon = "People", SortOrder = 2, IsActive = true },
                        new SimpleMenuDto { Id = 3, Name = "Onboarding", Route = "/onboarding", Icon = "PersonAdd", SortOrder = 3, IsActive = true },
                        new SimpleMenuDto { Id = 4, Name = "Reports", Route = "/reports", Icon = "Assessment", SortOrder = 4, IsActive = true }
                    });
                }
                else
                {
                    // Employee role
                    menuItems.AddRange(new[]
                    {
                        new SimpleMenuDto { Id = 2, Name = "My Profile", Route = "/profile", Icon = "Person", SortOrder = 2, IsActive = true },
                        new SimpleMenuDto { Id = 3, Name = "Time Entry", Route = "/time/entry", Icon = "Schedule", SortOrder = 3, IsActive = true },
                        new SimpleMenuDto { Id = 4, Name = "My Tasks", Route = "/tasks", Icon = "Assignment", SortOrder = 4, IsActive = true }
                    });
                }

                return Ok(new MenuItemsResponse
                {
                    Success = true,
                    Data = menuItems,
                    Message = "Menu items retrieved successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting menu items");
                return StatusCode(500, new MenuItemsResponse
                {
                    Success = false,
                    Message = $"Failed to get menu items: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Check menu access for specific menu
        /// </summary>
        [HttpGet("access/{menuName}")]
        [SwaggerOperation(Summary = "Check menu access", Description = "Check if user has access to specific menu")]
        [SwaggerResponse(200, "Access check completed", typeof(MenuAccessResponse))]
        public async Task<IActionResult> CheckMenuAccess(string menuName)
        {
            try
            {
                _logger.LogInformation($"Checking menu access for: {menuName}");

                // Simple access check based on menu name
                var access = new MenuAccessDto();

                switch (menuName.ToLower())
                {
                    case "dashboard":
                        access = new MenuAccessDto { CanView = true, CanEdit = false, CanDelete = false };
                        break;
                    case "employees":
                        access = new MenuAccessDto { CanView = true, CanEdit = true, CanDelete = false };
                        break;
                    case "administration":
                        access = new MenuAccessDto { CanView = true, CanEdit = true, CanDelete = true };
                        break;
                    default:
                        access = new MenuAccessDto { CanView = true, CanEdit = false, CanDelete = false };
                        break;
                }

                return Ok(new MenuAccessResponse
                {
                    Success = true,
                    Data = access,
                    Message = "Access check completed"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking menu access for: {menuName}");
                return StatusCode(500, new MenuAccessResponse
                {
                    Success = false,
                    Message = $"Failed to check menu access: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Get dashboard configuration
        /// </summary>
        [HttpGet("dashboard-config")]
        [SwaggerOperation(Summary = "Get dashboard config", Description = "Retrieve dashboard configuration")]
        [SwaggerResponse(200, "Dashboard config retrieved", typeof(MenuDashboardConfigResponse))]
        public async Task<IActionResult> GetDashboardConfig()
        {
            try
            {
                _logger.LogInformation("Getting dashboard configuration");

                var config = new MenuDashboardConfigDto
                {
                    ShowSidebar = true,
                    DefaultRoute = "/dashboard",
                    AllowedRoutes = new[] { "/dashboard", "/employees", "/time", "/profile", "/reports" },
                    NavigationStyle = "sidebar",
                    Theme = "default",
                    CollapsibleSidebar = true,
                    ShowUserMenu = true
                };

                return Ok(new MenuDashboardConfigResponse
                {
                    Success = true,
                    Data = config,
                    Message = "Dashboard configuration retrieved"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting dashboard config");
                return StatusCode(500, new MenuDashboardConfigResponse
                {
                    Success = false,
                    Message = $"Failed to get dashboard config: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Get breadcrumbs for current route
        /// </summary>
        [HttpGet("breadcrumbs")]
        [SwaggerOperation(Summary = "Get breadcrumbs", Description = "Generate breadcrumbs for current route")]
        [SwaggerResponse(200, "Breadcrumbs generated", typeof(BreadcrumbsResponse))]
        public async Task<IActionResult> GetBreadcrumbs([FromQuery] string currentRoute)
        {
            try
            {
                _logger.LogInformation($"Getting breadcrumbs for route: {currentRoute}");

                var breadcrumbs = new List<BreadcrumbDto>
                {
                    new BreadcrumbDto { Name = "Home", Route = "/", Icon = "Home" }
                };

                // Simple breadcrumb generation based on route
                if (!string.IsNullOrEmpty(currentRoute) && currentRoute != "/")
                {
                    var routeParts = currentRoute.Split('/', StringSplitOptions.RemoveEmptyEntries);
                    var currentPath = "";

                    foreach (var part in routeParts)
                    {
                        currentPath += "/" + part;
                        var displayName = part switch
                        {
                            "dashboard" => "Dashboard",
                            "employees" => "Employees",
                            "time" => "Time & Attendance",
                            "reports" => "Reports",
                            "admin" => "Administration",
                            "profile" => "Profile",
                            "onboarding" => "Onboarding",
                            _ => char.ToUpper(part[0]) + part[1..]
                        };

                        breadcrumbs.Add(new BreadcrumbDto
                        {
                            Name = displayName,
                            Route = currentPath,
                            Icon = GetIconForRoute(part)
                        });
                    }
                }

                return Ok(new BreadcrumbsResponse
                {
                    Success = true,
                    Data = breadcrumbs,
                    Message = "Breadcrumbs generated successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting breadcrumbs for route: {currentRoute}");
                return StatusCode(500, new BreadcrumbsResponse
                {
                    Success = false,
                    Message = $"Failed to get breadcrumbs: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Get user permissions for menu items
        /// </summary>
        [HttpGet("permissions")]
        [SwaggerOperation(Summary = "Get user permissions", Description = "Retrieve user permissions for menu items")]
        [SwaggerResponse(200, "Permissions retrieved", typeof(MenuPermissionsResponse))]
        public async Task<IActionResult> GetUserPermissions([FromQuery] string? role = null)
        {
            try
            {
                _logger.LogInformation($"Getting user permissions for role: {role}");

                var permissions = new List<MenuPermissionDto>();

                // Simple permission mapping based on role
                if (role?.ToLower() == "admin" || role?.ToLower() == "superadmin")
                {
                    permissions.AddRange(new[]
                    {
                        new MenuPermissionDto { MenuName = "Dashboard", CanView = true, CanEdit = false, CanDelete = false },
                        new MenuPermissionDto { MenuName = "Employees", CanView = true, CanEdit = true, CanDelete = true },
                        new MenuPermissionDto { MenuName = "Reports", CanView = true, CanEdit = true, CanDelete = false },
                        new MenuPermissionDto { MenuName = "Administration", CanView = true, CanEdit = true, CanDelete = true }
                    });
                }
                else
                {
                    permissions.AddRange(new[]
                    {
                        new MenuPermissionDto { MenuName = "Dashboard", CanView = true, CanEdit = false, CanDelete = false },
                        new MenuPermissionDto { MenuName = "Profile", CanView = true, CanEdit = true, CanDelete = false }
                    });
                }

                return Ok(new MenuPermissionsResponse
                {
                    Success = true,
                    Data = permissions,
                    Message = "Permissions retrieved successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user permissions");
                return StatusCode(500, new MenuPermissionsResponse
                {
                    Success = false,
                    Message = $"Failed to get permissions: {ex.Message}"
                });
            }
        }

        // =============================================================================
        // HELPER METHODS
        // =============================================================================

        private string GetIconForRoute(string routePart)
        {
            return routePart.ToLower() switch
            {
                "dashboard" => "Dashboard",
                "employees" => "People",
                "time" => "Schedule",
                "reports" => "Assessment",
                "admin" => "Settings",
                "profile" => "Person",
                "onboarding" => "PersonAdd",
                "tasks" => "Assignment",
                _ => "Page"
            };
        }
    }

    // =============================================================================
    // DTO CLASSES FOR MENU CONTROLLER
    // =============================================================================

    public class MenuTestResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string[] Endpoints { get; set; } = Array.Empty<string>();
    }

    public class SimpleMenuDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Route { get; set; } = string.Empty;
        public string? Icon { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
    }

    public class MenuAccessDto
    {
        public bool CanView { get; set; }
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
    }

    public class MenuDashboardConfigDto
    {
        public bool ShowSidebar { get; set; }
        public string DefaultRoute { get; set; } = string.Empty;
        public string[] AllowedRoutes { get; set; } = Array.Empty<string>();
        public string NavigationStyle { get; set; } = string.Empty;
        public string Theme { get; set; } = string.Empty;
        public bool CollapsibleSidebar { get; set; }
        public bool ShowUserMenu { get; set; }
    }

    public class BreadcrumbDto
    {
        public string Name { get; set; } = string.Empty;
        public string Route { get; set; } = string.Empty;
        public string? Icon { get; set; }
    }

    public class MenuPermissionDto
    {
        public string MenuName { get; set; } = string.Empty;
        public bool CanView { get; set; }
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
    }

    public class MenuItemsResponse
    {
        public bool Success { get; set; }
        public List<SimpleMenuDto> Data { get; set; } = new();
        public string Message { get; set; } = string.Empty;
    }

    public class MenuAccessResponse
    {
        public bool Success { get; set; }
        public MenuAccessDto? Data { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class MenuDashboardConfigResponse
    {
        public bool Success { get; set; }
        public MenuDashboardConfigDto? Data { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class BreadcrumbsResponse
    {
        public bool Success { get; set; }
        public List<BreadcrumbDto> Data { get; set; } = new();
        public string Message { get; set; } = string.Empty;
    }

    public class MenuPermissionsResponse
    {
        public bool Success { get; set; }
        public List<MenuPermissionDto> Data { get; set; } = new();
        public string Message { get; set; } = string.Empty;
    }
}