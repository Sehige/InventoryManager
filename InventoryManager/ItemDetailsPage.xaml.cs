using InventoryManager.Models;
using InventoryManager.Services;

namespace InventoryManager;

public partial class ItemDetailsPage : ContentPage
{
    private readonly InventoryService _inventoryService;
    private InventoryItem _currentItem;
    private DateTime? _scanTime;

    public ItemDetailsPage(InventoryService inventoryService, InventoryItem item, DateTime? scanTime = null)
    {
        InitializeComponent();
        _inventoryService = inventoryService;
        _currentItem = item;
        _scanTime = scanTime;

        DisplayItemDetails();
    }

    private void DisplayItemDetails()
    {
        if (_currentItem == null) return;

        // Basic information
        ItemNameLabel.Text = _currentItem.Name;
        ItemCodeLabel.Text = $"Code: {_currentItem.ItemCode}";
        CurrentStockLabel.Text = $"{_currentItem.CurrentQuantity} {_currentItem.Unit}";
        LocationLabel.Text = _currentItem.Location.GetDisplayName();

        // Additional details
        CategoryLabel.Text = _currentItem.Category ?? "Uncategorized";
        UnitLabel.Text = _currentItem.Unit;
        UnitCostLabel.Text = $"${_currentItem.UnitCost:F2}";
        LastModifiedLabel.Text = _currentItem.LastModifiedAt.ToString("g");

        // Stock status color
        if (_currentItem.IsLowStock)
        {
            CurrentStockLabel.TextColor = Colors.Orange;
        }
        else if (_currentItem.CurrentQuantity == 0)
        {
            CurrentStockLabel.TextColor = Colors.Red;
        }
        else
        {
            CurrentStockLabel.TextColor = Colors.Green;
        }

        // Show scan info if from QR scan
        if (_scanTime.HasValue)
        {
            ScanInfoFrame.IsVisible = true;
            ScanTimeLabel.Text = $"Scanned at: {_scanTime.Value:g}";
        }
    }

    private async void OnAdjustStockClicked(object sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is string adjustmentStr)
        {
            if (int.TryParse(adjustmentStr, out int adjustment))
            {
                try
                {
                    var newQuantity = _currentItem.CurrentQuantity + adjustment;
                    if (newQuantity < 0)
                    {
                        await DisplayAlert("Invalid Adjustment",
                            "Stock cannot be negative.",
                            "OK");
                        return;
                    }

                    var success = await _inventoryService.AdjustInventoryQuantityAsync(
                        _currentItem.Id,
                        adjustment,
                        "Adjustment",
                        $"Quick adjustment from QR scan: {adjustment:+#;-#;0}");

                    if (success)
                    {
                        _currentItem.CurrentQuantity = newQuantity;
                        DisplayItemDetails(); // Refresh display

                        await DisplayAlert("Success",
                            $"Stock adjusted by {adjustment:+#;-#;0}",
                            "OK");
                    }
                    else
                    {
                        await DisplayAlert("Error",
                            "Failed to update stock.",
                            "OK");
                    }
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Error",
                        $"An error occurred: {ex.Message}",
                        "OK");
                }
            }
        }
    }
}