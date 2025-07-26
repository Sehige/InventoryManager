// Services/DatabaseService.cs - Updated with sync support
// Enhanced database service with sync tables and migrations

using Microsoft.EntityFrameworkCore;
using InventoryManager.Models;
using System;
using System.Threading.Tasks;

namespace InventoryManager.Services
{
    /// <summary>
    /// Enhanced database service with synchronization support
    /// </summary>
    public class DatabaseService : DbContext
    {
        // Existing tables
        public DbSet<User> Users { get; set; }
        public DbSet<InventoryItem> InventoryItems { get; set; }
        public DbSet<InventoryTransaction> InventoryTransactions { get; set; }

        // New sync-related tables
        public DbSet<SyncLog> SyncLogs { get; set; }
        public DbSet<SyncConflict> SyncConflicts { get; set; }
        public DbSet<OfflineQueue> OfflineQueues { get; set; }
        public DbSet<DeviceRegistration> DeviceRegistrations { get; set; }

        /// <summary>
        /// Database connection configuration
        /// </summary>
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "inventory.db");
            optionsBuilder.UseSqlite($"Data Source={dbPath}");

            // Enable detailed logging in debug mode
#if DEBUG
            optionsBuilder.EnableSensitiveDataLogging();
            optionsBuilder.LogTo(message => System.Diagnostics.Debug.WriteLine(message));
#endif
        }

        /// <summary>
        /// Model configuration with sync support
        /// </summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // User configuration (updated with sync properties)
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Username).IsUnique();
                entity.Property(e => e.Username).IsRequired().HasMaxLength(50);
                entity.Property(e => e.FullName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Role).IsRequired().HasMaxLength(20);
                entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(100);

                // Sync properties
                entity.Property(e => e.CloudId).HasMaxLength(50);
                entity.Property(e => e.ETag).HasMaxLength(100);
                entity.HasIndex(e => e.CloudId);
            });

            // InventoryItem configuration (updated with sync properties)
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

                // Configure decimal precision
                entity.Property(e => e.UnitCost).HasPrecision(10, 2);

                // Sync properties
                entity.Property(e => e.CloudId).HasMaxLength(50);
                entity.Property(e => e.ETag).HasMaxLength(100);
                entity.HasIndex(e => e.CloudId);
                entity.HasIndex(e => e.SyncStatus);
            });

            // InventoryTransaction configuration (updated with sync properties)
            modelBuilder.Entity<InventoryTransaction>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TransactionType).IsRequired().HasMaxLength(20);
                entity.Property(e => e.Notes).HasMaxLength(500);
                entity.Property(e => e.ScanSessionId).HasMaxLength(50);

                // Relationships
                entity.HasOne(e => e.User)
                      .WithMany(u => u.Transactions)
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.InventoryItem)
                      .WithMany(i => i.Transactions)
                      .HasForeignKey(e => e.InventoryItemId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Sync properties
                entity.Property(e => e.CloudId).HasMaxLength(50);
                entity.Property(e => e.ETag).HasMaxLength(100);
                entity.HasIndex(e => e.CloudId);
                entity.HasIndex(e => e.Timestamp);
            });

            // SyncLog configuration
            modelBuilder.Entity<SyncLog>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.OperationType).IsRequired().HasMaxLength(20);
                entity.Property(e => e.Status).IsRequired().HasMaxLength(20);
                entity.Property(e => e.ErrorDetails).HasMaxLength(2000);
                entity.HasIndex(e => e.StartedAt);
                entity.HasIndex(e => e.Status);
            });

            // SyncConflict configuration
            modelBuilder.Entity<SyncConflict>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.EntityType).IsRequired().HasMaxLength(50);
                entity.Property(e => e.EntityId).IsRequired().HasMaxLength(50);
                entity.Property(e => e.LocalValue).IsRequired();
                entity.Property(e => e.RemoteValue).IsRequired();
                entity.Property(e => e.ResolutionStrategy).HasMaxLength(20);
                entity.HasIndex(e => new { e.EntityType, e.EntityId });
                entity.HasIndex(e => e.ResolvedAt);
            });

            // OfflineQueue configuration
            modelBuilder.Entity<OfflineQueue>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.EntityType).IsRequired().HasMaxLength(50);
                entity.Property(e => e.EntityId).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Operation).IsRequired().HasMaxLength(20);
                entity.Property(e => e.Payload).IsRequired();
                entity.Property(e => e.LastError).HasMaxLength(1000);
                entity.HasIndex(e => e.QueuedAt);
                entity.HasIndex(e => new { e.EntityType, e.EntityId });
            });

            // DeviceRegistration configuration
            modelBuilder.Entity<DeviceRegistration>(entity =>
            {
                entity.HasKey(e => e.DeviceId);
                entity.Property(e => e.DeviceId).HasMaxLength(100);
                entity.Property(e => e.DeviceName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Platform).IsRequired().HasMaxLength(20);
                entity.Property(e => e.RegisteredByUserId).IsRequired();
                entity.HasIndex(e => e.RegisteredByUserId);
            });

            // Seed initial data
            SeedData(modelBuilder);
        }

        /// <summary>
        /// Initialize database and apply migrations
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Database initialization starting...");

                var dbPath = Path.Combine(FileSystem.AppDataDirectory, "inventory.db");
                System.Diagnostics.Debug.WriteLine($"Database path: {dbPath}");

                // Check if this is first run
                bool isFirstRun = !File.Exists(dbPath);
                System.Diagnostics.Debug.WriteLine($"Is first run: {isFirstRun}");

                if (isFirstRun)
                {
                    System.Diagnostics.Debug.WriteLine("First run detected - creating new database");

                    // For first run, just create the database with new schema
                    await Database.EnsureCreatedAsync();

                    // Add default admin user
                    var adminUser = new User
                    {
                        Id = Guid.NewGuid().ToString(),
                        Username = "admin",
                        FullName = "System Administrator",
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                        Role = "Admin",
                        CreatedAt = DateTime.UtcNow,
                        LastLoginAt = DateTime.UtcNow,
                        IsActive = true,
                        // Set sync properties with defaults
                        SyncStatus = SyncStatus.Synced,
                        LastSyncedAt = null,
                        CloudId = null,
                        ETag = null
                    };

                    Users.Add(adminUser);
                    await SaveChangesAsync();

                    System.Diagnostics.Debug.WriteLine("Default admin user created");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Existing database found");

                    // For existing database, we need to be more careful
                    try
                    {
                        // Try to open connection
                        await Database.OpenConnectionAsync();
                        await Database.CloseConnectionAsync();
                        System.Diagnostics.Debug.WriteLine("Database connection successful");

                        // TEMPORARILY: Don't do migrations, just work with what we have
                        // await ApplyManualMigrationsAsync();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Database error: {ex.Message}");

                        // If there's a problem, offer to reset
                        System.Diagnostics.Debug.WriteLine("Database appears corrupted - considering reset");

                        // DELETE the corrupted database and start fresh
                        try
                        {
                            File.Delete(dbPath);
                            System.Diagnostics.Debug.WriteLine("Deleted corrupted database");

                            // Recreate
                            await Database.EnsureCreatedAsync();

                            // Add default admin
                            var adminUser = new User
                            {
                                Id = Guid.NewGuid().ToString(),
                                Username = "admin",
                                FullName = "System Administrator",
                                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                                Role = "Admin",
                                CreatedAt = DateTime.UtcNow,
                                LastLoginAt = DateTime.UtcNow,
                                IsActive = true,
                                SyncStatus = SyncStatus.Synced
                            };

                            Users.Add(adminUser);
                            await SaveChangesAsync();

                            System.Diagnostics.Debug.WriteLine("Database recreated successfully");
                        }
                        catch (Exception resetEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to reset database: {resetEx.Message}");
                            throw;
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine("Database initialization completed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CRITICAL: Database initialization failed: {ex}");
                throw;
            }
        }

        /// <summary>
        /// Apply custom migrations for sync support
        /// </summary>
        private async Task ApplyCustomMigrationsAsync()
        {
            // Check if sync columns exist and add them if not
            // This is a simplified approach - in production, use proper migrations

            try
            {
                // Test if sync columns exist by trying to query them
                var testUser = await Users.Select(u => new { u.CloudId, u.SyncStatus }).FirstOrDefaultAsync();
            }
            catch
            {
                // If the query fails, the columns don't exist
                // In a real app, you'd use proper EF migrations
                System.Diagnostics.Debug.WriteLine("Sync columns not found - database migration may be needed");
            }
        }

        /// <summary>
        /// Seed initial data including sync configuration
        /// </summary>
        private void SeedData(ModelBuilder modelBuilder)
        {
            // Create default admin user if not exists
            var adminId = "default-admin-id";
            modelBuilder.Entity<User>().HasData(new User
            {
                Id = adminId,
                Username = "admin",
                FullName = "System Administrator",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                Role = "Admin",
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LastLoginAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                IsActive = true,
                SyncStatus = SyncStatus.Synced,
                LastSyncedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            });
        }

        #region Helper Methods

        /// <summary>
        /// Get user by username (existing method preserved)
        /// </summary>
        public async Task<User?> GetUserByUsernameAsync(string username)
        {
            return await Users
                .FirstOrDefaultAsync(u => u.Username == username && u.IsActive);
        }

        /// <summary>
        /// Update user last login (existing method preserved)
        /// </summary>
        public async Task UpdateUserLastLoginAsync(string userId)
        {
            var user = await Users.FindAsync(userId);
            if (user != null)
            {
                user.LastLoginAt = DateTime.UtcNow;
                // Mark as modified for sync
                if (user.SyncStatus == SyncStatus.Synced)
                {
                    user.SyncStatus = SyncStatus.Modified;
                }
                await SaveChangesAsync();
            }
        }

        /// <summary>
        /// Override SaveChangesAsync to handle sync status updates
        /// </summary>
        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            // Update sync status for modified entities
            var entries = ChangeTracker.Entries<ISyncable>()
                .Where(e => e.State == EntityState.Modified || e.State == EntityState.Added);

            foreach (var entry in entries)
            {
                if (entry.State == EntityState.Added && entry.Entity.SyncStatus == SyncStatus.Synced)
                {
                    entry.Entity.SyncStatus = SyncStatus.Created;
                }
                else if (entry.State == EntityState.Modified && entry.Entity.SyncStatus == SyncStatus.Synced)
                {
                    entry.Entity.SyncStatus = SyncStatus.Modified;
                }
            }

            return await base.SaveChangesAsync(cancellationToken);
        }

        #endregion

        /// <summary>
        /// Create a new user in the database
        /// </summary>
        public async Task<bool> CreateUserAsync(User user)
        {
            try
            {
                // Check if username already exists
                var existingUser = await Users
                    .FirstOrDefaultAsync(u => u.Username == user.Username);

                if (existingUser != null)
                {
                    return false; // Username already taken
                }

                // Add the new user
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
        /// <summary>
        /// FOR DEVELOPMENT ONLY: Delete and recreate the database
        /// </summary>
        public async Task ResetDatabaseAsync()
        {
            try
            {
                return;
                // Delete the existing database
                await Database.EnsureDeletedAsync();

                // Recreate with new schema
                await Database.EnsureCreatedAsync();

                // Add default admin user
                var adminUser = new User
                {
                    Id = Guid.NewGuid().ToString(),
                    Username = "admin",
                    FullName = "System Administrator",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                    Role = "Admin",
                    CreatedAt = DateTime.UtcNow,
                    LastLoginAt = DateTime.UtcNow,
                    IsActive = true,
                    SyncStatus = SyncStatus.Synced,
                    LastSyncedAt = DateTime.UtcNow
                };

                Users.Add(adminUser);
                await SaveChangesAsync();

                System.Diagnostics.Debug.WriteLine("Database reset completed with default admin user");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Database reset failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Get all users from the database
        /// </summary>
        public async Task<List<User>> GetAllUsersAsync()
        {
            try
            {
                return await Users
                    .Where(u => u.IsActive)
                    .OrderBy(u => u.FullName)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting all users: {ex.Message}");
                return new List<User>();
            }
        }

        /// <summary>
        /// Get user statistics for dashboard
        /// </summary>
        public async Task<Dictionary<string, object>> GetUserStatsAsync()
        {
            try
            {
                var stats = new Dictionary<string, object>();

                // Total users
                stats["TotalUsers"] = await Users.CountAsync(u => u.IsActive);

                // Users by role
                stats["AdminCount"] = await Users.CountAsync(u => u.IsActive && u.Role == "Admin");
                stats["ManagerCount"] = await Users.CountAsync(u => u.IsActive && u.Role == "Manager");
                stats["OperatorCount"] = await Users.CountAsync(u => u.IsActive && u.Role == "Operator");

                // Recent activity
                var recentLoginDate = DateTime.UtcNow.AddDays(-7);
                stats["RecentlyActiveUsers"] = await Users
                    .CountAsync(u => u.IsActive && u.LastLoginAt >= recentLoginDate);

                // Get recently created users
                var recentUsers = await Users
                    .Where(u => u.IsActive)
                    .OrderByDescending(u => u.CreatedAt)
                    .Take(5)
                    .Select(u => new { u.FullName, u.Username, u.CreatedAt })
                    .ToListAsync();

                stats["RecentUsers"] = recentUsers;

                return stats;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting user stats: {ex.Message}");
                return new Dictionary<string, object>();
            }
        }
    }
}