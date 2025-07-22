using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TPAHRSystemSimple.Data;
using TPAHRSystemSimple.Models;
using TPAHRSystemSimple.Services;

namespace TPAHRSystemSimple.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
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
        // AUTHENTICATION ENDPOINTS
        // =============================================================================

        /// <summary>
        /// Login with email and password
        /// </summary>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
             if (!ModelState.IsValid)
                {
                    return BadRequest(ApiResponse<object>.ErrorResult("Invalid input data"));
                }

                var ipAddress = GetClientIpAddress();
                var userAgent = Request.Headers.UserAgent.ToString();

                var result = await _authService.LoginAsync(request, ipAddress, userAgent);

                if (result.Success)
                {
                    _logger.LogInformation($"User logged in successfully: {request.Email}");
                    return Ok(result);
                }

                _logger.LogWarning($"Login failed for user: {request.Email} - {result.Message}");
                return Unauthorized(result);
          
        }

        /// <summary>
        /// Logout and invalidate session
        /// </summary>
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            try
            {
                var sessionToken = GetSessionToken();
                if (string.IsNullOrEmpty(sessionToken))
                {
                    return Ok(ApiResponse<object>.SuccessResult(null, "Already logged out"));
                }

                var success = await _authService.LogoutAsync(sessionToken);

                if (success)
                {
                    _logger.LogInformation("User logged out successfully");
                    return Ok(ApiResponse<object>.SuccessResult(null, "Logged out successfully"));
                }

                return Ok(ApiResponse<object>.SuccessResult(null, "Session already invalidated"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout");
                return StatusCode(500, ApiResponse<object>.ErrorResult("An error occurred during logout"));
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
                    return Unauthorized(ApiResponse<object>.ErrorResult("Not authenticated"));
                }

                var user = await _authService.ValidateSessionAsync(sessionToken);
                if (user == null)
                {
                    return Unauthorized(ApiResponse<object>.ErrorResult("Session expired or invalid"));
                }

                // Get employee data if exists
                var employee = await _context.Employees
                    .Include(e => e.Department)
                    .FirstOrDefaultAsync(e => e.UserId == user.Id);

                var response = new AuthStatusResponse
                {
                    IsAuthenticated = true,
                    User = new UserDto
                    {
                        Id = user.Id,
                        Email = user.Email,
                        Role = user.Role,
                        IsActive = user.IsActive,
                        LastLogin = user.LastLogin,
                        MustChangePassword = user.MustChangePassword
                    },
                    Employee = employee != null ? new EmployeeDto
                    {
                        Id = employee.Id,
                        EmployeeNumber = employee.EmployeeNumber,
                        FirstName = employee.FirstName,
                        LastName = employee.LastName,
                        FullName = employee.FullName,
                        Email = employee.Email,
                        Position = employee.Position,
                        Department = employee.Department?.Name,
                        Status = employee.Status,
                        HireDate = employee.HireDate,
                        OnboardingCompletedDate = employee.OnboardingCompletedDate
                    } : null
                };

                return Ok(ApiResponse<AuthStatusResponse>.SuccessResult(response));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current user");
                return StatusCode(500, ApiResponse<object>.ErrorResult("An error occurred"));
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
        // HELPER METHODS
        // =============================================================================

        private string? GetSessionToken()
        {
            // Try Bearer token first (standard)
            var authHeader = Request.Headers.Authorization.FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
            {
                return authHeader["Bearer ".Length..].Trim();
            }

            // Try X-Session-Token header (custom)
            var sessionHeader = Request.Headers["X-Session-Token"].FirstOrDefault();
            if (!string.IsNullOrEmpty(sessionHeader))
            {
                return sessionHeader;
            }

            return null;
        }

        private string? GetClientIpAddress()
        {
            // Try to get the real IP from reverse proxy headers
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

            // Fall back to connection remote IP
            return HttpContext.Connection.RemoteIpAddress?.ToString();
        }
    }
}