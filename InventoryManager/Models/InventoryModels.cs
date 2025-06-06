// Models/InventoryModels.cs - Simplified Inventory Models with Location Enum
// This approach treats locations like categories - simple, fast, and easy to understand
// Think of this like having a dropdown list instead of a complex database relationship

using System.ComponentModel.DataAnnotations;

namespace InventoryManager.Models
{
    /// <summary>
    /// Enumeration of all possible warehouse locations
    /// This is much simpler than a database table and perfect for fixed location lists
    /// Add or remove locations here as your warehouse layout changes
    /// </summary>
    public enum WarehouseLocation
    {
        MainWarehouse,
        ColdStorage,
        LoadingDock,
        Workshop,
        OfficeStorage,
        OutdoorYard,
        SafetyStation,
        ChemicalStorage
    }

    /// <summary>
    /// Helper class to work with warehouse locations
    /// This provides human-readable names and descriptions for your enum values
    /// Think of this as a translation layer between code and user-friendly text
    /// </summary>
    public static class WarehouseLocationHelper
    {
        /// <summary>
        /// Convert enum values to user-friendly display names
        /// This makes your UI much more professional and readable
        /// </summary>
        public static string GetDisplayName(this WarehouseLocation location)
        {
            return location switch
            {
                WarehouseLocation.MainWarehouse => "Main Warehouse",
                WarehouseLocation.ColdStorage => "Cold Storage",
                WarehouseLocation.LoadingDock => "Loading Dock",
                WarehouseLocation.Workshop => "Workshop",
                WarehouseLocation.OfficeStorage => "Office Storage",
                WarehouseLocation.OutdoorYard => "Outdoor Yard",
                WarehouseLocation.SafetyStation => "Safety Station",
                WarehouseLocation.ChemicalStorage => "Chemical Storage",
                _ => location.ToString()
            };
        }

        /// <summary>
        /// Get a description of what's typically stored in each location
        /// This helps users understand where items should be placed
        /// </summary>
        public static string GetDescription(this WarehouseLocation location)
        {
            return location switch
            {
                WarehouseLocation.MainWarehouse => "General storage for most inventory items",
                WarehouseLocation.ColdStorage => "Temperature-controlled storage for sensitive materials",
                WarehouseLocation.LoadingDock => "Temporary storage for incoming and outgoing shipments",
                WarehouseLocation.Workshop => "Tools and materials for maintenance and repairs",
                WarehouseLocation.OfficeStorage => "Office supplies and administrative materials",
                WarehouseLocation.OutdoorYard => "Large items and materials stored outside",
                WarehouseLocation.SafetyStation => "Safety equipment and emergency supplies",
                WarehouseLocation.ChemicalStorage => "Hazardous materials and chemicals",
                _ => "General storage location"
            };
        }

        /// <summary>
        /// Get all available locations as a list for dropdowns and pickers
        /// This makes it easy to populate your UI controls
        /// </summary>
        public static List<WarehouseLocation> GetAllLocations()
        {
            return Enum.GetValues<WarehouseLocation>().ToList();
        }

        /// <summary>
        /// Get locations that a specific user role can access
        /// This implements your access control without complex database relationships
        /// Admins see everything, operators see a subset
        /// </summary>
        public static List<WarehouseLocation> GetAccessibleLocations(string userRole)
        {
            return userRole switch
            {
                "Admin" => GetAllLocations(), // Admins can access all locations
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
    /// Simplified inventory item that uses the location enum
    /// This is much cleaner and easier to work with than foreign key relationships
    /// </summary>
    public class InventoryItem
    {
        // Primary key for the inventory item
        public int Id { get; set; }

        // Unique identifier for barcodes or QR codes
        [Required]
        [MaxLength(50)]
        public string ItemCode { get; set; } = string.Empty;

        // Human-readable name for the item
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        // Optional detailed description
        [MaxLength(1000)]
        public string Description { get; set; } = string.Empty;

        // Current quantity in stock
        public int CurrentQuantity { get; set; } = 0;

        // Minimum quantity before reordering alert
        public int MinimumQuantity { get; set; } = 0;

        // Maximum quantity for storage optimization
        public int MaximumQuantity { get; set; } = 1000;

        // Unit of measurement
        [MaxLength(20)]
        public string Unit { get; set; } = "pieces";

        // Location using the simple enum approach
        // This is stored as an integer in the database but used as an enum in code
        public WarehouseLocation Location { get; set; } = WarehouseLocation.MainWarehouse;

        // Category for grouping similar items
        [MaxLength(100)]
        public string Category { get; set; } = string.Empty;

        // Optional supplier information
        [MaxLength(200)]
        public string Supplier { get; set; } = string.Empty;

        // Cost per unit for financial tracking
        public decimal UnitCost { get; set; } = 0;

        // Audit fields - track creation and modification
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastModifiedAt { get; set; } = DateTime.UtcNow;
        public string CreatedByUserId { get; set; } = string.Empty;
        public string LastModifiedByUserId { get; set; } = string.Empty;

        // Soft delete flag
        public bool IsActive { get; set; } = true;

        // Navigation property for transaction history
        public List<InventoryTransaction> Transactions { get; set; } = new();

        // Calculated properties for business logic
        public bool IsLowStock => CurrentQuantity <= MinimumQuantity;
        public decimal TotalValue => CurrentQuantity * UnitCost;
        public string LocationDisplayName => Location.GetDisplayName();
    }

    /// <summary>
    /// Updated inventory transaction that works with the simplified location system
    /// This maintains full audit trail while keeping things simple
    /// </summary>
    public partial class InventoryTransaction
    {
        // Reference to the inventory item that was changed
        public int InventoryItemId { get; set; }

        // Navigation property to the inventory item
        public InventoryItem InventoryItem { get; set; } = null!;

        // Optional scan session ID for barcode tracking
        public string? ScanSessionId { get; set; }

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
    /// Data transfer object for displaying inventory items in lists
    /// This flattens the data for efficient display without complex joins
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
    /// Now much simpler with enum-based location filtering
    /// </summary>
    public class InventoryFilter
    {
        // Filter by specific locations using the enum
        public List<WarehouseLocation> Locations { get; set; } = new();

        // Search text for item code, name, or description
        public string SearchText { get; set; } = string.Empty;

        // Filter by category
        public string Category { get; set; } = string.Empty;

        // Show only low stock items
        public bool ShowLowStockOnly { get; set; } = false;

        // Show only active items
        public bool ShowActiveOnly { get; set; } = true;

        // Sorting options
        public InventorySortBy SortBy { get; set; } = InventorySortBy.Name;
        public bool SortDescending { get; set; } = false;

        // Pagination
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