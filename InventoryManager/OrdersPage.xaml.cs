// OrdersPage.xaml.cs - Orders management page following InventoryPage pattern
// This implements the table-based orders view with the same patterns as InventoryPage

using InventoryManager.Services;
using InventoryManager.Models;
using System.Collections.ObjectModel;

namespace InventoryManager;

/// <summary>
/// Orders management page with table-based UI following InventoryPage pattern
/// </summary>
public partial class OrdersPage : ContentPage
{
    // Services for business logic and data access
    private readonly OrderService _orderService;
    private readonly AuthService _authService;
    private readonly DatabaseService _databaseService;
    private readonly InventoryService _inventoryService;

    // Observable collection for the table data
    private ObservableCollection<OrderDto> _orders;

    // Current filter settings
    private OrderFilter _currentFilter;

    // Track the current user
    private User? _currentUser;

    /// <summary>
    /// Constructor that sets up the table-based orders page
    /// </summary>
    public OrdersPage()
    {
        InitializeComponent();

        // Initialize services following existing pattern
        _databaseService = new DatabaseService();
        _authService = new AuthService(_databaseService);
        _inventoryService = new InventoryService(_databaseService, _authService);
        _orderService = new OrderService(_databaseService, _authService, _inventoryService);

        // Initialize collections
        _orders = new ObservableCollection<OrderDto>();
        _currentFilter = new OrderFilter();

        // Connect the collection to the UI
        OrdersCollectionView.ItemsSource = _orders;

        // Set default filter values
        StatusFilterPicker.SelectedIndex = 0; // "All Orders"

        // Load data
        _ = LoadPageDataAsync();
    }

    /// <summary>
    /// Load all data needed for the orders page
    /// </summary>
    private async Task LoadPageDataAsync()
    {
        try
        {
            LoadingIndicator.IsVisible = true;
            LoadingIndicator.IsRunning = true;

            await LoadCurrentUserAsync();
            await LoadOrdersAsync();
            await UpdateSummaryStatsAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to load orders data: {ex.Message}", "OK");
        }
        finally
        {
            LoadingIndicator.IsVisible = false;
            LoadingIndicator.IsRunning = false;
        }
    }

    /// <summary>
    /// Load current user information
    /// </summary>
    private async Task LoadCurrentUserAsync()
    {
        _currentUser = await _authService.GetCurrentUserAsync();

        if (_currentUser == null)
        {
            await Shell.Current.GoToAsync("//login");
            return;
        }
    }

    /// <summary>
    /// Load orders with current filter
    /// </summary>
    private async Task LoadOrdersAsync()
    {
        try
        {
            var orders = await _orderService.GetOrdersAsync(_currentFilter);

            _orders.Clear();
            foreach (var order in orders)
            {
                _orders.Add(order);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to load orders: {ex.Message}", "OK");
        }
    }

    /// <summary>
    /// Update summary statistics in header
    /// </summary>
    private async Task UpdateSummaryStatsAsync()
    {
        try
        {
            var allOrdersFilter = new OrderFilter();
            var allOrders = await _orderService.GetOrdersAsync(allOrdersFilter);

            TotalOrdersLabel.Text = allOrders.Count.ToString();
            PendingOrdersLabel.Text = allOrders.Count(o => o.Status == OrderStatus.Pending).ToString();
            CompletedOrdersLabel.Text = allOrders.Count(o => o.Status == OrderStatus.Finalized).ToString();
        }
        catch (Exception ex)
        {
            // Don't show error for stats - just set defaults
            TotalOrdersLabel.Text = "0";
            PendingOrdersLabel.Text = "0";
            CompletedOrdersLabel.Text = "0";
        }
    }

    /// <summary>
    /// Handle refresh gesture
    /// </summary>
    private async void OnRefreshing(object sender, EventArgs e)
    {
        try
        {
            await LoadOrdersAsync();
            await UpdateSummaryStatsAsync();
        }
        finally
        {
            RefreshView.IsRefreshing = false;
        }
    }

    /// <summary>
    /// Handle search text changes
    /// </summary>
    private async void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        _currentFilter.SearchText = e.NewTextValue ?? string.Empty;
        await LoadOrdersAsync();
    }

    /// <summary>
    /// Handle status filter changes
    /// </summary>
    private async void OnStatusFilterChanged(object sender, EventArgs e)
    {
        var picker = sender as Picker;

        _currentFilter.Status = picker?.SelectedIndex switch
        {
            1 => OrderStatus.Pending,    // "Pending Only"
            2 => OrderStatus.Finalized,  // "Completed Only"
            _ => null                    // "All Orders"
        };

        await LoadOrdersAsync();
    }

    /// <summary>
    /// Handle new order button click
    /// </summary>
    private async void OnNewOrderClicked(object sender, EventArgs e)
    {
        try
        {
            await Shell.Current.GoToAsync("///OrderEditPopup", new Dictionary<string, object>
            {
                ["OrderId"] = 0 // 0 indicates new order
            });
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to open new order dialog: {ex.Message}", "OK");
        }
    }

    /// <summary>
    /// Handle edit order button click
    /// </summary>
    private async void OnEditOrderClicked(object sender, EventArgs e)
    {
        try
        {
            if (sender is Button button && button.CommandParameter is int orderId)
            {
                OrderEditPopup.OrderId = orderId;

                await Shell.Current.GoToAsync("///OrderEditPopup", new Dictionary<string, object>
                {
                    ["OrderId"] = orderId
                });
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to open order edit dialog: {ex.Message}", "OK");
        }
    }

    /// <summary>
    /// Handle finalize order button click
    /// </summary>
    private async void OnFinalizeOrderClicked(object sender, EventArgs e)
    {
        try
        {
            if (sender is Button button && button.CommandParameter is int orderId)
            {
                var order = _orders.FirstOrDefault(o => o.Id == orderId);
                if (order == null) return;

                bool confirm = await DisplayAlert(
                    "Finalize Order",
                    $"Are you sure you want to finalize order '{order.OrderName}'? This action cannot be undone.",
                    "Yes, Finalize",
                    "Cancel");

                if (confirm)
                {
                    bool success = await _orderService.FinalizeOrderAsync(orderId);

                    if (success)
                    {
                        await DisplayAlert("Success", "Order has been finalized successfully.", "OK");
                        await LoadOrdersAsync();
                        await UpdateSummaryStatsAsync();
                    }
                    else
                    {
                        await DisplayAlert("Error", "Failed to finalize order. Please try again.", "OK");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to finalize order: {ex.Message}", "OK");
        }
    }

    /// <summary>
    /// Handle page appearing - refresh data
    /// </summary>
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Refresh data when page appears (e.g., returning from edit popup)
        await LoadOrdersAsync();
        await UpdateSummaryStatsAsync();
    }

    /// <summary>
    /// Apply filters based on current filter settings
    /// </summary>
    private async Task ApplyFiltersAsync()
    {
        try
        {
            LoadingIndicator.IsVisible = true;
            LoadingIndicator.IsRunning = true;

            await LoadOrdersAsync();
        }
        finally
        {
            LoadingIndicator.IsVisible = false;
            LoadingIndicator.IsRunning = false;
        }
    }

    /// <summary>
    /// Clear all filters
    /// </summary>
    private async void OnClearFiltersClicked(object sender, EventArgs e)
    {
        SearchEntry.Text = string.Empty;
        StatusFilterPicker.SelectedIndex = 0;

        _currentFilter = new OrderFilter();
        await LoadOrdersAsync();
    }

    /// <summary>
    /// Handle order row tap for details view
    /// </summary>
    private async void OnOrderTapped(object sender, EventArgs e)
    {
        try
        {
            if (sender is Grid grid && grid.BindingContext is OrderDto order)
            {
                // Navigate to order details or edit
                await Shell.Current.GoToAsync("//OrderEditPopup", new Dictionary<string, object>
                {
                    ["OrderId"] = order.Id
                });
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to open order details: {ex.Message}", "OK");
        }
    }
}