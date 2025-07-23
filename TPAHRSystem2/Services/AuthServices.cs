using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using TPAHRSystemSimple.Data;
using TPAHRSystemSimple.Models;

namespace TPAHRSystemSimple.Services
{
    public class AuthService
    {
        private readonly TPADbContext _context;
        private readonly ILogger<AuthService> _logger;
        private const int SessionTimeoutHours = 8;
        private const int MaxFailedAttempts = 5;
        private const int LockoutMinutes = 15;

        public AuthService(TPADbContext context, ILogger<AuthService> logger)
        {
            _context = context;
            _logger = logger;
        }

        // =============================================================================
        // LOGIN
        // =============================================================================

        public async Task<LoginResponse> LoginAsync(LoginRequest request, string? ipAddress, string? userAgent)
        {
            try
            {
                _logger.LogInformation($"Login attempt for email: {request.Email}");

                // Find user by email
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.ToLower());

                if (user == null)
                {
                    _logger.LogWarning($"Login failed - user not found: {request.Email}");
                    return new LoginResponse
                    {
                        Success = false,
                        Message = "Invalid email or password"
                    };
                }

                // Check if account is locked
                if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTime.UtcNow)
                {
                    _logger.LogWarning($"Login failed - account locked: {request.Email}");
                    return new LoginResponse
                    {
                        Success = false,
                        Message = "Account is temporarily locked. Please try again later."
                    };
                }

                // Check if account is active
                if (!user.IsActive)
                {
                    _logger.LogWarning($"Login failed - account inactive: {request.Email}");
                    return new LoginResponse
                    {
                        Success = false,
                        Message = "Account is disabled. Please contact your administrator."
                    };
                }

                // Verify password
                if (!VerifyPassword(request.Password, user.PasswordHash, user.Salt))
                {
                    await HandleFailedLoginAsync(user);
                    _logger.LogWarning($"Login failed - invalid password: {request.Email}");
                    return new LoginResponse
                    {
                        Success = false,
                        Message = "Invalid email or password"
                    };
                }

                // Password is correct - reset failed attempts and create session
                await ResetFailedAttemptsAsync(user);
                var sessionToken = await CreateSessionAsync(user, ipAddress, userAgent);

                // Get employee data if exists
                var employee = await _context.Employees
                    .Include(e => e.Department)
                    .FirstOrDefaultAsync(e => e.UserId == user.Id);

                // Log successful login activity (DATABASE-DRIVEN)
                await LogLoginActivityAsync(user, employee, ipAddress);

                _logger.LogInformation($"Login successful for user: {request.Email}");

                return new LoginResponse
                {
                    Success = true,
                    Message = "Login successful",
                    Token = sessionToken,
                    User = MapToUserDto(user),
                    Employee = employee != null ? MapToEmployeeDto(employee) : null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error during login for email: {request.Email}");
                return new LoginResponse
                {
                    Success = false,
                    Message = "An error occurred during login. Please try again."
                };
            }
        }

        // =============================================================================
        // DATABASE-DRIVEN ACTIVITY LOGGING
        // =============================================================================

        private async Task LogLoginActivityAsync(User user, Employee? employee, string? ipAddress)
        {
            try
            {
                // Find or create login activity type
                var activityType = await _context.ActivityTypes
                    .FirstOrDefaultAsync(at => at.Name == "Login" && at.IsActive);

                if (activityType == null)
                {
                    // Create login activity type if it doesn't exist
                    activityType = new ActivityType
                    {
                        Name = "Login",
                        Description = "User login event",
                        IconName = "Login",
                        Color = "#4caf50",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.ActivityTypes.Add(activityType);
                    await _context.SaveChangesAsync();
                }

                // Create login activity record
                var activity = new RecentActivity
                {
                    UserId = user.Id,
                    EmployeeId = employee?.Id,
                    ActivityTypeId = activityType.Id,
                    Title = "User Login",
                    Description = $"Successful login from {ipAddress ?? "unknown location"}",
                    Metadata = $"{{\"ipAddress\": \"{ipAddress}\", \"role\": \"{user.Role}\"}}",
                    CreatedAt = DateTime.UtcNow
                };

                _context.RecentActivities.Add(activity);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Login activity logged for user: {user.Email}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to log login activity for user: {user.Email}");
                // Don't throw - login should still succeed even if activity logging fails
            }
        }

        // =============================================================================
        // SESSION MANAGEMENT (MATCHING ORIGINAL API)
        // =============================================================================

        public async Task<string> CreateSessionAsync(User user, string? ipAddress, string? userAgent)
        {
            // Use 64-byte token like the original (not 32-byte)
            var sessionToken = GenerateSessionToken();
            var expiresAt = DateTime.UtcNow.AddHours(SessionTimeoutHours);

            var session = new UserSession
            {
                UserId = user.Id,
                SessionToken = sessionToken,
                IPAddress = ipAddress,
                UserAgent = userAgent,
                ExpiresAt = expiresAt,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.UserSessions.Add(session);

            // Update user last login
            user.LastLogin = DateTime.UtcNow;
            user.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation($"Session created for user ID: {user.Id}, expires: {expiresAt}");
            return sessionToken;
        }

        public async Task<User?> ValidateSessionAsync(string sessionToken)
        {
            if (string.IsNullOrEmpty(sessionToken))
                return null;

            try
            {
                // EXACT same logic as original API - no IsActive check on User here
                var session = await _context.UserSessions
                    .Include(s => s.User)
                    .FirstOrDefaultAsync(s => s.SessionToken == sessionToken
                                           && s.IsActive
                                           && s.ExpiresAt > DateTime.UtcNow);

                // Return user directly if session exists and is valid (original behavior)
                return session?.User;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error validating session token");
                return null;
            }
        }

        public async Task<bool> LogoutAsync(string sessionToken)
        {
            try
            {
                var session = await _context.UserSessions
                    .FirstOrDefaultAsync(s => s.SessionToken == sessionToken && s.IsActive);

                if (session != null)
                {
                    session.IsActive = false;
                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"Session logged out: {sessionToken}");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error during logout for session: {sessionToken}");
                return false;
            }
        }

        // =============================================================================
        // PASSWORD & TOKEN UTILITIES (MATCHING ORIGINAL)
        // =============================================================================

        private static bool VerifyPassword(string password, string hash, string salt)
        {
            var computedHash = ComputeHash(password, salt);
            return computedHash == hash;
        }

        public static string ComputeHash(string password, string salt)
        {
            // EXACT same algorithm as original
            var combined = password + salt;
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(combined));
            return Convert.ToBase64String(hashBytes);
        }

        public static string GenerateSalt()
        {
            var saltBytes = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(saltBytes);
            return Convert.ToBase64String(saltBytes);
        }

        private static string GenerateSessionToken()
        {
            // EXACT same as original - 64 bytes (not 32)
            var tokenBytes = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(tokenBytes);
            return Convert.ToBase64String(tokenBytes);
        }

        // =============================================================================
        // FAILED LOGIN HANDLING
        // =============================================================================

        private async Task HandleFailedLoginAsync(User user)
        {
            user.FailedLoginAttempts++;
            user.UpdatedAt = DateTime.UtcNow;

            if (user.FailedLoginAttempts >= MaxFailedAttempts)
            {
                user.LockoutEnd = DateTime.UtcNow.AddMinutes(LockoutMinutes);
                _logger.LogWarning($"Account locked due to failed login attempts: {user.Email}");
            }

            await _context.SaveChangesAsync();
        }

        private async Task ResetFailedAttemptsAsync(User user)
        {
            user.FailedLoginAttempts = 0;
            user.LockoutEnd = null;
            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        // =============================================================================
        // MAPPING HELPERS
        // =============================================================================

        private static UserDto MapToUserDto(User user)
        {
            return new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                Role = user.Role,
                IsActive = user.IsActive,
                LastLogin = user.LastLogin,
                MustChangePassword = user.MustChangePassword
            };
        }

        private static EmployeeDto MapToEmployeeDto(Employee employee)
        {
            return new EmployeeDto
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
            };
        }
    }
}