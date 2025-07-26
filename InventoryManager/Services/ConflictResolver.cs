// Services/ConflictResolver.cs
// Handles conflict resolution during synchronization

using InventoryManager.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace InventoryManager.Services
{
    /// <summary>
    /// Interface for conflict resolution
    /// </summary>
    public interface IConflictResolver
    {
        Task<ConflictResolutionResult> ResolveAsync(SyncConflict conflict, ConflictResolutionStrategy strategy);
        Task<ConflictResolutionStrategy> GetDefaultStrategyAsync(Type entityType);
        Task<bool> CanAutoResolveAsync(SyncConflict conflict);
    }

    /// <summary>
    /// Implementation of conflict resolution logic
    /// </summary>
    public class ConflictResolver : IConflictResolver
    {
        private readonly ILogger<ConflictResolver> _logger;
        private readonly SyncConfiguration _config;

        public ConflictResolver(ILogger<ConflictResolver> logger, SyncConfiguration config)
        {
            _logger = logger;
            _config = config;
        }

        /// <summary>
        /// Resolve a sync conflict using the specified strategy
        /// </summary>
        public async Task<ConflictResolutionResult> ResolveAsync(SyncConflict conflict, ConflictResolutionStrategy strategy)
        {
            try
            {
                _logger.LogInformation($"Resolving conflict {conflict.Id} with strategy {strategy}");

                switch (strategy)
                {
                    case ConflictResolutionStrategy.ServerWins:
                        return new ConflictResolutionResult
                        {
                            Success = true,
                            ResolvedValue = conflict.RemoteValue
                        };

                    case ConflictResolutionStrategy.ClientWins:
                        return new ConflictResolutionResult
                        {
                            Success = true,
                            ResolvedValue = conflict.LocalValue
                        };

                    case ConflictResolutionStrategy.LastWriteWins:
                        // In a real implementation, you would compare timestamps
                        // For now, we'll default to server wins
                        return new ConflictResolutionResult
                        {
                            Success = true,
                            ResolvedValue = conflict.RemoteValue
                        };

                    case ConflictResolutionStrategy.Manual:
                        return new ConflictResolutionResult
                        {
                            Success = false,
                            ErrorMessage = "Manual resolution required"
                        };

                    default:
                        return new ConflictResolutionResult
                        {
                            Success = false,
                            ErrorMessage = $"Unknown resolution strategy: {strategy}"
                        };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resolving conflict");
                return new ConflictResolutionResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// Get the default resolution strategy for an entity type
        /// </summary>
        public Task<ConflictResolutionStrategy> GetDefaultStrategyAsync(Type entityType)
        {
            // You can customize this based on entity type
            // For example, inventory quantities might always use ServerWins
            // while user profiles might use LastWriteWins

            if (entityType == typeof(InventoryItem))
            {
                // Inventory data is critical, so server should win
                return Task.FromResult(ConflictResolutionStrategy.ServerWins);
            }
            else if (entityType == typeof(User))
            {
                // User profile updates can use last write wins
                return Task.FromResult(ConflictResolutionStrategy.LastWriteWins);
            }
            else
            {
                // Default to configuration
                return Task.FromResult(_config.DefaultConflictResolution);
            }
        }

        /// <summary>
        /// Determine if a conflict can be automatically resolved
        /// </summary>
        public async Task<bool> CanAutoResolveAsync(SyncConflict conflict)
        {
            // Don't auto-resolve if manual resolution is configured
            if (_config.DefaultConflictResolution == ConflictResolutionStrategy.Manual)
                return false;

            // Auto-resolve based on entity type
            switch (conflict.EntityType.ToLower())
            {
                case "inventorytransactions":
                    // Transactions should never be auto-resolved
                    return false;

                case "inventoryitems":
                    // Only auto-resolve if it's a minor update (not quantity changes)
                    // In a real implementation, you'd parse and compare the values
                    return !conflict.LocalValue.Contains("CurrentQuantity");

                case "users":
                    // User updates can generally be auto-resolved
                    return true;

                default:
                    return false;
            }
        }
    }
}
