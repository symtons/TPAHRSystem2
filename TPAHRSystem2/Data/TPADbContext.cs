using Microsoft.EntityFrameworkCore;
using TPAHRSystemSimple.Models;

namespace TPAHRSystemSimple.Data
{
    public class TPADbContext : DbContext
    {
        public TPADbContext(DbContextOptions<TPADbContext> options) : base(options)
        {
        }

        // Core Authentication & User Management
        public DbSet<User> Users { get; set; }
        public DbSet<UserSession> UserSessions { get; set; }
        public DbSet<Employee> Employees { get; set; }
        public DbSet<Department> Departments { get; set; }

        // Menu & Permissions System  
        public DbSet<MenuItem> MenuItems { get; set; }
        public DbSet<RoleMenuPermission> RoleMenuPermissions { get; set; }

        // Dashboard & UI (DATABASE-DRIVEN)
        public DbSet<DashboardStat> DashboardStats { get; set; }
        public DbSet<QuickAction> QuickActions { get; set; }
        public DbSet<ActivityType> ActivityTypes { get; set; }
        public DbSet<RecentActivity> RecentActivities { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // =============================================================================
            // USER & AUTHENTICATION CONFIGURATION  
            // =============================================================================

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Email).IsUnique();

                entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
                entity.Property(e => e.PasswordHash).IsRequired();
                entity.Property(e => e.Salt).IsRequired();
                entity.Property(e => e.Role).IsRequired().HasMaxLength(50);
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.FailedLoginAttempts).HasDefaultValue(0);
                entity.Property(e => e.MustChangePassword).HasDefaultValue(false);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");
            });

            modelBuilder.Entity<UserSession>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.SessionToken).IsUnique();

                entity.Property(e => e.SessionToken).IsRequired().HasMaxLength(255);
                entity.Property(e => e.IPAddress).HasMaxLength(50);
                entity.Property(e => e.UserAgent).HasMaxLength(500);
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

                entity.HasOne(e => e.User)
                    .WithMany(u => u.UserSessions)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // =============================================================================
            // EMPLOYEE & DEPARTMENT CONFIGURATION
            // =============================================================================

            modelBuilder.Entity<Employee>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Email).IsUnique();
                entity.HasIndex(e => e.EmployeeNumber).IsUnique();

                entity.Property(e => e.EmployeeNumber).HasMaxLength(20);
                entity.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.LastName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
                entity.Property(e => e.Position).HasMaxLength(100);
                entity.Property(e => e.Status).HasMaxLength(20).HasDefaultValue("Active");
              //  entity.Property(e => e.Phone).HasMaxLength(15);
                entity.Property(e => e.Address).HasMaxLength(200);
                entity.Property(e => e.City).HasMaxLength(100);
                entity.Property(e => e.State).HasMaxLength(10);
                entity.Property(e => e.ZipCode).HasMaxLength(10);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");

                entity.HasOne(e => e.User)
                    .WithMany(u => u.Employees)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.Department)
                    .WithMany(d => d.Employees)
                    .HasForeignKey(e => e.DepartmentId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Manager)
                    .WithMany(m => m.DirectReports)
                    .HasForeignKey(e => e.ManagerId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<Department>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            });

            // =============================================================================
            // MENU SYSTEM CONFIGURATION
            // =============================================================================

            modelBuilder.Entity<MenuItem>(entity =>
            {
                entity.ToTable("MenuItems");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Route).IsRequired().HasMaxLength(255);
                entity.Property(e => e.Icon).HasMaxLength(100);
                entity.Property(e => e.RequiredPermission).HasMaxLength(100);
                entity.Property(e => e.SortOrder).HasDefaultValue(0);
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

                entity.HasOne(e => e.Parent)
                    .WithMany(e => e.Children)
                    .HasForeignKey(e => e.ParentId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(e => e.Route);
                entity.HasIndex(e => e.SortOrder);
            });

            modelBuilder.Entity<RoleMenuPermission>(entity =>
            {
                entity.ToTable("RoleMenuPermissions");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Role).IsRequired().HasMaxLength(50);
                entity.Property(e => e.CanView).HasDefaultValue(true);
                entity.Property(e => e.CanEdit).HasDefaultValue(false);
                entity.Property(e => e.CanDelete).HasDefaultValue(false);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

                entity.HasOne(e => e.MenuItem)
                    .WithMany(m => m.RolePermissions)
                    .HasForeignKey(e => e.MenuItemId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => new { e.Role, e.MenuItemId }).IsUnique();
            });

            // =============================================================================
            // DASHBOARD SYSTEM CONFIGURATION (DATABASE-DRIVEN)
            // =============================================================================

            modelBuilder.Entity<DashboardStat>(entity =>
            {
                entity.ToTable("DashboardStats");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.StatKey).IsRequired().HasMaxLength(100);
                entity.Property(e => e.StatName).IsRequired().HasMaxLength(200);
                entity.Property(e => e.StatValue).IsRequired().HasMaxLength(100);
                entity.Property(e => e.StatColor).IsRequired().HasMaxLength(50);
                entity.Property(e => e.IconName).HasMaxLength(100);
                entity.Property(e => e.Subtitle).HasMaxLength(200);
                entity.Property(e => e.ApplicableRoles).HasMaxLength(500);
                entity.Property(e => e.SortOrder).HasDefaultValue(0);
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.LastUpdated).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

                entity.HasIndex(e => e.StatKey);
            });

            modelBuilder.Entity<QuickAction>(entity =>
            {
                entity.ToTable("QuickActions");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.ActionKey).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.IconName).HasMaxLength(100);
                entity.Property(e => e.Route).HasMaxLength(255);
                entity.Property(e => e.Color).IsRequired().HasMaxLength(50);
                entity.Property(e => e.ApplicableRoles).HasMaxLength(500);
                entity.Property(e => e.SortOrder).HasDefaultValue(0);
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

                entity.HasIndex(e => e.ActionKey);
            });

            // =============================================================================
            // ACTIVITY TRACKING CONFIGURATION (DATABASE-DRIVEN)
            // =============================================================================

            modelBuilder.Entity<ActivityType>(entity =>
            {
                entity.ToTable("ActivityTypes");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Description).HasMaxLength(255);
                entity.Property(e => e.IconName).HasMaxLength(100);
                entity.Property(e => e.Color).IsRequired().HasMaxLength(50);
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            });

            modelBuilder.Entity<RecentActivity>(entity =>
            {
                entity.ToTable("RecentActivities");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.Metadata).HasMaxLength(1000);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Employee)
                    .WithMany()
                    .HasForeignKey(e => e.EmployeeId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.ActivityType)
                    .WithMany()
                    .HasForeignKey(e => e.ActivityTypeId);

                entity.HasIndex(e => new { e.UserId, e.CreatedAt });
            });
        }
    }
}