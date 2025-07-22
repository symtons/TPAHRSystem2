using System.ComponentModel.DataAnnotations;

namespace TPAHRSystemSimple.Models
{
    public class Employee
    {
        public int Id { get; set; }

        [MaxLength(20)]
        public string EmployeeNumber { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string Email { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? Position { get; set; }

        public int? DepartmentId { get; set; }
        public int? UserId { get; set; }
        public int? ManagerId { get; set; }

        public DateTime? HireDate { get; set; }
        public DateTime? TerminationDate { get; set; }
        public DateTime? OnboardingCompletedDate { get; set; }

        [MaxLength(20)]
        public string Status { get; set; } = "Active";

        //[MaxLength(15)]
        //public string? Phone { get; set; }

        [MaxLength(200)]
        public string? Address { get; set; }

        [MaxLength(100)]
        public string? City { get; set; }

        [MaxLength(10)]
        public string? State { get; set; }

        [MaxLength(10)]
        public string? ZipCode { get; set; }

        public DateTime? DateOfBirth { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Computed property
        public string FullName => $"{FirstName} {LastName}";

        // Navigation properties
        public User? User { get; set; }
        public Department? Department { get; set; }
        public Employee? Manager { get; set; }
        public List<Employee> DirectReports { get; set; } = new List<Employee>();
    }

    public class Department
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        //public int? ManagerId { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public List<Employee> Employees { get; set; } = new List<Employee>();
    }
}