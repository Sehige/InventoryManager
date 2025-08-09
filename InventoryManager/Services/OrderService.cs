// Services/OrderService.cs - Order Management Service
// This follows the same patterns as InventoryService and integrates with existing services

using Microsoft.EntityFrameworkCore;
using InventoryManager.Models;

namespace InventoryManager.Services
{
    /// <summary>
    /// Order management service that integrates with inventory system
    /// Follows the same pattern as InventoryService for consistency
    /// </summary>
    public class OrderService
    {
        private readonly DatabaseService _databaseService;
        private readonly AuthService _authService;
        private readonly InventoryService _inventoryService;

        public OrderService(DatabaseService databaseService, AuthService authService, InventoryService inventoryService)
        {
            _databaseService = databaseService;
            _authService = authService;
            _inventoryService = inventoryService;
        }

        /// <summary>
        /// Get all orders with filtering - follows InventoryService pattern
        /// </summary>
        public async Task<List<OrderDto>> GetOrdersAsync(OrderFilter? filter = null)
        {
            try
            {
                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null)
                {
                    throw new UnauthorizedAccessException("User must be logged in to access orders");
                }

                filter ??= new OrderFilter();

                var query = _databaseService.Orders
                    .Include(o => o.CreatedByUser)
                    .Include(o => o.OrderItems)
                        .ThenInclude(oi => oi.InventoryItem)
                    .AsQueryable();

                // Apply filters
                if (!string.IsNullOrWhiteSpace(filter.SearchText))
                {
                    query = query.Where(o => o.OrderName.Contains(filter.SearchText) ||
                                           o.Notes.Contains(filter.SearchText));
                }

                if (filter.Status.HasValue)
                {
                    query = query.Where(o => o.Status == filter.Status.Value);
                }

                if (filter.CreatedDateFrom.HasValue)
                {
                    query = query.Where(o => o.CreatedDate >= filter.CreatedDateFrom.Value);
                }

                if (filter.CreatedDateTo.HasValue)
                {
                    query = query.Where(o => o.CreatedDate <= filter.CreatedDateTo.Value);
                }

                if (!string.IsNullOrWhiteSpace(filter.CreatedByUserId))
                {
                    query = query.Where(o => o.CreatedByUserId == filter.CreatedByUserId);
                }

                // Apply sorting
                query = filter.SortBy switch
                {
                    OrderSortBy.OrderName => filter.SortDescending ? query.OrderByDescending(o => o.OrderName) : query.OrderBy(o => o.OrderName),
                    OrderSortBy.CreatedDate => filter.SortDescending ? query.OrderByDescending(o => o.CreatedDate) : query.OrderBy(o => o.CreatedDate),
                    OrderSortBy.FinalizedDate => filter.SortDescending ? query.OrderByDescending(o => o.FinalizedDate) : query.OrderBy(o => o.FinalizedDate),
                    OrderSortBy.Status => filter.SortDescending ? query.OrderByDescending(o => o.Status) : query.OrderBy(o => o.Status),
                    OrderSortBy.TotalItems => filter.SortDescending ? query.OrderByDescending(o => o.OrderItems.Sum(oi => oi.Quantity)) : query.OrderBy(o => o.OrderItems.Sum(oi => oi.Quantity)),
                    OrderSortBy.CreatedByUser => filter.SortDescending ? query.OrderByDescending(o => o.CreatedByUser.FullName) : query.OrderBy(o => o.CreatedByUser.FullName),
                    _ => filter.SortDescending ? query.OrderByDescending(o => o.CreatedDate) : query.OrderBy(o => o.CreatedDate)
                };

                // Apply pagination
                var orders = await query
                    .Skip((filter.PageNumber - 1) * filter.PageSize)
                    .Take(filter.PageSize)
                    .ToListAsync();

                // Convert to DTOs
                return orders.Select(ConvertToDto).ToList();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to retrieve orders: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Get a specific order by ID
        /// </summary>
        public async Task<OrderDto?> GetOrderByIdAsync(int orderId)
        {
            try
            {
                var order = await _databaseService.Orders
                    .Include(o => o.CreatedByUser)
                    .Include(o => o.OrderItems)
                        .ThenInclude(oi => oi.InventoryItem)
                    .FirstOrDefaultAsync(o => o.Id == orderId);

                return order != null ? ConvertToDto(order) : null;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to retrieve order: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Create a new order
        /// </summary>
        public async Task<OrderDto> CreateOrderAsync(string orderName, string notes = "")
        {
            try
            {
                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser == null)
                {
                    throw new UnauthorizedAccessException("User must be logged in to create orders");
                }

                var order = new Order
                {
                    OrderName = orderName.Trim(),
                    Notes = notes.Trim(),
                    CreatedByUserId = currentUser.Id.ToString(),
                    CreatedDate = DateTime.UtcNow,
                    Status = OrderStatus.Pending,
                    SyncStatus = SyncStatus.PendingSync
                };

                _databaseService.Orders.Add(order);
                await _databaseService.SaveChangesAsync();

                return await GetOrderByIdAsync(order.Id) ?? throw new InvalidOperationException("Failed to retrieve created order");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to create order: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Add item to order and update inventory
        /// This implements the core requirement to automatically update inventory
        /// </summary>
        public async Task<bool> AddItemToOrderAsync(int orderId, int inventoryItemId, int quantity)
        {
            try
            {
                var order = await _databaseService.Orders
                    .Include(o => o.OrderItems)
                    .FirstOrDefaultAsync(o => o.Id == orderId);

                if (order == null || order.IsFinalized)
                {
                    return false;
                }

                var inventoryItem = await _databaseService.InventoryItems
                    .FirstOrDefaultAsync(i => i.Id == inventoryItemId);

                if (inventoryItem == null || inventoryItem.CurrentQuantity < quantity)
                {
                    return false;
                }

                // Check if item already exists in order
                var existingOrderItem = order.OrderItems.FirstOrDefault(oi => oi.InventoryItemId == inventoryItemId);

                if (existingOrderItem != null)
                {
                    // Update existing item quantity
                    existingOrderItem.Quantity += quantity;
                    existingOrderItem.SyncStatus = SyncStatus.PendingSync;
                }
                else
                {
                    // Add new item to order
                    var orderItem = new OrderItem
                    {
                        OrderId = orderId,
                        InventoryItemId = inventoryItemId,
                        Quantity = quantity,
                        SyncStatus = SyncStatus.PendingSync
                    };

                    _databaseService.OrderItems.Add(orderItem);
                }

                // Update inventory quantity (FR6.4 requirement)
                inventoryItem.CurrentQuantity -= quantity;
                inventoryItem.LastModifiedAt = DateTime.UtcNow;
                inventoryItem.SyncStatus = SyncStatus.PendingSync;

                order.SyncStatus = SyncStatus.PendingSync;

                await _databaseService.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to add item to order: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Remove item from order and restore inventory
        /// This implements FR6.5, FR6.6, FR6.7 requirements
        /// </summary>
        public async Task<bool> RemoveItemFromOrderAsync(int orderId, int orderItemId, int? quantityToRemove = null)
        {
            try
            {
                var order = await _databaseService.Orders
                    .Include(o => o.OrderItems)
                    .FirstOrDefaultAsync(o => o.Id == orderId);

                if (order == null || order.IsFinalized)
                {
                    return false;
                }

                var orderItem = await _databaseService.OrderItems
                    .Include(oi => oi.InventoryItem)
                    .FirstOrDefaultAsync(oi => oi.Id == orderItemId && oi.OrderId == orderId);

                if (orderItem == null)
                {
                    return false;
                }

                int actualQuantityToRemove = quantityToRemove ?? orderItem.Quantity;
                if (actualQuantityToRemove > orderItem.Quantity)
                {
                    actualQuantityToRemove = orderItem.Quantity;
                }

                // Update or restore inventory (FR6.5, FR6.6, FR6.7)
                if (orderItem.InventoryItem != null)
                {
                    // FR6.7: Update existing inventory item
                    orderItem.InventoryItem.CurrentQuantity += actualQuantityToRemove;
                    orderItem.InventoryItem.LastModifiedAt = DateTime.UtcNow;
                    orderItem.InventoryItem.SyncStatus = SyncStatus.PendingSync;
                }
                else
                {
                    // FR6.6: Create new inventory item if it doesn't exist
                    var newInventoryItem = new InventoryItem
                    {
                        ItemCode = $"RESTORED_{orderItem.Id}",
                        Name = $"Restored Item from Order {order.OrderName}",
                        CurrentQuantity = actualQuantityToRemove,
                        MinimumQuantity = 0,
                        MaximumQuantity = 999999,
                        Unit = "pieces",
                        Location = WarehouseLocation.MainWarehouse,
                        Category = "Restored",
                        CreatedAt = DateTime.UtcNow,
                        LastModifiedAt = DateTime.UtcNow,
                        IsActive = true,
                        SyncStatus = SyncStatus.PendingSync
                    };

                    _databaseService.InventoryItems.Add(newInventoryItem);
                }

                // Update or remove order item
                if (actualQuantityToRemove >= orderItem.Quantity)
                {
                    // Remove entire order item
                    _databaseService.OrderItems.Remove(orderItem);
                }
                else
                {
                    // Reduce quantity
                    orderItem.Quantity -= actualQuantityToRemove;
                    orderItem.SyncStatus = SyncStatus.PendingSync;
                }

                order.SyncStatus = SyncStatus.PendingSync;

                await _databaseService.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to remove item from order: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Finalize an order - marks it as complete
        /// </summary>
        public async Task<bool> FinalizeOrderAsync(int orderId)
        {
            try
            {
                var order = await _databaseService.Orders
                    .Include(o => o.OrderItems)
                    .FirstOrDefaultAsync(o => o.Id == orderId);

                if (order == null || !order.CanFinalize())
                {
                    return false;
                }

                order.Finalize();
                await _databaseService.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to finalize order: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Update order details
        /// </summary>
        public async Task<bool> UpdateOrderAsync(int orderId, string orderName, string notes)
        {
            try
            {
                var order = await _databaseService.Orders.FirstOrDefaultAsync(o => o.Id == orderId);

                if (order == null || order.IsFinalized)
                {
                    return false;
                }

                order.OrderName = orderName.Trim();
                order.Notes = notes.Trim();
                order.SyncStatus = SyncStatus.PendingSync;

                await _databaseService.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to update order: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Delete an order (only if pending)
        /// </summary>
        public async Task<bool> DeleteOrderAsync(int orderId)
        {
            try
            {
                var order = await _databaseService.Orders
                    .Include(o => o.OrderItems)
                    .FirstOrDefaultAsync(o => o.Id == orderId);

                if (order == null || order.IsFinalized)
                {
                    return false;
                }

                // Restore inventory for all items in the order
                foreach (var orderItem in order.OrderItems)
                {
                    await RemoveItemFromOrderAsync(orderId, orderItem.Id);
                }

                _databaseService.Orders.Remove(order);
                await _databaseService.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to delete order: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Convert Order entity to DTO - follows existing pattern
        /// </summary>
        private static OrderDto ConvertToDto(Order order)
        {
            return new OrderDto
            {
                Id = order.Id,
                OrderName = order.OrderName,
                CreatedDate = order.CreatedDate,
                FinalizedDate = order.FinalizedDate,
                Status = order.Status,
                StatusDisplayName = order.StatusDisplayName,
                StatusColor = order.StatusColor,
                CreatedByUserName = order.CreatedByUser?.FullName ?? "Unknown",
                TotalItems = order.TotalItems,
                TotalValue = order.TotalValue,
                Notes = order.Notes,
                IsFinalized = order.IsFinalized,
                CanFinalize = order.CanFinalize(),
                OrderItems = order.OrderItems?.Select(oi => new OrderItemDto
                {
                    Id = oi.Id,
                    OrderId = oi.OrderId,
                    InventoryItemId = oi.InventoryItemId,
                    ItemName = oi.ItemName,
                    ItemCode = oi.ItemCode,
                    Quantity = oi.Quantity,
                    Unit = oi.Unit,
                    UnitCost = oi.UnitCost,
                    TotalValue = oi.TotalValue,
                    AvailableStock = oi.InventoryItem?.CurrentQuantity ?? 0
                }).ToList() ?? new List<OrderItemDto>()
            };
        }
    }
}