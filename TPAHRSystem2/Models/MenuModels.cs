using System.ComponentModel.DataAnnotations;

namespace TPAHRSystemSimple.Models
{
    public class MenuItem
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string Route { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? Icon { get; set; }

        [MaxLength(100)]
        public string? RequiredPermission { get; set; }

        public int? ParentId { get; set; }
        public int SortOrder { get; set; } = 0;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public MenuItem? Parent { get; set; }
        public List<MenuItem> Children { get; set; } = new List<MenuItem>();
        public List<RoleMenuPermission> RolePermissions { get; set; } = new List<RoleMenuPermission>();
    }

    public class RoleMenuPermission
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Role { get; set; } = string.Empty;

        public int MenuItemId { get; set; }
        public bool CanView { get; set; } = true;
        public bool CanEdit { get; set; } = false;
        public bool CanDelete { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public MenuItem MenuItem { get; set; } = null!;
    }

    // DTOs for API responses
    public class MenuItemDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Route { get; set; } = string.Empty;
        public string? Icon { get; set; }
        public int? ParentId { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
        public List<MenuItemDto> Children { get; set; } = new List<MenuItemDto>();

        // Permission info for current user
        public MenuPermissionDto? Permissions { get; set; }
    }

    public class MenuPermissionDto
    {
        public bool CanView { get; set; }
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
    }
}