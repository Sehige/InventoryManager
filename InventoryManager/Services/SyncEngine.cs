// Services/SyncEngine.cs
// Core synchronization engine that manages the sync process

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using InventoryManager.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InventoryManager.Services
{
    /// <summary>
    /// Interface for the synchronization service
    /// </summary>
    public interface ISyncService
    {
        Task<bool> SyncAllAsync(CancellationToken cancellationToken = default);
        Task<bool> SyncTableAsync(string tableName, CancellationToken cancellationToken = default);
        Task<int> GetPendingChangesCountAsync();
        Task<List<SyncConflict>> GetUnresolvedConflictsAsync();
        Task<bool> ResolveConflictAsync(int conflictId, ConflictResolutionStrategy strategy);
        Task<SyncLog?> GetLastSyncLogAsync();
        void StartBackgroundSync();
        void StopBackgroundSync();
    }

    /// <summary>
    /// Main synchronization engine implementation
    /// </summary>
    public class SyncEngine : ISyncService
    {
        private readonly DatabaseService _database;
        private readonly ICloudApiClient _cloudApi;
        private readonly IConflictResolver _conflictResolver;
        private readonly IConnectivityService _connectivity;
        private readonly ILogger<SyncEngine> _logger;
        private readonly SyncConfiguration _config;

        private Timer? _backgroundSyncTimer;
        private bool _isSyncing;
        private readonly SemaphoreSlim _syncSemaphore = new(1, 1);

        public SyncEngine(
            DatabaseService database,
            ICloudApiClient cloudApi,
            IConflictResolver conflictResolver,
            IConnectivityService connectivity,
            ILogger<SyncEngine> logger,
            SyncConfiguration config)
        {
            _database = database;
            _cloudApi = cloudApi;
            _conflictResolver = conflictResolver;
            _connectivity = connectivity;
            _logger = logger;
            _config = config;
        }

        /// <summary>
        /// Sync all tables with the cloud
        /// </summary>
        public async Task<bool> SyncAllAsync(CancellationToken cancellationToken = default)
        {
            if (!await CanSyncAsync())
                return false;

            await _syncSemaphore.WaitAsync(cancellationToken);
            try
            {
                if (_isSyncing)
                {
                    _logger.LogWarning("Sync already in progress");
                    return false;
                }

                _isSyncing = true;
                var syncLog = await StartSyncLogAsync("Pull");

                try
                {
                    // Phase 1: Push local changes
                    var pushResult = await PushLocalChangesAsync(cancellationToken);
                    if (!pushResult.Success)
                    {
                        await FailSyncLogAsync(syncLog, pushResult.ErrorMessage);
                        return false;
                    }

                    // Phase 2: Pull remote changes
                    var pullResult = await PullRemoteChangesAsync(cancellationToken);
                    if (!pullResult.Success)
                    {
                        await FailSyncLogAsync(syncLog, pullResult.ErrorMessage);
                        return false;
                    }

                    // Phase 3: Process any conflicts
                    var conflictResult = await ProcessConflictsAsync(cancellationToken);

                    await CompleteSyncLogAsync(syncLog,
                        pushResult.RecordsProcessed + pullResult.RecordsProcessed,
                        conflictResult.ConflictCount);

                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Sync failed");
                    await FailSyncLogAsync(syncLog, ex.Message);
                    return false;
                }
            }
            finally
            {
                _isSyncing = false;
                _syncSemaphore.Release();
            }
        }

        /// <summary>
        /// Sync a specific table
        /// </summary>
        public async Task<bool> SyncTableAsync(string tableName, CancellationToken cancellationToken = default)
        {
            if (!await CanSyncAsync())
                return false;

            await _syncSemaphore.WaitAsync(cancellationToken);
            try
            {
                _logger.LogInformation($"Syncing table: {tableName}");

                // Get last sync time for this table
                var lastSync = await GetLastSyncTimeAsync(tableName);

                // Pull changes from cloud
                var changes = await _cloudApi.GetChangesAsync(lastSync, tableName);
                if (changes == null)
                    return false;

                // Apply changes locally
                foreach (var change in changes.Changes)
                {
                    await ApplyRemoteChangeAsync(change, cancellationToken);
                }

                // Handle deletions
                foreach (var deletion in changes.Deletions)
                {
                    await ApplyRemoteDeletionAsync(deletion, cancellationToken);
                }

                return true;
            }
            finally
            {
                _syncSemaphore.Release();
            }
        }

        /// <summary>
        /// Get count of pending changes
        /// </summary>
        public async Task<int> GetPendingChangesCountAsync()
        {
            var count = 0;

            count += await _database.Users
                .CountAsync(u => u.SyncStatus != SyncStatus.Synced);

            count += await _database.InventoryItems
                .CountAsync(i => i.SyncStatus != SyncStatus.Synced);

            count += await _database.InventoryTransactions
                .CountAsync(t => t.SyncStatus != SyncStatus.Synced);

            count += await _database.Set<OfflineQueue>()
                .CountAsync();

            return count;
        }

        /// <summary>
        /// Get unresolved conflicts
        /// </summary>
        public async Task<List<SyncConflict>> GetUnresolvedConflictsAsync()
        {
            return await _database.Set<SyncConflict>()
                .Where(c => c.ResolvedAt == null)
                .OrderByDescending(c => c.DetectedAt)
                .ToListAsync();
        }

        /// <summary>
        /// Resolve a specific conflict
        /// </summary>
        public async Task<bool> ResolveConflictAsync(int conflictId, ConflictResolutionStrategy strategy)
        {
            var conflict = await _database.Set<SyncConflict>().FindAsync(conflictId);
            if (conflict == null || conflict.ResolvedAt != null)
                return false;

            var result = await _conflictResolver.ResolveAsync(conflict, strategy);
            if (result.Success)
            {
                conflict.ResolvedAt = DateTime.UtcNow;
                conflict.ResolutionStrategy = strategy.ToString();
                await _database.SaveChangesAsync();

                // Apply the resolution
                await ApplyConflictResolutionAsync(conflict, result);
            }

            return result.Success;
        }

        /// <summary>
        /// Get the last sync log
        /// </summary>
        public async Task<SyncLog?> GetLastSyncLogAsync()
        {
            return await _database.Set<SyncLog>()
                .OrderByDescending(l => l.StartedAt)
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// Start background synchronization
        /// </summary>
        public void StartBackgroundSync()
        {
            if (!_config.EnableBackgroundSync)
                return;

            _logger.LogInformation("Starting background sync");

            _backgroundSyncTimer = new Timer(
                async _ => await BackgroundSyncCallback(),
                null,
                _config.SyncInterval,
                _config.SyncInterval);
        }

        /// <summary>
        /// Stop background synchronization
        /// </summary>
        public void StopBackgroundSync()
        {
            _logger.LogInformation("Stopping background sync");
            _backgroundSyncTimer?.Dispose();
            _backgroundSyncTimer = null;
        }

        #region Private Methods

        private async Task<bool> CanSyncAsync()
        {
            // Check connectivity
            if (!await _connectivity.IsConnectedAsync())
            {
                _logger.LogWarning("No internet connection available");
                return false;
            }

            // Check if wifi-only sync is enabled
            if (_config.SyncOnlyOnWifi && !await _connectivity.IsWifiConnectedAsync())
            {
                _logger.LogWarning("Sync requires WiFi connection");
                return false;
            }

            // Check if cloud API is reachable
            if (!await _cloudApi.IsConnectedAsync())
            {
                _logger.LogWarning("Cloud service is not reachable");
                return false;
            }

            return true;
        }

        private async Task BackgroundSyncCallback()
        {
            if (!_config.AutoSyncEnabled)
                return;

            try
            {
                _logger.LogDebug("Background sync triggered");
                await SyncAllAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background sync failed");
            }
        }

        private async Task<PushResult> PushLocalChangesAsync(CancellationToken cancellationToken)
        {
            try
            {
                var changes = new List<SyncChange>();

                // Collect all local changes
                changes.AddRange(await GetLocalChangesAsync<User>("Users"));
                changes.AddRange(await GetLocalChangesAsync<InventoryItem>("InventoryItems"));
                changes.AddRange(await GetLocalChangesAsync<InventoryTransaction>("InventoryTransactions"));

                if (changes.Count == 0)
                {
                    _logger.LogDebug("No local changes to push");
                    return new PushResult { Success = true };
                }

                // Push changes to cloud
                var response = await _cloudApi.PushChangesAsync(changes);
                if (response == null)
                {
                    return new PushResult
                    {
                        Success = false,
                        ErrorMessage = "Failed to push changes to cloud"
                    };
                }

                // Mark successfully synced items
                await MarkItemsAsSyncedAsync(changes, response);

                return new PushResult
                {
                    Success = true,
                    RecordsProcessed = response.Accepted
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pushing local changes");
                return new PushResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        private async Task<PullResult> PullRemoteChangesAsync(CancellationToken cancellationToken)
        {
            try
            {
                var lastSync = await GetLastSyncTimeAsync();
                var response = await _cloudApi.GetChangesAsync(lastSync);

                if (response == null)
                {
                    return new PullResult
                    {
                        Success = false,
                        ErrorMessage = "Failed to pull changes from cloud"
                    };
                }

                var processed = 0;

                // Apply remote changes
                foreach (var change in response.Changes)
                {
                    if (await ApplyRemoteChangeAsync(change, cancellationToken))
                        processed++;
                }

                // Apply deletions
                foreach (var deletion in response.Deletions)
                {
                    if (await ApplyRemoteDeletionAsync(deletion, cancellationToken))
                        processed++;
                }

                return new PullResult
                {
                    Success = true,
                    RecordsProcessed = processed
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pulling remote changes");
                return new PullResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        private async Task<ConflictProcessResult> ProcessConflictsAsync(CancellationToken cancellationToken)
        {
            var conflicts = await GetUnresolvedConflictsAsync();
            var autoResolved = 0;

            foreach (var conflict in conflicts)
            {
                if (await _conflictResolver.CanAutoResolveAsync(conflict))
                {
                    var strategy = await _conflictResolver.GetDefaultStrategyAsync(
                        Type.GetType(conflict.EntityType) ?? typeof(object));

                    if (await ResolveConflictAsync(conflict.Id, strategy))
                        autoResolved++;
                }
            }

            return new ConflictProcessResult
            {
                ConflictCount = conflicts.Count,
                AutoResolved = autoResolved
            };
        }

        private async Task<List<SyncChange>> GetLocalChangesAsync<T>(string tableName) where T : class, ISyncable
        {
            var changes = new List<SyncChange>();
            var items = await _database.Set<T>()
                .Where(i => i.SyncStatus != SyncStatus.Synced)
                .ToListAsync();

            foreach (var item in items)
            {
                changes.Add(new SyncChange
                {
                    Table = tableName,
                    Operation = item.SyncStatus switch
                    {
                        SyncStatus.Created => "CREATE",
                        SyncStatus.Modified => "UPDATE",
                        SyncStatus.Deleted => "DELETE",
                        _ => "UNKNOWN"
                    },
                    Id = item.CloudId ?? item.LocalId,
                    Data = item,
                    Timestamp = DateTime.UtcNow,
                    ETag = item.ETag ?? ""
                });
            }

            return changes;
        }

        private async Task<bool> ApplyRemoteChangeAsync(SyncChange change, CancellationToken cancellationToken)
        {
            try
            {
                switch (change.Table.ToLower())
                {
                    case "users":
                        return await ApplyUserChangeAsync(change);
                    case "inventoryitems":
                        return await ApplyInventoryItemChangeAsync(change);
                    case "inventorytransactions":
                        return await ApplyTransactionChangeAsync(change);
                    default:
                        _logger.LogWarning($"Unknown table: {change.Table}");
                        return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to apply remote change for {change.Table}:{change.Id}");

                // Create conflict record
                await CreateConflictAsync(change, ex.Message);
                return false;
            }
        }

        private async Task<bool> ApplyRemoteDeletionAsync(SyncDeletion deletion, CancellationToken cancellationToken)
        {
            try
            {
                switch (deletion.Table.ToLower())
                {
                    case "inventoryitems":
                        var item = await _database.InventoryItems
                            .FirstOrDefaultAsync(i => i.CloudId == deletion.Id);
                        if (item != null)
                        {
                            item.IsActive = false;
                            item.SyncStatus = SyncStatus.Synced;
                            await _database.SaveChangesAsync();
                        }
                        return true;

                    default:
                        _logger.LogWarning($"Deletion not supported for table: {deletion.Table}");
                        return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to apply deletion for {deletion.Table}:{deletion.Id}");
                return false;
            }
        }

        // Helper methods for applying specific entity changes
        private async Task<bool> ApplyUserChangeAsync(SyncChange change)
        {
            var userData = JsonSerializer.Deserialize<User>(change.Data.ToString() ?? "{}");
            if (userData == null) return false;

            var existingUser = await _database.Users
                .FirstOrDefaultAsync(u => u.CloudId == change.Id || u.Id == userData.Id);

            if (existingUser == null && change.Operation == "CREATE")
            {
                userData.SyncStatus = SyncStatus.Synced;
                userData.LastSyncedAt = DateTime.UtcNow;
                _database.Users.Add(userData);
            }
            else if (existingUser != null && change.Operation == "UPDATE")
            {
                // Check for conflicts
                if (existingUser.SyncStatus == SyncStatus.Modified)
                {
                    await CreateConflictAsync(change, "Local modifications exist");
                    return false;
                }

                // Update fields
                existingUser.FullName = userData.FullName;
                existingUser.Role = userData.Role;
                existingUser.IsActive = userData.IsActive;
                existingUser.SyncStatus = SyncStatus.Synced;
                existingUser.LastSyncedAt = DateTime.UtcNow;
                existingUser.ETag = change.ETag;
            }

            await _database.SaveChangesAsync();
            return true;
        }

        private async Task<bool> ApplyInventoryItemChangeAsync(SyncChange change)
        {
            var itemData = JsonSerializer.Deserialize<InventoryItem>(change.Data.ToString() ?? "{}");
            if (itemData == null) return false;

            var existingItem = await _database.InventoryItems
                .FirstOrDefaultAsync(i => i.CloudId == change.Id || i.ItemCode == itemData.ItemCode);

            if (existingItem == null && change.Operation == "CREATE")
            {
                itemData.SyncStatus = SyncStatus.Synced;
                itemData.LastSyncedAt = DateTime.UtcNow;
                _database.InventoryItems.Add(itemData);
            }
            else if (existingItem != null && change.Operation == "UPDATE")
            {
                // Check for conflicts
                if (existingItem.SyncStatus == SyncStatus.Modified)
                {
                    await CreateConflictAsync(change, "Local modifications exist");
                    return false;
                }

                // Update fields
                existingItem.Name = itemData.Name;
                existingItem.Description = itemData.Description;
                existingItem.CurrentQuantity = itemData.CurrentQuantity;
                existingItem.MinimumQuantity = itemData.MinimumQuantity;
                existingItem.MaximumQuantity = itemData.MaximumQuantity;
                existingItem.Unit = itemData.Unit;
                existingItem.Location = itemData.Location;
                existingItem.Category = itemData.Category;
                existingItem.Supplier = itemData.Supplier;
                existingItem.UnitCost = itemData.UnitCost;
                existingItem.LastModifiedAt = itemData.LastModifiedAt;
                existingItem.SyncStatus = SyncStatus.Synced;
                existingItem.LastSyncedAt = DateTime.UtcNow;
                existingItem.ETag = change.ETag;
            }

            await _database.SaveChangesAsync();
            return true;
        }

        private async Task<bool> ApplyTransactionChangeAsync(SyncChange change)
        {
            var transData = JsonSerializer.Deserialize<InventoryTransaction>(change.Data.ToString() ?? "{}");
            if (transData == null) return false;

            var existingTrans = await _database.InventoryTransactions
                .FirstOrDefaultAsync(t => t.CloudId == change.Id);

            if (existingTrans == null && change.Operation == "CREATE")
            {
                transData.SyncStatus = SyncStatus.Synced;
                transData.LastSyncedAt = DateTime.UtcNow;
                _database.InventoryTransactions.Add(transData);
                await _database.SaveChangesAsync();
                return true;
            }

            // Transactions are typically immutable, so we don't update them
            return false;
        }

        private async Task MarkItemsAsSyncedAsync(List<SyncChange> changes, SyncPushResponse response)
        {
            // Mark successfully synced items
            foreach (var change in changes.Where(c => !response.Errors.Any(e => e.EntityId == c.Id)))
            {
                switch (change.Table.ToLower())
                {
                    case "users":
                        var user = await _database.Users.FirstOrDefaultAsync(u => u.Id == change.Id);
                        if (user != null)
                        {
                            user.SyncStatus = SyncStatus.Synced;
                            user.LastSyncedAt = DateTime.UtcNow;
                        }
                        break;

                    case "inventoryitems":
                        var item = await _database.InventoryItems
                            .FirstOrDefaultAsync(i => i.Id.ToString() == change.Id);
                        if (item != null)
                        {
                            item.SyncStatus = SyncStatus.Synced;
                            item.LastSyncedAt = DateTime.UtcNow;
                        }
                        break;

                    case "inventorytransactions":
                        var trans = await _database.InventoryTransactions
                            .FirstOrDefaultAsync(t => t.Id.ToString() == change.Id);
                        if (trans != null)
                        {
                            trans.SyncStatus = SyncStatus.Synced;
                            trans.LastSyncedAt = DateTime.UtcNow;
                        }
                        break;
                }
            }

            await _database.SaveChangesAsync();
        }

        private async Task CreateConflictAsync(SyncChange change, string reason)
        {
            var conflict = new SyncConflict
            {
                EntityType = change.Table,
                EntityId = change.Id,
                LocalValue = "{}",  // Would serialize local entity here
                RemoteValue = JsonSerializer.Serialize(change.Data),
                DetectedAt = DateTime.UtcNow
            };

            _database.Set<SyncConflict>().Add(conflict);
            await _database.SaveChangesAsync();
        }

        private async Task ApplyConflictResolutionAsync(SyncConflict conflict, ConflictResolutionResult result)
        {
            // Apply the resolved data to the appropriate entity
            // Implementation depends on the resolution strategy and entity type
            _logger.LogInformation($"Applied conflict resolution for {conflict.EntityType}:{conflict.EntityId}");
        }

        private async Task<DateTime> GetLastSyncTimeAsync(string? tableName = null)
        {
            var query = _database.Set<SyncLog>()
                .Where(l => l.Status == "Completed");

            if (!string.IsNullOrEmpty(tableName))
            {
                query = query.Where(l => l.OperationType.Contains(tableName));
            }

            var lastSync = await query
                .OrderByDescending(l => l.CompletedAt)
                .FirstOrDefaultAsync();

            return lastSync?.CompletedAt ?? DateTime.MinValue;
        }

        private async Task<SyncLog> StartSyncLogAsync(string operationType)
        {
            var log = new SyncLog
            {
                OperationType = operationType,
                StartedAt = DateTime.UtcNow,
                Status = "InProgress"
            };

            _database.Set<SyncLog>().Add(log);
            await _database.SaveChangesAsync();
            return log;
        }

        private async Task CompleteSyncLogAsync(SyncLog log, int recordsProcessed, int conflicts)
        {
            log.CompletedAt = DateTime.UtcNow;
            log.RecordsProcessed = recordsProcessed;
            log.ErrorCount = conflicts;
            log.Status = "Completed";
            await _database.SaveChangesAsync();
        }

        private async Task FailSyncLogAsync(SyncLog log, string? error)
        {
            log.CompletedAt = DateTime.UtcNow;
            log.ErrorDetails = error;
            log.Status = "Failed";
            await _database.SaveChangesAsync();
        }

        #endregion

        #region Helper Classes

        private class PushResult
        {
            public bool Success { get; set; }
            public int RecordsProcessed { get; set; }
            public string? ErrorMessage { get; set; }
        }

        private class PullResult
        {
            public bool Success { get; set; }
            public int RecordsProcessed { get; set; }
            public string? ErrorMessage { get; set; }
        }

        private class ConflictProcessResult
        {
            public int ConflictCount { get; set; }
            public int AutoResolved { get; set; }
        }

        #endregion
    }
}