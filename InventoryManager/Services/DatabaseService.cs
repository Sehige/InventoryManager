// Services/DatabaseService.cs - SIMPLIFIED Database Service with Location Enum
// This version eliminates the complex location relationships and uses simple enums instead
// Much easier to understand, maintain, and use in practice

using Microsoft.EntityFrameworkCore;
using InventoryManager.Models;

namespace InventoryManager.Services
{
    /// <summary>
    /// Simplified database service that treats locations as enum values instead of database entities
    /// This approach is much more straightforward and eliminates complex relationships
    /// Perfect for warehouse scenarios where locations are predefined and don't change often
    /// </summary>
    public class DatabaseService : DbContext
    {
        // User management tables (unchanged from your original)
        public DbSet<User> Users { get; set; }
        public DbSet<InventoryTransaction> InventoryTransactions { get; set; }

        // Simplified inventory management - no location table needed!
        public DbSet<InventoryItem> InventoryItems { get; set; }

        /// <summary>
        /// Database connection configuration - same as before
        /// </summary>
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "inventory.db");
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }

        /// <summary>
        /// Simplified model configuration - much cleaner without location relationships
        /// </summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // User configuration (unchanged)
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Username).IsUnique();
                entity.Property(e => e.Username).IsRequired().HasMaxLength(50);
                entity.Property(e => e.FullName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Role).IsRequired().HasMaxLength(20);
                entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(100);
            });

            // Simplified InventoryItem configuration
            modelBuilder.Entity<InventoryItem>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ItemCode).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Description).HasMaxLength(1000);
                entity.Property(e => e.Unit).HasMaxLength(20);
                entity.Property(e => e.Category).HasMaxLength(100);
                entity.Property(e => e.Supplier).HasMaxLength(200);
                entity.Property(e => e.CreatedByUserId).IsRequired();
                entity.Property(e => e.LastModifiedByUserId).IsRequired();

                // Ensure item codes are unique among active items
                entity.HasIndex(e => new { e.ItemCode, e.IsActive })
                      .IsUnique()
                      .HasFilter("IsActive = 1");

                // Configure decimal precision for costs
                entity.Property(e => e.UnitCost).HasPrecision(10, 2);

                // Location is stored as an integer (enum value) - no foreign key needed!
                entity.Property(e => e.Location)
                      .HasConversion<int>(); // This converts enum to int for storage
            });

            // Simplified InventoryTransaction configuration
            modelBuilder.Entity<InventoryTransaction>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TransactionType).IsRequired();
                entity.Property(e => e.UserId).IsRequired();

                // Link to users
                entity.HasOne(e => e.User)
                      .WithMany(u => u.Transactions)
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Restrict);

                // Link to inventory items - much simpler now!
                entity.HasOne(e => e.InventoryItem)
                      .WithMany(i => i.Transactions)
                      .HasForeignKey(e => e.InventoryItemId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            base.OnModelCreating(modelBuilder);
        }

        /// <summary>
        /// Simplified initialization - no complex location setup needed
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                await Database.EnsureCreatedAsync();

                // Create default admin user
                if (!await Users.AnyAsync())
                {
                    await CreateDefaultAdminAsync();
                }

                // Create sample inventory items with enum locations
                if (!await InventoryItems.AnyAsync())
                {
                    await CreateSampleInventoryAsync();
                }

                System.Diagnostics.Debug.WriteLine("Database initialized successfully with simplified location system");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Database initialization error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Create default admin user (unchanged)
        /// </summary>
        private async Task CreateDefaultAdminAsync()
        {
            var adminUser = new User
            {
                Id = Guid.NewGuid().ToString(),
                Username = "admin",
                FullName = "System Administrator",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                Role = "Admin",
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.MinValue,
                IsActive = true
            };

            Users.Add(adminUser);
            await SaveChangesAsync();
            System.Diagnostics.Debug.WriteLine("Default admin user created successfully");
        }

        /// <summary>
        /// Create sample inventory with enum-based locations - much simpler!
        /// </summary>
        private async Task CreateSampleInventoryAsync()
        {
            var adminUser = await Users.FirstAsync(u => u.Username == "admin");

            var sampleItems = new[]
            {
                new InventoryItem
                {
                    ItemCode = "HW001",
                    Name = "Steel Bolts M8x40mm",
                    Description = "High-grade stainless steel bolts for construction",
                    CurrentQuantity = 500,
                    MinimumQuantity = 100,
                    MaximumQuantity = 1000,
                    Unit = "pieces",
                    Location = WarehouseLocation.MainWarehouse, // Simple enum assignment!
                    Category = "Hardware",
                    Supplier = "Steel Supply Co.",
                    UnitCost = 0.25m,
                    CreatedAt = DateTime.UtcNow,
                    LastModifiedAt = DateTime.UtcNow,
                    CreatedByUserId = adminUser.Id,
                    LastModifiedByUserId = adminUser.Id,
                    IsActive = true
                },

                new InventoryItem
                {
                    ItemCode = "CH001",
                    Name = "Industrial Lubricant",
                    Description = "High-performance synthetic lubricant for machinery",
                    CurrentQuantity = 25,
                    MinimumQuantity = 10,
                    MaximumQuantity = 50,
                    Unit = "liters",
                    Location = WarehouseLocation.ColdStorage,
                    Category = "Chemicals",
                    Supplier = "Chemical Solutions Ltd.",
                    UnitCost = 15.50m,
                    CreatedAt = DateTime.UtcNow,
                    LastModifiedAt = DateTime.UtcNow,
                    CreatedByUserId = adminUser.Id,
                    LastModifiedByUserId = adminUser.Id,
                    IsActive = true
                },

                new InventoryItem
                {
                    ItemCode = "SF001",
                    Name = "Safety Helmets",
                    Description = "OSHA-compliant safety helmets with chin strap",
                    CurrentQuantity = 8,
                    MinimumQuantity = 20, // Low stock for testing
                    MaximumQuantity = 100,
                    Unit = "pieces",
                    Location = WarehouseLocation.SafetyStation,
                    Category = "Safety Equipment",
                    Supplier = "Safety First Inc.",
                    UnitCost = 25.00m,
                    CreatedAt = DateTime.UtcNow,
                    LastModifiedAt = DateTime.UtcNow,
                    CreatedByUserId = adminUser.Id,
                    LastModifiedByUserId = adminUser.Id,
                    IsActive = true
                },

                new InventoryItem
                {
                    ItemCode = "TL001",
                    Name = "Drill Bit Set Professional",
                    Description = "High-speed steel drill bits 1-10mm with titanium coating",
                    CurrentQuantity = 12,
                    MinimumQuantity = 5,
                    MaximumQuantity = 25,
                    Unit = "sets",
                    Location = WarehouseLocation.Workshop,
                    Category = "Tools",
                    Supplier = "Tool Masters",
                    UnitCost = 45.00m,
                    CreatedAt = DateTime.UtcNow,
                    LastModifiedAt = DateTime.UtcNow,
                    CreatedByUserId = adminUser.Id,
                    LastModifiedByUserId = adminUser.Id,
                    IsActive = true
                },

                new InventoryItem
                {
                    ItemCode = "PK001",
                    Name = "Heavy Duty Packing Tape",
                    Description = "Reinforced packing tape 48mm x 50m for shipping",
                    CurrentQuantity = 75,
                    MinimumQuantity = 30,
                    MaximumQuantity = 200,
                    Unit = "rolls",
                    Location = WarehouseLocation.LoadingDock,
                    Category = "Packing Supplies",
                    Supplier = "Packaging Pro",
                    UnitCost = 3.50m,
                    CreatedAt = DateTime.UtcNow,
                    LastModifiedAt = DateTime.UtcNow,
                    CreatedByUserId = adminUser.Id,
                    LastModifiedByUserId = adminUser.Id,
                    IsActive = true
                },

                new InventoryItem
                {
                    ItemCode = "OF001",
                    Name = "Printer Paper A4",
                    Description = "High-quality 80gsm white paper, 500 sheets per ream",
                    CurrentQuantity = 15,
                    MinimumQuantity = 25, // Another low stock item
                    MaximumQuantity = 100,
                    Unit = "reams",
                    Location = WarehouseLocation.OfficeStorage,
                    Category = "Office Supplies",
                    Supplier = "Office Direct",
                    UnitCost = 4.75m,
                    CreatedAt = DateTime.UtcNow,
                    LastModifiedAt = DateTime.UtcNow,
                    CreatedByUserId = adminUser.Id,
                    LastModifiedByUserId = adminUser.Id,
                    IsActive = true
                }
            };

            InventoryItems.AddRange(sampleItems);
            await SaveChangesAsync();
            System.Diagnostics.Debug.WriteLine($"Created {sampleItems.Length} sample inventory items with enum locations");
        }

        // Keep all your existing user management methods unchanged
        public async Task<List<User>> GetAllUsersAsync()
        {
            return await Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToListAsync();
        }

        public async Task<User?> GetUserByUsernameAsync(string username)
        {
            return await Users.FirstOrDefaultAsync(u => u.Username == username && u.IsActive);
        }

        public async Task<bool> CreateUserAsync(User user)
        {
            try
            {
                Users.Add(user);
                await SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating user: {ex.Message}");
                return false;
            }
        }

        public async Task UpdateUserLastLoginAsync(string userId)
        {
            var user = await Users.FindAsync(userId);
            if (user != null)
            {
                user.LastLoginAt = DateTime.UtcNow;
                await SaveChangesAsync();
            }
        }

        public async Task<Dictionary<string, object>> GetUserStatsAsync()
        {
            var stats = new Dictionary<string, object>();

            stats["TotalUsers"] = await Users.CountAsync(u => u.IsActive);

            var roleStats = await Users
                .Where(u => u.IsActive)
                .GroupBy(u => u.Role)
                .Select(g => new { Role = g.Key, Count = g.Count() })
                .ToListAsync();

            stats["UsersByRole"] = roleStats;

            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
            stats["RecentlyActiveUsers"] = await Users
                .CountAsync(u => u.IsActive && u.LastLoginAt > thirtyDaysAgo);

            return stats;
        }

        /// <summary>
        /// Simplified inventory statistics - much easier to calculate without joins!
        /// </summary>
        public async Task<Dictionary<string, object>> GetInventoryStatsAsync()
        {
            var stats = new Dictionary<string, object>();

            try
            {
                stats["TotalItems"] = await InventoryItems.CountAsync(i => i.IsActive);
                stats["LowStockItems"] = await InventoryItems.CountAsync(i => i.IsActive && i.CurrentQuantity <= i.MinimumQuantity);
                stats["TotalInventoryValue"] = await InventoryItems
                    .Where(i => i.IsActive)
                    .SumAsync(i => i.CurrentQuantity * i.UnitCost);

                // Category breakdown - simple and fast
                var categoryStats = await InventoryItems
                    .Where(i => i.IsActive)
                    .GroupBy(i => i.Category)
                    .Select(g => new { Category = g.Key, Count = g.Count(), TotalValue = g.Sum(i => i.CurrentQuantity * i.UnitCost) })
                    .ToListAsync();

                stats["Categories"] = categoryStats;

                // Location breakdown using enum - no joins needed!
                var locationStats = await InventoryItems
                    .Where(i => i.IsActive)
                    .GroupBy(i => i.Location)
                    .Select(g => new { Location = g.Key.GetDisplayName(), Count = g.Count() })
                    .ToListAsync();

                stats["LocationBreakdown"] = locationStats;

                return stats;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting inventory stats: {ex.Message}");
                return stats;
            }
        }

        /// <summary>
        /// Create a test operator - now much simpler since location access is handled by role
        /// </summary>
        public async Task<User> CreateTestOperatorAsync()
        {
            var timestamp = DateTime.Now.ToString("MMddHHmmss");
            var testOperator = new User
            {
                Id = Guid.NewGuid().ToString(),
                Username = $"operator{timestamp}",
                FullName = $"Test Operator {timestamp}",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("test123"),
                Role = "Operator", // Role determines location access automatically
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            Users.Add(testOperator);
            await SaveChangesAsync();

            System.Diagnostics.Debug.WriteLine($"Test operator created: {testOperator.Username} - location access determined by role");
            return testOperator;
        }
    }
}