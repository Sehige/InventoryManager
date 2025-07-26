// Models/InventoryModels.cs - COMPLETE REPLACEMENT
// This fixes the enum conflicts and ensures all models work correctly with sync

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace InventoryManager.Models
{
    /// <summary>
    /// Warehouse locations enum - using your existing values
    /// </summary>
    public enum WarehouseLocation
    {
        MainWarehouse = 0,
        LoadingDock = 1,
        Workshop = 2,
        OfficeStorage = 3,
        ColdStorage = 4,
        SecureVault = 5,
        Overflow = 6,
        Returns = 7,
        Quarantine = 8
    }

    /// <summary>
    /// Extension methods for the WarehouseLocation enum
    /// </summary>
    public static class WarehouseLocationHelper
    {
        public static string GetDisplayName(this WarehouseLocation location)
        {
            return location switch
            {
                WarehouseLocation.MainWarehouse => "Main Warehouse",
                WarehouseLocation.LoadingDock => "Loading Dock",
                WarehouseLocation.Workshop => "Workshop",
                WarehouseLocation.OfficeStorage => "Office Storage",
                WarehouseLocation.ColdStorage => "Cold Storage",
                WarehouseLocation.SecureVault => "Secure Vault",
                WarehouseLocation.Overflow => "Overflow Storage",
                WarehouseLocation.Returns => "Returns Area",
                WarehouseLocation.Quarantine => "Quarantine",
                _ => location.ToString()
            };
        }

        public static List<WarehouseLocation> GetAllLocations()
        {
            return Enum.GetValues<WarehouseLocation>().ToList();
        }

        public static List<WarehouseLocation> GetAccessibleLocations(string userRole)
        {
            return userRole switch
            {
                "Admin" => GetAllLocations(),
                "Manager" => new List<WarehouseLocation>
                {
                    WarehouseLocation.MainWarehouse,
                    WarehouseLocation.LoadingDock,
                    WarehouseLocation.Workshop,
                    WarehouseLocation.OfficeStorage
                },
                "Operator" => new List<WarehouseLocation>
                {
                    WarehouseLocation.MainWarehouse,
                    WarehouseLocation.LoadingDock
                },
                _ => new List<WarehouseLocation> { WarehouseLocation.MainWarehouse }
            };
        }
    }

    /// <summary>
    /// Inventory item model with ISyncable implementation
    /// </summary>
    public class InventoryItem : ISyncable
    {
        public int Id { get; set; }

        [Required, MaxLength(50)]
        public string ItemCode { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string Description { get; set; } = string.Empty;

        public int CurrentQuantity { get; set; } = 0;
        public int MinimumQuantity { get; set; } = 0;
        public int MaximumQuantity { get; set; } = 999999;

        [MaxLength(20)]
        public string Unit { get; set; } = "pieces";

        public WarehouseLocation Location { get; set; } = WarehouseLocation.MainWarehouse;

        [MaxLength(100)]
        public string Category { get; set; } = string.Empty;

        [MaxLength(200)]
        public string Supplier { get; set; } = string.Empty;

        public decimal UnitCost { get; set; } = 0;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastModifiedAt { get; set; } = DateTime.UtcNow;
        public string CreatedByUserId { get; set; } = string.Empty;
        public string LastModifiedByUserId { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;

        // Navigation property
        public List<InventoryTransaction> Transactions { get; set; } = new();

        // Calculated properties
        public bool IsLowStock => CurrentQuantity <= MinimumQuantity;
        public decimal TotalValue => CurrentQuantity * UnitCost;
        public string LocationDisplayName => Location.GetDisplayName();

        // Sync properties implementing ISyncable
        public SyncStatus SyncStatus { get; set; } = SyncStatus.Synced;
        public DateTime? LastSyncedAt { get; set; }
        public string? CloudId { get; set; }
        public string? ETag { get; set; }
        public string LocalId => Id.ToString();
    }

    /// <summary>
    /// Inventory transaction model with ISyncable implementation
    /// </summary>
    public class InventoryTransaction : ISyncable
    {
        public int Id { get; set; }
        public int InventoryItemId { get; set; }
        public InventoryItem InventoryItem { get; set; } = null!;
        public string? ScanSessionId { get; set; }
        public string MaterialId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public int QuantityChange { get; set; }
        public string TransactionType { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string Notes { get; set; } = string.Empty;
        public User User { get; set; } = null!;

        // Sync properties implementing ISyncable
        public SyncStatus SyncStatus { get; set; } = SyncStatus.Synced;
        public DateTime? LastSyncedAt { get; set; }
        public string? CloudId { get; set; }
        public string? ETag { get; set; }
        public string LocalId => Id.ToString();

        // Business rule validation
        public bool IsValidQuantityChange(int currentQuantity)
        {
            if (QuantityChange < 0)
            {
                return currentQuantity + QuantityChange >= 0;
            }
            return true;
        }

        public decimal CalculateFinancialImpact(decimal unitCost)
        {
            return Math.Abs(QuantityChange) * unitCost;
        }
    }

    /// <summary>
    /// Data transfer object for displaying inventory items
    /// </summary>
    public class InventoryItemDto
    {
        public int Id { get; set; }
        public string ItemCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int CurrentQuantity { get; set; }
        public int MinimumQuantity { get; set; }
        public string Unit { get; set; } = string.Empty;
        public WarehouseLocation Location { get; set; }
        public string LocationDisplayName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public decimal UnitCost { get; set; }
        public bool IsLowStock { get; set; }
        public decimal TotalValue { get; set; }
        public DateTime LastModifiedAt { get; set; }
        public string LastModifiedByUserName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Filter criteria for searching inventory
    /// </summary>
    public class InventoryFilter
    {
        public List<WarehouseLocation> Locations { get; set; } = new();
        public string SearchText { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public bool ShowLowStockOnly { get; set; } = false;
        public bool ShowActiveOnly { get; set; } = true;
        public InventorySortBy SortBy { get; set; } = InventorySortBy.Name;
        public bool SortDescending { get; set; } = false;
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    /// <summary>
    /// Sorting options for inventory lists
    /// </summary>
    public enum InventorySortBy
    {
        Name,
        ItemCode,
        CurrentQuantity,
        Location,
        Category,
        LastModified,
        UnitCost,
        TotalValue
    }
}