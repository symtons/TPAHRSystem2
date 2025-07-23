using System.ComponentModel.DataAnnotations;

namespace TPAHRSystemSimple.Models
{
    // =============================================================================
    // DATABASE-DRIVEN DASHBOARD ENTITIES
    // =============================================================================

    public class DashboardStat
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string StatKey { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string StatName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string StatValue { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string StatColor { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? IconName { get; set; }

        [MaxLength(200)]
        public string? Subtitle { get; set; }

        [MaxLength(500)]
        public string? ApplicableRoles { get; set; }

        public int SortOrder { get; set; } = 0;
        public bool IsActive { get; set; } = true;
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class QuickAction
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string ActionKey { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(100)]
        public string? IconName { get; set; }

        [MaxLength(255)]
        public string? Route { get; set; }

        [Required]
        [MaxLength(50)]
        public string Color { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? ApplicableRoles { get; set; }

        public int SortOrder { get; set; } = 0;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class ActivityType
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(255)]
        public string? Description { get; set; }

        [MaxLength(100)]
        public string? IconName { get; set; }

        [Required]
        [MaxLength(50)]
        public string Color { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class RecentActivity
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public int? EmployeeId { get; set; }
        public int ActivityTypeId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(1000)]
        public string? Metadata { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public User User { get; set; } = null!;
        public Employee? Employee { get; set; }
        public ActivityType ActivityType { get; set; } = null!;
    }

    // =============================================================================
    // DTOs FOR API RESPONSES
    // =============================================================================

    public class DashboardStatDto
    {
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public string? Icon { get; set; }
        public string? Subtitle { get; set; }
    }

    public class QuickActionDto
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string? Icon { get; set; }
        public string Color { get; set; } = string.Empty;
        public string? Route { get; set; }
    }

    public class RecentActivityDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime Timestamp { get; set; }
        public string Type { get; set; } = string.Empty;
        public string? Icon { get; set; }
        public string Color { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
    }
}