// Models/OrderModels.cs - Order Management Models implementing ISyncable
// This builds on your existing model patterns and integrates with inventory system

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace InventoryManager.Models
{
    /// <summary>
    /// Order status enumeration for tracking order lifecycle
    /// </summary>
    public enum OrderStatus
    {
        Pending = 0,
        Finalized = 1
    }

    /// <summary>
    /// Order model implementing ISyncable pattern from existing codebase
    /// </summary>
    public class Order : ISyncable
    {
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string OrderName { get; set; } = string.Empty;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime? FinalizedDate { get; set; }
        public OrderStatus Status { get; set; } = OrderStatus.Pending;

        [Required, MaxLength(50)]
        public string CreatedByUserId { get; set; } = string.Empty;

        [MaxLength(500)]
        public string Notes { get; set; } = string.Empty;

        // Navigation properties
        public User CreatedByUser { get; set; } = null!;
        public List<OrderItem> OrderItems { get; set; } = new();

        // ISyncable implementation - following existing pattern
        public SyncStatus SyncStatus { get; set; } = SyncStatus.Synced;
        public DateTime? LastSyncedAt { get; set; }
        public string? CloudId { get; set; }
        public string? ETag { get; set; }
        public string LocalId => Id.ToString();

        // Business logic properties
        public bool IsFinalized => Status == OrderStatus.Finalized;
        public int TotalItems => OrderItems?.Sum(oi => oi.Quantity) ?? 0;
        public decimal TotalValue => OrderItems?.Sum(oi => oi.TotalValue) ?? 0;
        public string StatusDisplayName => Status == OrderStatus.Finalized ? "Completed" : "Pending";
        public string StatusColor => Status == OrderStatus.Finalized ? "#008000" : "#FFA500";

        /// <summary>
        /// Finalize the order - sets status and finalized date
        /// </summary>
        public void Finalize()
        {
            if (Status != OrderStatus.Finalized)
            {
                Status = OrderStatus.Finalized;
                FinalizedDate = DateTime.UtcNow;
                SyncStatus = SyncStatus.PendingSync;
            }
        }

        /// <summary>
        /// Check if order can be finalized
        /// </summary>
        public bool CanFinalize()
        {
            return Status == OrderStatus.Pending && OrderItems.Any();
        }
    }

    /// <summary>
    /// Order item model linking orders to inventory items
    /// </summary>
    public class OrderItem : ISyncable
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public int InventoryItemId { get; set; }
        public int Quantity { get; set; }

        // Navigation properties
        public Order Order { get; set; } = null!;
        public InventoryItem InventoryItem { get; set; } = null!;

        // ISyncable implementation
        public SyncStatus SyncStatus { get; set; } = SyncStatus.Synced;
        public DateTime? LastSyncedAt { get; set; }
        public string? CloudId { get; set; }
        public string? ETag { get; set; }
        public string LocalId => Id.ToString();

        // Business logic properties
        public decimal UnitCost => InventoryItem?.UnitCost ?? 0;
        public decimal TotalValue => Quantity * UnitCost;
        public string ItemName => InventoryItem?.Name ?? "Unknown Item";
        public string ItemCode => InventoryItem?.ItemCode ?? "";
        public string Unit => InventoryItem?.Unit ?? "pieces";

        /// <summary>
        /// Validate that quantity is positive
        /// </summary>
        public bool IsValidQuantity()
        {
            return Quantity > 0;
        }
    }

    /// <summary>
    /// DTO for displaying orders in UI - similar to InventoryItemDto pattern
    /// </summary>
    public class OrderDto
    {
        public int Id { get; set; }
        public string OrderName { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public DateTime? FinalizedDate { get; set; }
        public OrderStatus Status { get; set; }
        public string StatusDisplayName { get; set; } = string.Empty;
        public string StatusColor { get; set; } = string.Empty;
        public string CreatedByUserName { get; set; } = string.Empty;
        public int TotalItems { get; set; }
        public decimal TotalValue { get; set; }
        public string Notes { get; set; } = string.Empty;
        public bool IsFinalized { get; set; }
        public bool CanFinalize { get; set; }
        public List<OrderItemDto> OrderItems { get; set; } = new();
    }

    /// <summary>
    /// DTO for displaying order items
    /// </summary>
    public class OrderItemDto
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public int InventoryItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string Unit { get; set; } = string.Empty;
        public decimal UnitCost { get; set; }
        public decimal TotalValue { get; set; }
        public int AvailableStock { get; set; }
    }

    /// <summary>
    /// Filter criteria for searching orders - following InventoryFilter pattern
    /// </summary>
    public class OrderFilter
    {
        public string SearchText { get; set; } = string.Empty;
        public OrderStatus? Status { get; set; }
        public DateTime? CreatedDateFrom { get; set; }
        public DateTime? CreatedDateTo { get; set; }
        public string CreatedByUserId { get; set; } = string.Empty;
        public OrderSortBy SortBy { get; set; } = OrderSortBy.CreatedDate;
        public bool SortDescending { get; set; } = true;
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    /// <summary>
    /// Sorting options for orders list
    /// </summary>
    public enum OrderSortBy
    {
        OrderName,
        CreatedDate,
        FinalizedDate,
        Status,
        TotalItems,
        TotalValue,
        CreatedByUser
    }
}