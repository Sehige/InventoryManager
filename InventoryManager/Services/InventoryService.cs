// Services/InventoryService.cs - Simplified Inventory Service with Location Enum
// This version is much cleaner and easier to understand than the complex relationship approach
// Location access control is now handled through simple role-based filtering

using Microsoft.EntityFrameworkCore;
using InventoryManager.Models;

namespace InventoryManager.Services
{
    /// <summary>
    /// Simplified inventory service that uses enum-based location filtering
    /// This approach eliminates complex database relationships while maintaining all functionality
    /// Think of this as the business logic layer that enforces your warehouse rules simply and clearly
    /// </summary>
    public class InventoryService
    {
        private readonly DatabaseService _databaseService;
        private readonly AuthService _authService;

        public InventoryService(DatabaseService databaseService, AuthService authService)
        {
            _databaseService = databaseService;
            _authService = authService;
        }

        /// <summary>
        /// Get inventory items filtered by user role and location access
        /// This is much simpler now - we just check the user's role to determine which locations they can see
        /// No complex database joins or location assignment tables needed!
        /// </summary>
        public async Task<List<InventoryItemDto>> GetInventoryItemsAsync(InventoryFilter? filter = null)
        {
            try
            {
                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null)
                {
                    throw new UnauthorizedAccessException("User must be logged in to access inventory");
                }

                filter ??= new InventoryFilter();

                // Get the locations this user can access based on their role
                // This is much simpler than database lookups!
                var accessibleLocations = WarehouseLocationHelper.GetAccessibleLocations(currentUser.Role);

                // Start building the query - no complex joins needed
                var query = _databaseService.InventoryItems.AsQueryable();

                // Apply location-based security filtering using enum comparison
                if (filter.Locations.Any())
                {
                    // User specified specific locations - only show those they have access to
                    var requestedAccessibleLocations = filter.Locations.Intersect(accessibleLocations);
                    query = query.Where(i => requestedAccessibleLocations.Contains(i.Location));
                }
                else
                {
                    // No specific locations requested - show all accessible locations for this user role
                    query = query.Where(i => accessibleLocations.Contains(i.Location));
                }

                // Apply other filters - much simpler without location joins
                if (filter.ShowActiveOnly)
                {
                    query = query.Where(i => i.IsActive);
                }

                if (!string.IsNullOrWhiteSpace(filter.SearchText))
                {
                    var searchLower = filter.SearchText.ToLower();
                    query = query.Where(i =>
                        i.ItemCode.ToLower().Contains(searchLower) ||
                        i.Name.ToLower().Contains(searchLower) ||
                        i.Description.ToLower().Contains(searchLower));
                }

                if (!string.IsNullOrWhiteSpace(filter.Category))
                {
                    query = query.Where(i => i.Category == filter.Category);
                }

                if (filter.ShowLowStockOnly)
                {
                    query = query.Where(i => i.CurrentQuantity <= i.MinimumQuantity);
                }

                // Apply sorting - much simpler with enum locations
                query = filter.SortBy switch
                {
                    InventorySortBy.Name => filter.SortDescending ?
                        query.OrderByDescending(i => i.Name) : query.OrderBy(i => i.Name),
                    InventorySortBy.ItemCode => filter.SortDescending ?
                        query.OrderByDescending(i => i.ItemCode) : query.OrderBy(i => i.ItemCode),
                    InventorySortBy.CurrentQuantity => filter.SortDescending ?
                        query.OrderByDescending(i => i.CurrentQuantity) : query.OrderBy(i => i.CurrentQuantity),
                    InventorySortBy.Location => filter.SortDescending ?
                        query.OrderByDescending(i => i.Location) : query.OrderBy(i => i.Location),
                    InventorySortBy.Category => filter.SortDescending ?
                        query.OrderByDescending(i => i.Category) : query.OrderBy(i => i.Category),
                    InventorySortBy.LastModified => filter.SortDescending ?
                        query.OrderByDescending(i => i.LastModifiedAt) : query.OrderBy(i => i.LastModifiedAt),
                    InventorySortBy.UnitCost => filter.SortDescending ?
                        query.OrderByDescending(i => i.UnitCost) : query.OrderBy(i => i.UnitCost),
                    InventorySortBy.TotalValue => filter.SortDescending ?
                        query.OrderByDescending(i => i.CurrentQuantity * i.UnitCost) :
                        query.OrderBy(i => i.CurrentQuantity * i.UnitCost),
                    _ => query.OrderBy(i => i.Name)
                };

                // Apply pagination
                var skip = (filter.PageNumber - 1) * filter.PageSize;
                query = query.Skip(skip).Take(filter.PageSize);

                // Execute query and transform to DTOs - no complex joins!
                var items = await query
                    .Select(i => new InventoryItemDto
                    {
                        Id = i.Id,
                        ItemCode = i.ItemCode,
                        Name = i.Name,
                        Description = i.Description,
                        CurrentQuantity = i.CurrentQuantity,
                        MinimumQuantity = i.MinimumQuantity,
                        Unit = i.Unit,
                        Location = i.Location,
                        LocationDisplayName = i.Location.GetDisplayName(), // Simple enum extension method
                        Category = i.Category,
                        UnitCost = i.UnitCost,
                        IsLowStock = i.CurrentQuantity <= i.MinimumQuantity,
                        TotalValue = i.CurrentQuantity * i.UnitCost,
                        LastModifiedAt = i.LastModifiedAt,
                        LastModifiedByUserName = "" // Can populate if needed
                    })
                    .ToListAsync();

                return items;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting inventory items: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Get locations accessible to the current user - now super simple!
        /// Just returns the enum values based on user role
        /// </summary>
        public async Task<List<WarehouseLocation>> GetAccessibleLocationsAsync()
        {
            try
            {
                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null)
                {
                    return new List<WarehouseLocation>();
                }

                // Simply return locations based on role - no database queries needed!
                return WarehouseLocationHelper.GetAccessibleLocations(currentUser.Role);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting accessible locations: {ex.Message}");
                return new List<WarehouseLocation>();
            }
        }

        /// <summary>
        /// Update an inventory item (admin only)
        /// Much simpler now without location relationship complexity
        /// </summary>
        public async Task<bool> UpdateInventoryItemAsync(InventoryItem item)
        {
            try
            {
                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null)
                {
                    throw new UnauthorizedAccessException("User must be logged in to update inventory");
                }

                // For now, only admins can edit inventory items
                if (currentUser.Role != "Admin")
                {
                    throw new UnauthorizedAccessException("Only administrators can modify inventory items");
                }

                var existingItem = await _databaseService.InventoryItems.FindAsync(item.Id);
                if (existingItem == null)
                {
                    return false;
                }

                // Update all properties - location is just an enum value, super simple!
                existingItem.ItemCode = item.ItemCode;
                existingItem.Name = item.Name;
                existingItem.Description = item.Description;
                existingItem.CurrentQuantity = item.CurrentQuantity;
                existingItem.MinimumQuantity = item.MinimumQuantity;
                existingItem.MaximumQuantity = item.MaximumQuantity;
                existingItem.Unit = item.Unit;
                existingItem.Location = item.Location; // Simple enum assignment!
                existingItem.Category = item.Category;
                existingItem.Supplier = item.Supplier;
                existingItem.UnitCost = item.UnitCost;
                existingItem.LastModifiedAt = DateTime.UtcNow;
                existingItem.LastModifiedByUserId = currentUser.Id;

                await _databaseService.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating inventory item: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Create a new inventory item (admin only)
        /// Simple creation without location relationship complexity
        /// </summary>
        public async Task<InventoryItem?> CreateInventoryItemAsync(InventoryItem item)
        {
            try
            {
                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null)
                {
                    throw new UnauthorizedAccessException("User must be logged in to create inventory items");
                }

                if (currentUser.Role != "Admin")
                {
                    throw new UnauthorizedAccessException("Only administrators can create inventory items");
                }

                // Check for unique item code
                var existingItem = await _databaseService.InventoryItems
                    .FirstOrDefaultAsync(i => i.ItemCode == item.ItemCode && i.IsActive);

                if (existingItem != null)
                {
                    throw new InvalidOperationException($"An item with code '{item.ItemCode}' already exists");
                }

                // Set audit fields
                item.CreatedAt = DateTime.UtcNow;
                item.LastModifiedAt = DateTime.UtcNow;
                item.CreatedByUserId = currentUser.Id;
                item.LastModifiedByUserId = currentUser.Id;
                item.IsActive = true;

                _databaseService.InventoryItems.Add(item);
                await _databaseService.SaveChangesAsync();

                return item;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating inventory item: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Get inventory statistics with location breakdown
        /// Much simpler calculation using enum grouping
        /// </summary>
        public async Task<Dictionary<string, object>> GetInventoryStatsAsync()
        {
            try
            {
                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null)
                {
                    return new Dictionary<string, object>();
                }

                var stats = new Dictionary<string, object>();

                // Get accessible locations for this user
                var accessibleLocations = WarehouseLocationHelper.GetAccessibleLocations(currentUser.Role);

                // Filter items by accessible locations - simple enum comparison
                var accessibleItems = _databaseService.InventoryItems
                    .Where(i => i.IsActive && accessibleLocations.Contains(i.Location));

                // Calculate statistics - much faster without joins
                stats["TotalItems"] = await accessibleItems.CountAsync();
                stats["AccessibleLocations"] = accessibleLocations.Count;
                stats["LowStockItems"] = await accessibleItems.CountAsync(i => i.CurrentQuantity <= i.MinimumQuantity);
                stats["TotalValue"] = await accessibleItems.SumAsync(i => i.CurrentQuantity * i.UnitCost);

                if (await accessibleItems.AnyAsync())
                {
                    stats["AverageStockLevel"] = await accessibleItems.AverageAsync(i => (double)i.CurrentQuantity);
                }
                else
                {
                    stats["AverageStockLevel"] = 0.0;
                }

                // Category breakdown for accessible items
                var categoryStats = await accessibleItems
                    .GroupBy(i => i.Category)
                    .Select(g => new { Category = g.Key, Count = g.Count(), TotalValue = g.Sum(i => i.CurrentQuantity * i.UnitCost) })
                    .ToListAsync();

                stats["CategoryBreakdown"] = categoryStats;

                // Location breakdown using enum display names - super clean!
                var locationStats = await accessibleItems
                    .GroupBy(i => i.Location)
                    .Select(g => new
                    {
                        Location = g.Key.GetDisplayName(),
                        Count = g.Count(),
                        TotalValue = g.Sum(i => i.CurrentQuantity * i.UnitCost)
                    })
                    .ToListAsync();

                stats["LocationBreakdown"] = locationStats;

                return stats;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting inventory stats: {ex.Message}");
                return new Dictionary<string, object>();
            }
        }

        /// <summary>
        /// Get all categories available in accessible inventory
        /// Simple and fast without complex joins
        /// </summary>
        public async Task<List<string>> GetAvailableCategoriesAsync()
        {
            try
            {
                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null)
                {
                    return new List<string>();
                }

                var accessibleLocations = WarehouseLocationHelper.GetAccessibleLocations(currentUser.Role);

                return await _databaseService.InventoryItems
                    .Where(i => i.IsActive && accessibleLocations.Contains(i.Location))
                    .Where(i => !string.IsNullOrWhiteSpace(i.Category))
                    .Select(i => i.Category)
                    .Distinct()
                    .OrderBy(c => c)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting categories: {ex.Message}");
                return new List<string>();
            }
        }

        /// <summary>
        /// Adjust inventory quantity and create transaction record
        /// This handles stock movements (usage, restocking, adjustments)
        /// </summary>
        public async Task<bool> AdjustInventoryQuantityAsync(int itemId, int quantityChange, string transactionType, string notes = "")
        {
            try
            {
                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null)
                {
                    throw new UnauthorizedAccessException("User must be logged in to adjust inventory");
                }

                var item = await _databaseService.InventoryItems.FindAsync(itemId);
                if (item == null)
                {
                    return false;
                }

                // Check if user has access to this item's location
                var accessibleLocations = WarehouseLocationHelper.GetAccessibleLocations(currentUser.Role);
                if (!accessibleLocations.Contains(item.Location))
                {
                    throw new UnauthorizedAccessException("You don't have access to items in this location");
                }

                // Validate the quantity change won't result in negative inventory
                var newQuantity = item.CurrentQuantity + quantityChange;
                if (newQuantity < 0)
                {
                    throw new InvalidOperationException($"Cannot reduce quantity by {Math.Abs(quantityChange)}. Only {item.CurrentQuantity} available.");
                }

                // Update the item quantity
                item.CurrentQuantity = newQuantity;
                item.LastModifiedAt = DateTime.UtcNow;
                item.LastModifiedByUserId = currentUser.Id;

                // Create transaction record for audit trail
                var transaction = new InventoryTransaction
                {
                    InventoryItemId = itemId,
                    UserId = currentUser.Id,
                    QuantityChange = quantityChange,
                    TransactionType = transactionType,
                    Timestamp = DateTime.UtcNow,
                    Notes = notes
                };

                _databaseService.InventoryTransactions.Add(transaction);
                await _databaseService.SaveChangesAsync();

                System.Diagnostics.Debug.WriteLine($"Inventory adjusted: {item.Name} by {quantityChange} units");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error adjusting inventory: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Get transaction history for an item (if user has access to that location)
        /// </summary>
        public async Task<List<InventoryTransaction>> GetItemTransactionHistoryAsync(int itemId)
        {
            try
            {
                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null)
                {
                    return new List<InventoryTransaction>();
                }

                var item = await _databaseService.InventoryItems.FindAsync(itemId);
                if (item == null)
                {
                    return new List<InventoryTransaction>();
                }

                // Check location access
                var accessibleLocations = WarehouseLocationHelper.GetAccessibleLocations(currentUser.Role);
                if (!accessibleLocations.Contains(item.Location))
                {
                    return new List<InventoryTransaction>();
                }

                return await _databaseService.InventoryTransactions
                    .Where(t => t.InventoryItemId == itemId)
                    .Include(t => t.User)
                    .OrderByDescending(t => t.Timestamp)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting transaction history: {ex.Message}");
                return new List<InventoryTransaction>();
            }
        }

        /// <summary>
        /// Get an inventory item by its item code (for QR scanning)
        /// </summary>
        public async Task<InventoryItem?> GetItemByCodeAsync(string itemCode)
        {
            try
            {
                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null)
                {
                    throw new UnauthorizedAccessException("User must be logged in to access inventory");
                }

                var item = await _databaseService.InventoryItems
                    .FirstOrDefaultAsync(i => i.ItemCode == itemCode && i.IsActive);

                if (item == null)
                    return null;

                // Check if user has access to this item's location
                var accessibleLocations = WarehouseLocationHelper.GetAccessibleLocations(currentUser.Role);
                if (!accessibleLocations.Contains(item.Location))
                {
                    throw new UnauthorizedAccessException("You don't have access to items in this location");
                }

                return item;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting item by code: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Record a QR scan transaction
        /// </summary>
        public async Task<bool> RecordQRScanAsync(string itemCode, string scanSessionId, string notes = "QR Scan")
        {
            try
            {
                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null)
                {
                    throw new UnauthorizedAccessException("User must be logged in");
                }

                var item = await GetItemByCodeAsync(itemCode);
                if (item == null)
                    return false;

                // Create a transaction record for the scan
                var transaction = new InventoryTransaction
                {
                    InventoryItemId = item.Id,
                    TransactionType = "Scan",
                    QuantityChange = 0, // Just recording the scan, no quantity change
                    Timestamp = DateTime.UtcNow,
                    UserId = currentUser.Id,
                    Notes = notes,
                    ScanSessionId = scanSessionId
                };

                _databaseService.Add(transaction);
                await _databaseService.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error recording QR scan: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Quick stock adjustment from QR scan page
        /// </summary>
        public async Task<bool> QuickStockAdjustmentAsync(string itemCode, int adjustment, string reason = "Quick adjustment from QR scan")
        {
            try
            {
                var item = await GetItemByCodeAsync(itemCode);
                if (item == null)
                    return false;

                return await AdjustInventoryQuantityAsync(item.Id, adjustment, "Adjustment", reason);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in quick stock adjustment: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get recent scans for an item
        /// </summary>
        public async Task<List<InventoryTransaction>> GetRecentScansAsync(string itemCode, int count = 10)
        {
            try
            {
                var item = await GetItemByCodeAsync(itemCode);
                if (item == null)
                    return new List<InventoryTransaction>();

                return await _databaseService.InventoryTransactions
                    .Where(t => t.InventoryItemId == item.Id && t.TransactionType == "Scan")
                    .OrderByDescending(t => t.Timestamp)
                    .Take(count)
                    .Include(t => t.User)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting recent scans: {ex.Message}");
                return new List<InventoryTransaction>();
            }
        }
    }
}
