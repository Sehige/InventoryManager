// Models/User.cs
// This file defines the structure of user data in our application
// Think of this as a blueprint that tells the database what information we want to store about each user

namespace InventoryManager.Models
{
    /// <summary>
    /// Represents a user in the inventory management system
    /// This class maps directly to a table in our SQLite database
    /// </summary>
    public class User
    {
        // Primary key - unique identifier for each user
        // Using string instead of int because we're using GUIDs for better security
        public string Id { get; set; } = string.Empty;

        // Username must be unique across the system
        // This is what users will type when logging in
        public string Username { get; set; } = string.Empty;

        // Display name for the user interface
        // This makes the app more personal and professional
        public string FullName { get; set; } = string.Empty;

        // We never store actual passwords - only the hashed version
        // BCrypt will transform "password123" into something like "$2a$10$N9qo8uLO..."
        public string PasswordHash { get; set; } = string.Empty;

        // Role-based access control: Admin, Manager, or Operator
        // This determines what features each user can access
        public string Role { get; set; } = string.Empty;

        // Audit fields - important for tracking user activity
        public DateTime CreatedAt { get; set; }
        public DateTime LastLoginAt { get; set; }

        // Soft delete - we don't actually remove users, just mark them as inactive
        // This preserves transaction history even after someone leaves the company
        public bool IsActive { get; set; } = true;

        // Navigation property - Entity Framework will use this to link users to their transactions
        // This creates a one-to-many relationship (one user can have many transactions)
        public List<InventoryTransaction> Transactions { get; set; } = new();
    }

    /// <summary>
    /// Represents a single inventory transaction (usage, restock, adjustment)
    /// This is where we track every change to inventory quantities
    /// </summary>
    public partial class InventoryTransaction
    {
        // Auto-incrementing primary key
        public int Id { get; set; }

        // Links to the material that was affected
        // In the future, this will reference a Material table
        public string MaterialId { get; set; } = string.Empty;

        // Links to the user who performed this transaction
        // Foreign key that references User.Id
        public string UserId { get; set; } = string.Empty;

        // Positive numbers for restocking, negative for usage
        // Example: +50 means 50 items were added, -10 means 10 were used
        public int QuantityChange { get; set; }

        // Type of transaction: "Usage", "Restock", "Adjustment", "Transfer"
        public string TransactionType { get; set; } = string.Empty;

        // When this transaction occurred - crucial for audit trails
        public DateTime Timestamp { get; set; }

        // Optional field for additional context
        // Example: "Used for Project ABC" or "Damaged items removed"
        public string Notes { get; set; } = string.Empty;

        // Navigation property back to the user
        // Entity Framework uses this to automatically load user details when needed
        public User User { get; set; } = null!;
    }
}