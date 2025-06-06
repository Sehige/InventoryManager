// Converters/ValueConverters.cs - UI Value Converters for Data Binding
// This file contains reusable converters that transform data for display in the UI
// These help make your XAML data binding more flexible and user-friendly

using InventoryManager.Models;
using System.Globalization;

namespace InventoryManager.Converters
{
    /// <summary>
    /// Converts boolean values to colors for UI elements
    /// Commonly used for status indicators like low stock warnings
    /// </summary>
    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                // Default behavior: true = red/orange (warning), false = green (good)
                // This works well for low stock indicators where true means "problem"
                return boolValue ? Colors.Orange : Colors.Green;
            }
            
            // If we can't convert, return a neutral color
            return Colors.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // This converter is typically one-way, so we don't implement ConvertBack
            throw new NotImplementedException("BoolToColorConverter is a one-way converter");
        }
    }

    /// <summary>
    /// Converts boolean values to different colors with customizable options
    /// This version allows you to specify custom colors through the parameter
    /// </summary>
    public class BoolToCustomColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue && parameter is string colorPair)
            {
                // Parameter format: "TrueColor,FalseColor" (e.g., "Red,Green")
                var colors = colorPair.Split(',');
                if (colors.Length == 2)
                {
                    var trueColor = GetColorFromName(colors[0].Trim());
                    var falseColor = GetColorFromName(colors[1].Trim());
                    
                    return boolValue ? trueColor : falseColor;
                }
            }
            
            // Fallback to default behavior
            return value is bool b ? (b ? Colors.Orange : Colors.Green) : Colors.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("BoolToCustomColorConverter is a one-way converter");
        }

        private Color GetColorFromName(string colorName)
        {
            return colorName.ToLower() switch
            {
                "red" => Colors.Red,
                "green" => Colors.Green,
                "blue" => Colors.Blue,
                "orange" => Colors.Orange,
                "yellow" => Colors.Yellow,
                "purple" => Colors.Purple,
                "gray" => Colors.Gray,
                "black" => Colors.Black,
                "white" => Colors.White,
                _ => Colors.Gray
            };
        }
    }

    /// <summary>
    /// Converts enum values to their display names
    /// Useful for showing user-friendly location names in the UI
    /// </summary>
    public class EnumToDisplayNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is WarehouseLocation location)
            {
                return location.GetDisplayName();
            }
            
            // For other enums, just use ToString()
            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("EnumToDisplayNameConverter is a one-way converter");
        }
    }

    /// <summary>
    /// Converts decimal values to currency strings
    /// Useful for displaying prices and costs in a consistent format
    /// </summary>
    public class DecimalToCurrencyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal decimalValue)
            {
                return $"${decimalValue:F2}";
            }
            
            if (value is double doubleValue)
            {
                return $"${doubleValue:F2}";
            }
            
            return "$0.00";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string stringValue && stringValue.StartsWith("$"))
            {
                var numericString = stringValue.Substring(1);
                if (decimal.TryParse(numericString, out var result))
                {
                    return result;
                }
            }
            
            return 0m;
        }
    }

    /// <summary>
    /// Converts boolean values to visibility states
    /// Useful for showing/hiding UI elements based on conditions
    /// </summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                // Check if we should invert the logic (parameter = "invert")
                bool shouldInvert = parameter?.ToString()?.ToLower() == "invert";
                
                return shouldInvert ? !boolValue : boolValue;
            }
            
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool boolValue && boolValue;
        }
    }

    /// <summary>
    /// Converts integer values to formatted strings with units
    /// Useful for displaying quantities with their measurement units
    /// </summary>
    public class QuantityWithUnitConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int quantity && parameter is string unit)
            {
                return $"{quantity} {unit}";
            }
            
            return value?.ToString() ?? "0";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("QuantityWithUnitConverter is a one-way converter");
        }
    }
}