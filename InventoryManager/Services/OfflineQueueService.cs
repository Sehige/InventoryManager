

// Services/OfflineQueueService.cs
// Manages operations queued while offline

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using InventoryManager.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InventoryManager.Services
{
    /// <summary>
    /// Interface for offline queue management
    /// </summary>
    public interface IOfflineQueueService
    {
        Task EnqueueAsync<T>(T entity, string operation) where T : ISyncable;
        Task<List<OfflineQueue>> GetPendingOperationsAsync();
        Task<bool> ProcessQueueAsync();
        Task RemoveFromQueueAsync(int queueId);
        Task ClearQueueAsync();
    }

    /// <summary>
    /// Manages operations that need to be synced when connectivity is restored
    /// </summary>
    public class OfflineQueueService : IOfflineQueueService
    {
        private readonly DatabaseService _database;
        private readonly ICloudApiClient _cloudApi;
        private readonly ILogger<OfflineQueueService> _logger;

        public OfflineQueueService(
            DatabaseService database,
            ICloudApiClient cloudApi,
            ILogger<OfflineQueueService> logger)
        {
            _database = database;
            _cloudApi = cloudApi;
            _logger = logger;
        }

        /// <summary>
        /// Add an operation to the offline queue
        /// </summary>
        public async Task EnqueueAsync<T>(T entity, string operation) where T : ISyncable
        {
            var queueItem = new OfflineQueue
            {
                EntityType = typeof(T).Name,
                EntityId = entity.LocalId,
                Operation = operation,
                Payload = JsonSerializer.Serialize(entity),
                QueuedAt = DateTime.UtcNow,
                RetryCount = 0
            };

            _database.Set<OfflineQueue>().Add(queueItem);
            await _database.SaveChangesAsync();

            _logger.LogInformation($"Queued {operation} operation for {typeof(T).Name}:{entity.LocalId}");
        }

        /// <summary>
        /// Get all pending operations from the queue
        /// </summary>
        public async Task<List<OfflineQueue>> GetPendingOperationsAsync()
        {
            return await _database.Set<OfflineQueue>()
                .OrderBy(q => q.QueuedAt)
                .ToListAsync();
        }

        /// <summary>
        /// Process all queued operations
        /// </summary>
        public async Task<bool> ProcessQueueAsync()
        {
            var queue = await GetPendingOperationsAsync();
            if (!queue.Any())
                return true;

            _logger.LogInformation($"Processing {queue.Count} queued operations");

            var allSuccess = true;

            foreach (var item in queue)
            {
                try
                {
                    var success = await ProcessQueueItemAsync(item);
                    if (success)
                    {
                        await RemoveFromQueueAsync(item.Id);
                    }
                    else
                    {
                        allSuccess = false;
                        item.RetryCount++;
                        item.LastError = "Processing failed";
                        await _database.SaveChangesAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error processing queue item {item.Id}");
                    allSuccess = false;

                    item.RetryCount++;
                    item.LastError = ex.Message;
                    await _database.SaveChangesAsync();
                }
            }

            return allSuccess;
        }

        /// <summary>
        /// Remove a successfully processed item from the queue
        /// </summary>
        public async Task RemoveFromQueueAsync(int queueId)
        {
            var item = await _database.Set<OfflineQueue>().FindAsync(queueId);
            if (item != null)
            {
                _database.Set<OfflineQueue>().Remove(item);
                await _database.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Clear all items from the queue
        /// </summary>
        public async Task ClearQueueAsync()
        {
            var items = await _database.Set<OfflineQueue>().ToListAsync();
            _database.Set<OfflineQueue>().RemoveRange(items);
            await _database.SaveChangesAsync();

            _logger.LogInformation($"Cleared {items.Count} items from offline queue");
        }

        private async Task<bool> ProcessQueueItemAsync(OfflineQueue item)
        {
            switch (item.EntityType.ToLower())
            {
                case "inventoryitem":
                    return await ProcessInventoryItemAsync(item);

                case "inventorytransaction":
                    return await ProcessTransactionAsync(item);

                case "user":
                    return await ProcessUserAsync(item);

                default:
                    _logger.LogWarning($"Unknown entity type in queue: {item.EntityType}");
                    return false;
            }
        }

        private async Task<bool> ProcessInventoryItemAsync(OfflineQueue item)
        {
            var inventoryItem = JsonSerializer.Deserialize<InventoryItem>(item.Payload);
            if (inventoryItem == null)
                return false;

            switch (item.Operation.ToUpper())
            {
                case "CREATE":
                    var created = await _cloudApi.CreateInventoryItemAsync(inventoryItem);
                    if (created != null)
                    {
                        // Update local item with cloud ID
                        var localItem = await _database.InventoryItems
                            .FirstOrDefaultAsync(i => i.Id.ToString() == item.EntityId);
                        if (localItem != null)
                        {
                            localItem.CloudId = created.CloudId;
                            localItem.SyncStatus = SyncStatus.Synced;
                            localItem.LastSyncedAt = DateTime.UtcNow;
                            await _database.SaveChangesAsync();
                        }
                        return true;
                    }
                    break;

                case "UPDATE":
                    if (!string.IsNullOrEmpty(inventoryItem.CloudId))
                    {
                        var updated = await _cloudApi.UpdateInventoryItemAsync(
                            int.Parse(inventoryItem.CloudId), inventoryItem);
                        return updated != null;
                    }
                    break;

                case "DELETE":
                    if (!string.IsNullOrEmpty(inventoryItem.CloudId))
                    {
                        return await _cloudApi.DeleteInventoryItemAsync(
                            int.Parse(inventoryItem.CloudId));
                    }
                    break;
            }

            return false;
        }

        private async Task<bool> ProcessTransactionAsync(OfflineQueue item)
        {
            var transaction = JsonSerializer.Deserialize<InventoryTransaction>(item.Payload);
            if (transaction == null)
                return false;

            // Transactions are typically create-only
            if (item.Operation.ToUpper() == "CREATE")
            {
                return await _cloudApi.BatchCreateTransactionsAsync(new List<InventoryTransaction> { transaction });
            }

            return false;
        }

        private async Task<bool> ProcessUserAsync(OfflineQueue item)
        {
            // User sync would be implemented similarly
            // For now, return true as users are typically managed differently
            return true;
        }
    }
}