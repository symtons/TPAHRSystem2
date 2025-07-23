using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Cors;
using TPAHRSystemSimple.Data;
using TPAHRSystemSimple.Models;
using TPAHRSystemSimple.Services;
using System.ComponentModel.DataAnnotations;

namespace TPAHRSystemSimple.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [EnableCors("AllowReactApp")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;
        private readonly TPADbContext _context;
        private readonly ILogger<AuthController> _logger;

        public AuthController(AuthService authService, TPADbContext context, ILogger<AuthController> logger)
        {
            _authService = authService;
            _context = context;
            _logger = logger;
        }

        // =============================================================================
        // AUTHENTICATION ENDPOINTS (MATCHING ORIGINAL API EXACTLY)
        // =============================================================================

        /// <summary>
        /// Login with email and password
        /// </summary>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestModel request)
        {
            try
            {
                // Add CORS headers manually for extra compatibility
                Response.Headers.Add("Access-Control-Allow-Origin", "*");
                Response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
                Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization");

                if (!ModelState.IsValid)
                {
                    return BadRequest(new { success = false, message = "Invalid input data" });
                }

                var ipAddress = GetClientIpAddress();
                var userAgent = Request.Headers.UserAgent.ToString();

                var loginRequest = new LoginRequest
                {
                    Email = request.Email,
                    Password = request.Password
                };

                var result = await _authService.LoginAsync(loginRequest, ipAddress, userAgent);

                if (result.Success)
                {
                    _logger.LogInformation($"User logged in successfully: {request.Email}");

                    // Return EXACT format that frontend expects
                    return Ok(new
                    {
                        success = true,
                        message = result.Message,
                        token = result.Token,      // Session token for Authorization header
                        user = result.User,        // User data  
                        employee = result.Employee // Employee data if exists
                    });
                }

                _logger.LogWarning($"Login failed for user: {request.Email} - {result.Message}");
                return Unauthorized(new
                {
                    success = false,
                    message = result.Message,
                    token = (string?)null,
                    user = (object?)null,
                    employee = (object?)null
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error during login for email: {request.Email}");
                return StatusCode(500, new { success = false, message = "An error occurred during login" });
            }
        }

        /// <summary>
        /// Handle CORS preflight requests
        /// </summary>
        [HttpOptions("login")]
        public IActionResult PreflightLogin()
        {
            return Ok();
        }

        /// <summary>
        /// Logout and invalidate session (MATCHING ORIGINAL API)
        /// </summary>
        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] LogoutRequestModel? request = null)
        {
            try
            {
                string? sessionToken = null;

                // Try to get token from request body first (original behavior)
                if (request != null && !string.IsNullOrEmpty(request.Token))
                {
                    sessionToken = request.Token;
                }
                else
                {
                    // Fallback to Authorization header
                    sessionToken = GetSessionToken();
                }

                if (string.IsNullOrEmpty(sessionToken))
                {
                    return Ok(new { success = true, message = "Already logged out" });
                }

                var success = await _authService.LogoutAsync(sessionToken);

                return Ok(new
                {
                    success = success,
                    message = success ? "Logout successful" : "Invalid token"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout");
                return StatusCode(500, new { success = false, message = "An error occurred during logout" });
            }
        }

        /// <summary>
        /// Validate token - required by frontend on app start (MATCHING ORIGINAL API EXACTLY)
        /// </summary>
        [HttpGet("validate")]
        public async Task<IActionResult> ValidateToken([FromQuery] string token)
        {
            try
            {
                if (string.IsNullOrEmpty(token))
                {
                    return Unauthorized(new { success = false, message = "Token is required" });
                }

                // Use the EXACT same method as original API
                var user = await _authService.ValidateSessionAsync(token);
                if (user == null)
                {
                    return Unauthorized(new { success = false, message = "Invalid or expired token" });
                }

                // Get employee data if exists - SAME as original
                var employee = await _context.Employees
                    .Include(e => e.Department)
                    .FirstOrDefaultAsync(e => e.UserId == user.Id);

                // EXACT response format as original API that frontend expects
                var response = new
                {
                    success = true,
                    user = new
                    {
                        id = user.Id,
                        email = user.Email,
                        role = user.Role,
                        isActive = user.IsActive,
                        lastLogin = user.LastLogin,
                        mustChangePassword = user.MustChangePassword
                    },
                    employee = employee != null ? new
                    {
                        id = employee.Id,
                        employeeNumber = employee.EmployeeNumber,
                        firstName = employee.FirstName,
                        lastName = employee.LastName,
                        fullName = employee.FullName,
                        email = employee.Email,
                        position = employee.Position,
                        department = employee.Department?.Name,
                        status = employee.Status,
                        hireDate = employee.HireDate,
                        onboardingCompletedDate = employee.OnboardingCompletedDate
                    } : null
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating token");
                return StatusCode(500, new { success = false, message = "An error occurred validating token" });
            }
        }

        /// <summary>
        /// Get current user information
        /// </summary>
        [HttpGet("me")]
        public async Task<IActionResult> GetCurrentUser()
        {
            try
            {
                var sessionToken = GetSessionToken();
                if (string.IsNullOrEmpty(sessionToken))
                {
                    return Unauthorized(new { success = false, message = "Not authenticated" });
                }

                var user = await _authService.ValidateSessionAsync(sessionToken);
                if (user == null)
                {
                    return Unauthorized(new { success = false, message = "Session expired or invalid" });
                }

                // Get employee data if exists
                var employee = await _context.Employees
                    .Include(e => e.Department)
                    .FirstOrDefaultAsync(e => e.UserId == user.Id);

                var response = new
                {
                    success = true,
                    isAuthenticated = true,
                    user = new
                    {
                        id = user.Id,
                        email = user.Email,
                        role = user.Role,
                        isActive = user.IsActive,
                        lastLogin = user.LastLogin,
                        mustChangePassword = user.MustChangePassword
                    },
                    employee = employee != null ? new
                    {
                        id = employee.Id,
                        employeeNumber = employee.EmployeeNumber,
                        firstName = employee.FirstName,
                        lastName = employee.LastName,
                        fullName = employee.FullName,
                        email = employee.Email,
                        position = employee.Position,
                        department = employee.Department?.Name,
                        status = employee.Status,
                        hireDate = employee.HireDate,
                        onboardingCompletedDate = employee.OnboardingCompletedDate
                    } : null
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current user");
                return StatusCode(500, new { success = false, message = "An error occurred" });
            }
        }

        /// <summary>
        /// Health check for authentication service
        /// </summary>
        [HttpGet("health")]
        public IActionResult Health()
        {
            return Ok(new
            {
                success = true,
                message = "Authentication service is healthy",
                timestamp = DateTime.UtcNow,
                service = "Authentication API"
            });
        }

        // =============================================================================
        // HELPER METHODS (MATCHING ORIGINAL API EXACTLY)
        // =============================================================================

        private string? GetSessionToken()
        {
            // EXACT same logic as original - try Authorization header first
            var authHeader = Request.Headers.Authorization.FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
            {
                return authHeader["Bearer ".Length..].Trim();
            }

            // Try X-Session-Token header as fallback
            var sessionHeader = Request.Headers["X-Session-Token"].FirstOrDefault();
            if (!string.IsNullOrEmpty(sessionHeader))
            {
                return sessionHeader;
            }

            return null;
        }

        private string? GetClientIpAddress()
        {
            // EXACT same logic as original
            var xForwardedFor = Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(xForwardedFor))
            {
                return xForwardedFor.Split(',')[0].Trim();
            }

            var xRealIp = Request.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrEmpty(xRealIp))
            {
                return xRealIp;
            }

            return HttpContext.Connection.RemoteIpAddress?.ToString();
        }
    }

    // =============================================================================
    // REQUEST MODELS (SIMPLE CLASSES FOR CONTROLLER USE)
    // =============================================================================

    public class LoginRequestModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }

    public class LogoutRequestModel
    {
        public string Token { get; set; } = string.Empty;
    }
}