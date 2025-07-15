using System;
using System.Collections.Generic;
using InventoryManager.Models;

namespace InventoryManager.Models
{
    /// <summary>
    /// QR Code data structure for encoding/decoding
    /// This is just a data model, not a service
    /// </summary>
    public class InventoryQRData
    {
        public string ItemCode { get; set; }
        public string BatchNumber { get; set; }
        public DateTime ManufactureDate { get; set; }
        public WarehouseLocation Location { get; set; }

        /// <summary>
        /// Serialize to QR code format (e.g., JSON or custom format)
        /// </summary>
        public string ToQRString()
        {
            // Using a pipe-delimited format for efficiency
            // Format: INV|{ItemCode}|{BatchNumber}|{ManufactureDate}|{Location}
            return $"INV|{ItemCode}|{BatchNumber}|{ManufactureDate:yyyy-MM-dd}|{(int)Location}";
        }

        /// <summary>
        /// Parse QR code string back to object
        /// </summary>
        public static InventoryQRData FromQRString(string qrData)
        {
            if (string.IsNullOrEmpty(qrData) || !qrData.StartsWith("INV|"))
                return null;

            var parts = qrData.Split('|');
            if (parts.Length < 5)
                return null;

            return new InventoryQRData
            {
                ItemCode = parts[1],
                BatchNumber = parts[2],
                ManufactureDate = DateTime.TryParse(parts[3], out var date) ? date : DateTime.MinValue,
                Location = Enum.TryParse<WarehouseLocation>(parts[4], out var loc) ? loc : WarehouseLocation.MainWarehouse
            };
        }
    }

    /// <summary>
    /// Result of a QR code scan operation
    /// </summary>
    public class QRScanResult
    {
        public bool Success { get; set; }
        public string ItemCode { get; set; }
        public string RawData { get; set; }
        public DateTime ScannedAt { get; set; }
        public string ErrorMessage { get; set; }

        // Additional metadata that might be encoded in QR
        public Dictionary<string, string> AdditionalData { get; set; }
    }
}