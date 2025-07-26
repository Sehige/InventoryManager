// Models/SyncModels.cs
// Core synchronization models and interfaces for the local-first architecture

using System;
using System.Collections.Generic;

namespace InventoryManager.Models
{
    /// <summary>
    /// Sync status enumeration for tracking entity synchronization state
    /// </summary>
    public enum SyncStatus
    {
        /// <summary>Item is synchronized with cloud</summary>
        Synced = 0,
        /// <summary>Item has local changes pending sync</summary>
        Modified = 1,
        /// <summary>Item is newly created and not yet synced</summary>
        Created = 2,
        /// <summary>Item is marked for deletion</summary>
        Deleted = 3,
        /// <summary>Item has a sync conflict that needs resolution</summary>
        Conflict = 4
    }

    /// <summary>
    /// Interface that all syncable entities must implement
    /// This enables the sync engine to work with any entity type
    /// </summary>
    public interface ISyncable
    {
        /// <summary>Local database ID</summary>
        string LocalId { get; }

        /// <summary>Cloud service ID (null if not yet synced)</summary>
        string? CloudId { get; set; }

        /// <summary>Current synchronization status</summary>
        SyncStatus SyncStatus { get; set; }

        /// <summary>Last successful sync timestamp</summary>
        DateTime? LastSyncedAt { get; set; }

        /// <summary>ETag for optimistic concurrency control</summary>
        string? ETag { get; set; }
    }

    /// <summary>
    /// Tracks synchronization operations for audit and troubleshooting
    /// </summary>
    public class SyncLog
    {
        public int Id { get; set; }
        public string OperationType { get; set; } = string.Empty; // Pull, Push, Conflict, Error
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int RecordsProcessed { get; set; }
        public int ErrorCount { get; set; }
        public string? ErrorDetails { get; set; }
        public string Status { get; set; } = "InProgress"; // InProgress, Completed, Failed
    }

    /// <summary>
    /// Represents a synchronization conflict that needs resolution
    /// </summary>
    public class SyncConflict
    {
        public int Id { get; set; }
        public string EntityType { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public string LocalValue { get; set; } = string.Empty;
        public string RemoteValue { get; set; } = string.Empty;
        public DateTime DetectedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public string? ResolutionStrategy { get; set; }
        public string? ResolvedByUserId { get; set; }
    }

    /// <summary>
    /// Queues operations for later sync when offline
    /// </summary>
    public class OfflineQueue
    {
        public int Id { get; set; }
        public string EntityType { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public string Operation { get; set; } = string.Empty; // Create, Update, Delete
        public string Payload { get; set; } = string.Empty; // JSON serialized entity
        public DateTime QueuedAt { get; set; }
        public int RetryCount { get; set; }
        public string? LastError { get; set; }
    }

    /// <summary>
    /// Configuration for synchronization behavior
    /// </summary>
    public class SyncConfiguration
    {
        public bool AutoSyncEnabled { get; set; } = true;
        public TimeSpan SyncInterval { get; set; } = TimeSpan.FromMinutes(15);
        public ConflictResolutionStrategy DefaultConflictResolution { get; set; } = ConflictResolutionStrategy.ServerWins;
        public bool SyncOnlyOnWifi { get; set; } = false;
        public int MaxRetryAttempts { get; set; } = 3;
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(30);
        public bool EnableBackgroundSync { get; set; } = true;
    }

    /// <summary>
    /// Conflict resolution strategies
    /// </summary>
    public enum ConflictResolutionStrategy
    {
        /// <summary>Server data takes precedence</summary>
        ServerWins,
        /// <summary>Local data takes precedence</summary>
        ClientWins,
        /// <summary>Use the most recent timestamp</summary>
        LastWriteWins,
        /// <summary>Require manual resolution</summary>
        Manual
    }

    /// <summary>
    /// Result of a conflict resolution operation
    /// </summary>
    public class ConflictResolutionResult
    {
        public bool Success { get; set; }
        public string? ResolvedValue { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Device registration for managing authorized devices
    /// </summary>
    public class DeviceRegistration
    {
        public string DeviceId { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public string Platform { get; set; } = string.Empty; // iOS, Android, Windows
        public DateTime RegisteredAt { get; set; }
        public DateTime LastSeenAt { get; set; }
        public bool IsActive { get; set; } = true;
        public string RegisteredByUserId { get; set; } = string.Empty;
    }
}