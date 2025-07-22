using System.ComponentModel.DataAnnotations;

namespace TPAHRSystemSimple.Models
{
    // =============================================================================
    // AUTH REQUEST/RESPONSE DTOs
    // =============================================================================

    public class LoginRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }

    public class LoginResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Token { get; set; }
        public UserDto? User { get; set; }
        public EmployeeDto? Employee { get; set; }
    }

    public class UserDto
    {
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime? LastLogin { get; set; }
        public bool MustChangePassword { get; set; }
    }

    public class EmployeeDto
    {
        public int Id { get; set; }
        public string EmployeeNumber { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Position { get; set; }
        public string? Department { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime? HireDate { get; set; }
        public DateTime? OnboardingCompletedDate { get; set; }
    }

    public class AuthStatusResponse
    {
        public bool IsAuthenticated { get; set; }
        public UserDto? User { get; set; }
        public EmployeeDto? Employee { get; set; }
    }

    // =============================================================================
    // API RESPONSE WRAPPER
    // =============================================================================

    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }
        public List<string> Errors { get; set; } = new List<string>();

        public static ApiResponse<T> SuccessResult(T data, string message = "Success")
        {
            return new ApiResponse<T>
            {
                Success = true,
                Message = message,
                Data = data
            };
        }

        public static ApiResponse<T> ErrorResult(string message, List<string>? errors = null)
        {
            return new ApiResponse<T>
            {
                Success = false,
                Message = message,
                Errors = errors ?? new List<string>()
            };
        }
    }
}