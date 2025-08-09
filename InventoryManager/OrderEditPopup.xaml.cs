// OrderEditPopup.xaml.cs - Order Creation and Editing Popup Code-behind
// This handles the creation and editing of orders, including item management

using InventoryManager.Models;
using InventoryManager.Services;
using Microsoft.Maui.Graphics;
using System.Collections.ObjectModel;

namespace InventoryManager;

/// <summary>
/// Order edit popup for creating and modifying orders
/// Supports adding/removing items and integrates with inventory management
/// </summary>
[QueryProperty(nameof(OrderId), "OrderId")]
public partial class OrderEditPopup : ContentPage
{
    // Services
    private readonly OrderService _orderService;
    private readonly InventoryService _inventoryService;
    private readonly AuthService _authService;
    private readonly DatabaseService _databaseService;

    // Current order data
    private OrderDto? _currentOrder;
    private ObservableCollection<OrderItemDto> _orderItems;

    // Query parameter
    public static int OrderId { 
        get; 
        set; }

    /// <summary>
    /// Constructor
    /// </summary>
    public OrderEditPopup()
    {
        InitializeComponent();

        // Initialize services
        _databaseService = new DatabaseService();
        _authService = new AuthService(_databaseService);
        _inventoryService = new InventoryService(_databaseService, _authService);
        _orderService = new OrderService(_databaseService, _authService, _inventoryService);

        // Initialize collections
        _orderItems = new ObservableCollection<OrderItemDto>();
        OrderItemsCollectionView.ItemsSource = _orderItems;
    }

    /// <summary>
    /// Handle page appearing - load order data
    /// </summary>
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadOrderDataAsync();
    }

    /// <summary>
    /// Load order data based on OrderId parameter
    /// </summary>
    private async Task LoadOrderDataAsync()
    {
        try
        {
            ItemsLoadingIndicator.IsVisible = true;
            ItemsLoadingIndicator.IsRunning = true;

            if (OrderId == 0)
            {
                // Creating new order
                SetupForNewOrder();
            }
            else
            {
                // Editing existing order
                await LoadExistingOrderAsync();
            }

            UpdateSummary();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to load order data: {ex.Message}", "OK");
        }
        finally
        {
            ItemsLoadingIndicator.IsVisible = false;
            ItemsLoadingIndicator.IsRunning = false;
        }
    }

    /// <summary>
    /// Setup UI for creating a new order
    /// </summary>
    private void SetupForNewOrder()
    {
        PageTitleLabel.Text = "Create New Order";
        StatusSection.IsVisible = false;
        FinalizeBtn.IsVisible = false;
        DeleteBtn.IsVisible = false;

        // Clear form
        OrderNameEntry.Text = string.Empty;
        NotesEditor.Text = string.Empty;
        _orderItems.Clear();
    }

    /// <summary>
    /// Load existing order for editing
    /// </summary>
    private async Task LoadExistingOrderAsync()
    {
        _currentOrder = await _orderService.GetOrderByIdAsync(OrderId);

        if (_currentOrder == null)
        {
            await DisplayAlert("Error", "Order not found.", "OK");
            await Shell.Current.GoToAsync("..");
            return;
        }

        // Update UI
        PageTitleLabel.Text = "Edit Order";
        OrderNameEntry.Text = _currentOrder.OrderName;
        NotesEditor.Text = _currentOrder.Notes;

        // Show status
        StatusSection.IsVisible = true;
        StatusLabel.Text = _currentOrder.StatusDisplayName;
        StatusBorder.BackgroundColor = Color.FromArgb(_currentOrder.StatusColor);

        // Show appropriate buttons
        FinalizeBtn.IsVisible = _currentOrder.CanFinalize;
        DeleteBtn.IsVisible = !_currentOrder.IsFinalized;

        // Disable editing if finalized
        bool isEditable = !_currentOrder.IsFinalized;
        OrderNameEntry.IsEnabled = isEditable;
        NotesEditor.IsEnabled = isEditable;
        AddItemBtn.IsVisible = isEditable;

        // Load order items
        _orderItems.Clear();
        foreach (var item in _currentOrder.OrderItems)
        {
            _orderItems.Add(item);
        }
    }

    /// <summary>
    /// Handle add item button click
    /// </summary>
    private async void OnAddItemClicked(object sender, EventArgs e)
    {
        try
        {
            // Get available inventory items
            var inventoryItems = await _inventoryService.GetInventoryItemsAsync(new InventoryFilter
            {
                ShowActiveOnly = true,
                PageSize = 1000 // Get all items for selection
            });

            // Filter out items that are already in the order or have no stock
            var availableItems = inventoryItems
                .Where(item => item.CurrentQuantity > 0 &&
                              !_orderItems.Any(oi => oi.InventoryItemId == item.Id))
                .ToList();

            if (!availableItems.Any())
            {
                await DisplayAlert("No Items Available",
                    "There are no inventory items available to add to this order.",
                    "OK");
                return;
            }

            // Show item selection popup
            await ShowItemSelectionPopup(availableItems);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to load inventory items: {ex.Message}", "OK");
        }
    }

    /// <summary>
    /// Show item selection popup
    /// </summary>
    private async Task ShowItemSelectionPopup(List<InventoryItemDto> availableItems)
    {
        try
        {
            // Create selection options
            var itemOptions = availableItems.Select(item =>
                $"{item.Name} ({item.ItemCode}) - Stock: {item.CurrentQuantity}")
                .ToArray();

            string selectedItem = await DisplayActionSheet(
                "Select Item to Add",
                "Cancel",
                null,
                itemOptions);

            if (selectedItem != "Cancel" && !string.IsNullOrEmpty(selectedItem))
            {
                // Find selected inventory item
                var selectedIndex = Array.IndexOf(itemOptions, selectedItem);
                if (selectedIndex >= 0 && selectedIndex < availableItems.Count)
                {
                    var inventoryItem = availableItems[selectedIndex];
                    await ShowQuantitySelectionPopup(inventoryItem);
                }
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to select item: {ex.Message}", "OK");
        }
    }

    /// <summary>
    /// Show quantity selection popup
    /// </summary>
    private async Task ShowQuantitySelectionPopup(InventoryItemDto inventoryItem)
    {
        try
        {
            string quantityStr = await DisplayPromptAsync(
                "Enter Quantity",
                $"How many {inventoryItem.Unit} of '{inventoryItem.Name}' do you want to add?\n\nAvailable: {inventoryItem.CurrentQuantity}",
                "Add",
                "Cancel",
                "1",
                keyboard: Keyboard.Numeric);

            if (string.IsNullOrEmpty(quantityStr)) return;

            if (int.TryParse(quantityStr, out int quantity) && quantity > 0)
            {
                if (quantity > inventoryItem.CurrentQuantity)
                {
                    await DisplayAlert("Invalid Quantity",
                        $"Cannot add {quantity} items. Only {inventoryItem.CurrentQuantity} available in stock.",
                        "OK");
                    return;
                }

                await AddItemToOrder(inventoryItem.Id, quantity);
            }
            else
            {
                await DisplayAlert("Invalid Quantity", "Please enter a valid positive number.", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to add item: {ex.Message}", "OK");
        }
    }

    /// <summary>
    /// Add item to order
    /// </summary>
    private async Task AddItemToOrder(int inventoryItemId, int quantity)
    {
        try
        {
            bool success;

            if (_currentOrder != null)
            {
                // Adding to existing order
                success = await _orderService.AddItemToOrderAsync(_currentOrder.Id, inventoryItemId, quantity);
            }
            else
            {
                // For new orders, we need to create the order first
                if (string.IsNullOrWhiteSpace(OrderNameEntry.Text))
                {
                    await DisplayAlert("Order Name Required",
                        "Please enter an order name before adding items.",
                        "OK");
                    return;
                }

                // Create the order
                _currentOrder = await _orderService.CreateOrderAsync(OrderNameEntry.Text, NotesEditor.Text ?? "");
                OrderId = _currentOrder.Id;

                // Now add the item
                success = await _orderService.AddItemToOrderAsync(_currentOrder.Id, inventoryItemId, quantity);
            }

            if (success)
            {
                // Reload order data
                await LoadExistingOrderAsync();
                await DisplayAlert("Success", "Item added to order successfully.", "OK");
            }
            else
            {
                await DisplayAlert("Error", "Failed to add item to order.", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to add item to order: {ex.Message}", "OK");
        }
    }

    /// <summary>
    /// Handle remove item button click
    /// </summary>
    private async void OnRemoveItemClicked(object sender, EventArgs e)
    {
        try
        {
            if (sender is Button button && button.CommandParameter is int orderItemId)
            {
                var orderItem = _orderItems.FirstOrDefault(oi => oi.Id == orderItemId);
                if (orderItem == null) return;

                bool confirm = await DisplayAlert(
                    "Remove Item",
                    $"Remove '{orderItem.ItemName}' from this order?\n\nThis will restore {orderItem.Quantity} items to inventory.",
                    "Yes, Remove",
                    "Cancel");

                if (confirm)
                {
                    bool success = await _orderService.RemoveItemFromOrderAsync(
                        _currentOrder!.Id,
                        orderItemId);

                    if (success)
                    {
                        await LoadExistingOrderAsync();
                        await DisplayAlert("Success", "Item removed from order and restored to inventory.", "OK");
                    }
                    else
                    {
                        await DisplayAlert("Error", "Failed to remove item from order.", "OK");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to remove item: {ex.Message}", "OK");
        }
    }

    /// <summary>
    /// Handle save button click
    /// </summary>
    private async void OnSaveClicked(object sender, EventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(OrderNameEntry.Text))
            {
                await DisplayAlert("Validation Error", "Order name is required.", "OK");
                return;
            }

            if (_currentOrder != null)
            {
                // Update existing order
                bool success = await _orderService.UpdateOrderAsync(
                    _currentOrder.Id,
                    OrderNameEntry.Text.Trim(),
                    NotesEditor.Text?.Trim() ?? "");

                if (success)
                {
                    await DisplayAlert("Success", "Order updated successfully.", "OK");
                    await ClosePopupAsync();
                }
                else
                {
                    await DisplayAlert("Error", "Failed to update order.", "OK");
                }
            }
            else
            {
                // Create new order
                var newOrder = await _orderService.CreateOrderAsync(
                    OrderNameEntry.Text.Trim(),
                    NotesEditor.Text?.Trim() ?? "");

                await DisplayAlert("Success", "Order created successfully.", "OK");
                await ClosePopupAsync();
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to save order: {ex.Message}", "OK");
        }
    }

    /// <summary>
    /// Handle finalize button click
    /// </summary>
    private async void OnFinalizeClicked(object sender, EventArgs e)
    {
        try
        {
            if (_currentOrder == null) return;

            bool confirm = await DisplayAlert(
                "Finalize Order",
                $"Are you sure you want to finalize '{_currentOrder.OrderName}'?\n\nThis action cannot be undone and the order cannot be modified after finalization.",
                "Yes, Finalize",
                "Cancel");

            if (confirm)
            {
                bool success = await _orderService.FinalizeOrderAsync(_currentOrder.Id);

                if (success)
                {
                    await DisplayAlert("Success", "Order has been finalized successfully.", "OK");
                    await ClosePopupAsync();
                }
                else
                {
                    await DisplayAlert("Error", "Failed to finalize order.", "OK");
                }
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to finalize order: {ex.Message}", "OK");
        }
    }

    /// <summary>
    /// Handle delete button click
    /// </summary>
    private async void OnDeleteClicked(object sender, EventArgs e)
    {
        try
        {
            if (_currentOrder == null) return;

            bool confirm = await DisplayAlert(
                "Delete Order",
                $"Are you sure you want to delete '{_currentOrder.OrderName}'?\n\nThis will restore all items to inventory and cannot be undone.",
                "Yes, Delete",
                "Cancel");

            if (confirm)
            {
                bool success = await _orderService.DeleteOrderAsync(_currentOrder.Id);

                if (success)
                {
                    await DisplayAlert("Success", "Order deleted successfully. All items have been restored to inventory.", "OK");
                    await ClosePopupAsync();
                }
                else
                {
                    await DisplayAlert("Error", "Failed to delete order.", "OK");
                }
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to delete order: {ex.Message}", "OK");
        }
    }

    /// <summary>
    /// Handle cancel button click
    /// </summary>
    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await ClosePopupAsync();
    }

    /// <summary>
    /// Properly close the modal popup
    /// </summary>
    private async Task ClosePopupAsync()
    {
        try
        {
            // For modal popups, use Navigation.PopModalAsync() or go back to the main route
            if (Navigation.ModalStack.Any())
            {
                await Navigation.PopModalAsync();
            }
            else
            {
                // Fallback: navigate back to orders page
                await Shell.Current.GoToAsync("//main/orders");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error closing popup: {ex.Message}");
            // Last resort: try to navigate to orders page
            try
            {
                await Shell.Current.GoToAsync("//main/orders");
            }
            catch (Exception fallbackEx)
            {
                System.Diagnostics.Debug.WriteLine($"Fallback navigation failed: {fallbackEx.Message}");
            }
        }
    }

    /// <summary>
    /// Update order summary display
    /// </summary>
    private void UpdateSummary()
    {
        if (_orderItems.Any())
        {
            SummarySection.IsVisible = true;
            TotalItemsLabel.Text = _orderItems.Sum(oi => oi.Quantity).ToString();
            TotalValueLabel.Text = _orderItems.Sum(oi => oi.TotalValue).ToString("C");
        }
        else
        {
            SummarySection.IsVisible = false;
        }
    }
}