// Models/User.cs - Updated existing file with ISyncable implementation
// This REPLACES the existing User.cs file - do not create a new file

using System;
using System.Collections.Generic;

namespace InventoryManager.Models
{
    /// <summary>
    /// Represents a user in the inventory management system
    /// Now implements ISyncable for cloud synchronization
    /// </summary>
    public class User : ISyncable
    {
        // Existing properties preserved exactly as they were
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime LastLoginAt { get; set; }
        public bool IsActive { get; set; } = true;

        // Navigation property
        public List<InventoryTransaction> Transactions { get; set; } = new();

        // NEW: Sync properties implementing ISyncable
        public SyncStatus SyncStatus { get; set; } = SyncStatus.Synced;
        public DateTime? LastSyncedAt { get; set; }
        public string? CloudId { get; set; }
        public string? ETag { get; set; }

        // ISyncable implementation
        public string LocalId => Id;
    }
}