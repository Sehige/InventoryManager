// InventoryPage.xaml.cs - FIXED Table with Border and Corrected Entry Usage
// This version removes the SelectAll method and updates Frame references to Border

using InventoryManager.Services;
using InventoryManager.Models;
using System.Collections.ObjectModel;
using InventoryManager.Views;

namespace InventoryManager;

/// <summary>
/// FIXED Table-based inventory page with Border controls and corrected Entry usage
/// </summary>
public partial class InventoryPage : ContentPage
{
    // Services for business logic and data access
    private readonly InventoryService _inventoryService;
    private readonly AuthService _authService;
    private readonly DatabaseService _databaseService;

    // Observable collection for the table data
    private ObservableCollection<InventoryItemDto> _inventoryItems;

    // Current filter settings
    private InventoryFilter _currentFilter;

    // Track the current user
    private User? _currentUser;

    // Track which item is currently being edited (to prevent multiple edits)
    private InventoryItemDto? _currentlyEditingItem;
    private int _nQuantityOld;

    // Store accessible locations for the current user
    private List<WarehouseLocation> _accessibleLocations;

    /// <summary>
    /// Constructor that sets up the table-based inventory page
    /// </summary>
    public InventoryPage()
    {
        InitializeComponent();

        // Initialize services
        _databaseService = new DatabaseService();
        _authService = new AuthService(_databaseService);
        _inventoryService = new InventoryService(_databaseService, _authService);

        // Initialize collections
        _inventoryItems = new ObservableCollection<InventoryItemDto>();
        _currentFilter = new InventoryFilter();
        _accessibleLocations = new List<WarehouseLocation>();

        // Connect the collection to the UI
        InventoryCollectionView.ItemsSource = _inventoryItems;

        // Load data
        _ = LoadPageDataAsync();
    }

    /// <summary>
    /// Load all data needed for the inventory page
    /// </summary>
    private async Task LoadPageDataAsync()
    {
        try
        {
            LoadingIndicator.IsVisible = true;
            LoadingIndicator.IsRunning = true;

            await LoadCurrentUserAsync();
            await SetupFiltersAsync();
            await LoadInventoryItemsAsync();
            await UpdateSummaryStatsAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to load inventory data: {ex.Message}", "OK");
        }
        finally
        {
            LoadingIndicator.IsVisible = false;
            LoadingIndicator.IsRunning = false;
        }
    }

    /// <summary>
    /// Load current user information and determine permissions
    /// </summary>
    private async Task LoadCurrentUserAsync()
    {
        _currentUser = await _authService.GetCurrentUserAsync();

        if (_currentUser == null)
        {
            await Shell.Current.GoToAsync("//login");
            return;
        }

        // Get locations accessible to this user
        _accessibleLocations = WarehouseLocationHelper.GetAccessibleLocations(_currentUser.Role);

        // Show admin actions if user is admin
        AdminActionsSection.IsVisible = _currentUser.Role == "Admin";

        // Update page title
        Title = $"Inventory - {_currentUser.FullName}";
    }

    /// <summary>
    /// Set up filter dropdown controls
    /// </summary>
    private async Task SetupFiltersAsync()
    {
        try
        {
            // Set up location filter
            var locationOptions = new List<string> { "All Locations" };
            var accessibleLocationNames = _accessibleLocations
                .Select(loc => loc.GetDisplayName())
                .ToList();
            locationOptions.AddRange(accessibleLocationNames);
            LocationPicker.ItemsSource = locationOptions;
            LocationPicker.SelectedIndex = 0;

            // Set up category filter
            var categoryOptions = new List<string> { "All Categories" };
            var availableCategories = await _inventoryService.GetAvailableCategoriesAsync();
            categoryOptions.AddRange(availableCategories);
            CategoryPicker.ItemsSource = categoryOptions;
            CategoryPicker.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error setting up filters: {ex.Message}");
        }
    }

    /// <summary>
    /// Load inventory items based on current filter settings
    /// </summary>
    private async Task LoadInventoryItemsAsync()
    {
        try
        {
            var items = await _inventoryService.GetInventoryItemsAsync(_currentFilter);

            // Clear existing items
            _inventoryItems.Clear();

            // Add items with alternating row indicator
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                // Add a property to track odd/even rows for alternating backgrounds
                var itemWithRowInfo = new InventoryItemDtoWithRowInfo(item, i % 2 == 1);
                _inventoryItems.Add(itemWithRowInfo);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to load inventory items: {ex.Message}", "OK");
        }
    }

    /// <summary>
    /// Update summary statistics in the header
    /// </summary>
    private async Task UpdateSummaryStatsAsync()
    {
        try
        {
            var stats = await _inventoryService.GetInventoryStatsAsync();

            TotalItemsLabel.Text = stats.GetValueOrDefault("TotalItems", 0).ToString();
            LowStockLabel.Text = stats.GetValueOrDefault("LowStockItems", 0).ToString();

            if (stats.TryGetValue("TotalValue", out var totalValue) && totalValue is decimal value)
            {
                TotalValueLabel.Text = $"${value:F2}";
            }
            else
            {
                TotalValueLabel.Text = "$0.00";
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error updating summary stats: {ex.Message}");
        }
    }

    // Event Handlers for Inline Editing

    /// <summary>
    /// Handle tap on quantity cell to start editing
    /// FIXED: Updated to work with Border instead of Frame
    /// </summary>
    private void OnQuantityTapped(object sender, TappedEventArgs e)
    {
        try
        {
            // Prevent multiple edits at once
            if (_currentlyEditingItem != null)
            {
                return;
            }

            if (e.Parameter is InventoryItemDto item)
            {
                _currentlyEditingItem = new InventoryItemDto
                {
                    Id = item.Id,
                    CurrentQuantity = item.CurrentQuantity
                };

                // Find the tapped border and its parent container
                // FIXED: Changed from Frame to Border
                if (sender is Border tappedBorder)
                {
                    var parentStack = tappedBorder.Parent as StackLayout;
                    if (parentStack != null)
                    {
                        // Find the display border and edit entry in the same container
                        // FIXED: Updated to look for Border instead of Frame
                        var displayBorder = parentStack.Children.OfType<Border>().FirstOrDefault();
                        var editEntry = parentStack.Children.OfType<Entry>().FirstOrDefault();

                        if (displayBorder != null && editEntry != null)
                        {
                            // Switch to edit mode
                            displayBorder.IsVisible = false;
                            editEntry.IsVisible = true;
                            editEntry.Text = item.CurrentQuantity.ToString();
                            editEntry.Focus();

                            // REMOVED: SelectAll() method doesn't exist in MAUI Entry
                            // The focus and text setting is sufficient for good UX
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error starting quantity edit: {ex.Message}");
        }
    }

    /// <summary>
    /// Handle completion of quantity editing (Enter key pressed)
    /// </summary>
    private async void OnQuantityEditCompleted(object sender, EventArgs e)
    {
        if (sender is Entry entry)
        {
            await SaveQuantityEdit(entry);
        }
    }

    /// <summary>
    /// Handle quantity edit losing focus (user tapped elsewhere)
    /// </summary>
    private async void OnQuantityEditUnfocused(object sender, FocusEventArgs e)
    {
        if (sender is Entry entry)
        {
            await SaveQuantityEdit(entry);
        }
    }

    /// <summary>
    /// Save the edited quantity value to the database
    /// </summary>
    private async Task SaveQuantityEdit(Entry editEntry)
    {
        try
        {
            if (_currentlyEditingItem == null)
                return;

            // Parse the new quantity
            if (!int.TryParse(editEntry.Text, out var newQuantity) || newQuantity < 0)
            {
                await DisplayAlert("Invalid Quantity", "Please enter a valid non-negative number.", "OK");

                // Reset to original value
                editEntry.Text = _currentlyEditingItem.CurrentQuantity.ToString();
                return;
            }

            // Calculate the change
            var quantityChange = newQuantity - _currentlyEditingItem.CurrentQuantity;

            if (quantityChange != 0)
            {
                // Determine transaction type
                var transactionType = quantityChange > 0 ? "Manual Adjustment - Increase" : "Manual Adjustment - Decrease";
                var notes = $"Quantity adjusted from {_currentlyEditingItem.CurrentQuantity} to {newQuantity} via table edit";

                // Save the change using the inventory service
                var success = await _inventoryService.AdjustInventoryQuantityAsync(
                    _currentlyEditingItem.Id, quantityChange, transactionType, notes);

                if (success)
                {
                    // Update the local item
                    _currentlyEditingItem.CurrentQuantity = newQuantity;
                    _currentlyEditingItem.TotalValue = newQuantity * _currentlyEditingItem.UnitCost;
                    _currentlyEditingItem.IsLowStock = newQuantity <= _currentlyEditingItem.MinimumQuantity;

                    // Refresh summary stats
                    await UpdateSummaryStatsAsync();

                    // Show brief success feedback (removed await to make it non-blocking)
                    _ = DisplayAlert("Success", "Quantity updated successfully", "OK");
                }
                else
                {
                    await DisplayAlert("Error", "Failed to update quantity", "OK");
                    editEntry.Text = _currentlyEditingItem.CurrentQuantity.ToString();
                }
            }

            // Switch back to display mode
            SwitchToDisplayMode(editEntry);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Error saving quantity: {ex.Message}", "OK");
            SwitchToDisplayMode(editEntry);
        }
        finally
        {
            _currentlyEditingItem = null;
        }
    }

    /// <summary>
    /// Switch a quantity cell back to display mode from edit mode
    /// FIXED: Updated to work with Border instead of Frame
    /// </summary>
    private void SwitchToDisplayMode(Entry editEntry)
    {
        try
        {
            var parentStack = editEntry.Parent as StackLayout;
            if (parentStack != null)
            {
                // FIXED: Look for Border instead of Frame
                Border? displayBorder = parentStack.Children.OfType<Border>().FirstOrDefault();
                if (displayBorder != null)
                {
                    editEntry.IsVisible = false;
                    displayBorder.IsVisible = true;

                    var layout = displayBorder.Content as Layout;
                    if (layout != null)
                    {
                        var quantityLabel = layout.Children.OfType<Label>().FirstOrDefault();
                        if (quantityLabel != null)
                        {
                            quantityLabel.Text = editEntry.Text;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error switching to display mode: {ex.Message}");
        }
    }

    // Filter Event Handlers

    /// <summary>
    /// Handle search text changes
    /// </summary>
    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        _currentFilter.SearchText = e.NewTextValue ?? string.Empty;
        DebounceAndReload();
    }

    /// <summary>
    /// Handle location filter changes
    /// </summary>
    private void OnLocationFilterChanged(object sender, EventArgs e)
    {
        if (LocationPicker.SelectedIndex <= 0)
        {
            _currentFilter.Locations.Clear();
        }
        else
        {
            var selectedLocationName = LocationPicker.SelectedItem?.ToString();
            var selectedLocation = _accessibleLocations.FirstOrDefault(loc =>
                loc.GetDisplayName() == selectedLocationName);

            if (selectedLocation != default(WarehouseLocation))
            {
                _currentFilter.Locations.Clear();
                _currentFilter.Locations.Add(selectedLocation);
            }
        }

        _ = LoadInventoryItemsAsync();
    }

    /// <summary>
    /// Handle category filter changes
    /// </summary>
    private void OnCategoryFilterChanged(object sender, EventArgs e)
    {
        if (CategoryPicker.SelectedIndex <= 0)
        {
            _currentFilter.Category = string.Empty;
        }
        else
        {
            _currentFilter.Category = CategoryPicker.SelectedItem?.ToString() ?? string.Empty;
        }

        _ = LoadInventoryItemsAsync();
    }

    /// <summary>
    /// Handle low stock filter toggle
    /// </summary>
    private void OnLowStockFilterChanged(object sender, CheckedChangedEventArgs e)
    {
        _currentFilter.ShowLowStockOnly = e.Value;
        _ = LoadInventoryItemsAsync();
    }

    /// <summary>
    /// Handle pull-to-refresh gesture
    /// </summary>
    private async void OnRefreshing(object sender, EventArgs e)
    {
        try
        {
            await LoadInventoryItemsAsync();
            await UpdateSummaryStatsAsync();
        }
        finally
        {
            RefreshView.IsRefreshing = false;
        }
    }

    /// <summary>
    /// Handle add new item button click (admin only) - UPDATED to use popup
    /// Replace your existing OnAddItemClicked method with this version
    /// </summary>
    private async void OnAddItemClicked(object sender, EventArgs e)
    {
        if (_currentUser?.Role != "Admin")
        {
            await DisplayAlert("Access Denied", "Only administrators can add inventory items.", "OK");
            return;
        }

        try
        {
            // Create and show the popup
            var addItemPopup = new AddItemPopup();

            // Subscribe to the ItemAdded event to refresh the inventory list
            addItemPopup.ItemAdded += OnNewItemAdded;

            // Navigate to the popup
            await Shell.Current.GoToAsync("AddItemPopup");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to open add item dialog: {ex.Message}", "OK");
        }
    }

    /// <summary>
    /// Handle when a new item is successfully added via the popup
    /// This refreshes the inventory display to show the new item
    /// </summary>
    private async void OnNewItemAdded(object? sender, InventoryItem newItem)
    {
        try
        {
            // Refresh the inventory list to show the new item
            await LoadInventoryItemsAsync();
            await UpdateSummaryStatsAsync();

            // Refresh filters in case this created a new category
            await SetupFiltersAsync();

            // Optional: Show a brief success message
            System.Diagnostics.Debug.WriteLine($"New item added: {newItem.Name}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error refreshing after item add: {ex.Message}");
        }
    }

    /// <summary>
    /// Show a dialog for adding a new inventory item
    /// </summary>
    private async Task ShowAddItemDialog()
    {
        var itemCode = await DisplayPromptAsync("New Item", "Enter item code:", "Next", "Cancel");
        if (string.IsNullOrWhiteSpace(itemCode)) return;

        var itemName = await DisplayPromptAsync("New Item", "Enter item name:", "Next", "Cancel");
        if (string.IsNullOrWhiteSpace(itemName)) return;

        var quantityStr = await DisplayPromptAsync("New Item", "Enter initial quantity:", "Next", "Cancel",
            keyboard: Keyboard.Numeric, initialValue: "0");
        if (!int.TryParse(quantityStr, out var quantity)) return;

        // Let user select location from accessible locations
        var locationNames = _accessibleLocations.Select(loc => loc.GetDisplayName()).ToArray();
        var locationChoice = await DisplayActionSheet("Select Location", "Cancel", null, locationNames);
        if (locationChoice == "Cancel" || string.IsNullOrEmpty(locationChoice)) return;

        var selectedLocation = _accessibleLocations.First(loc => loc.GetDisplayName() == locationChoice);

        try
        {
            var newItem = new InventoryItem
            {
                ItemCode = itemCode,
                Name = itemName,
                Description = "Created via table interface",
                CurrentQuantity = quantity,
                MinimumQuantity = Math.Max(1, quantity / 10),
                MaximumQuantity = quantity * 5,
                Unit = "pieces",
                Location = selectedLocation,
                Category = "General",
                Supplier = "Manual Entry",
                UnitCost = 0m
            };

            var created = await _inventoryService.CreateInventoryItemAsync(newItem);

            if (created != null)
            {
                await DisplayAlert("Success", "New item created successfully", "OK");
                await LoadInventoryItemsAsync();
                await UpdateSummaryStatsAsync();
                await SetupFiltersAsync();
            }
            else
            {
                await DisplayAlert("Error", "Failed to create new item", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to create item: {ex.Message}", "OK");
        }
    }

    /// <summary>
    /// Debounce search input to avoid excessive filtering
    /// </summary>
    private void DebounceAndReload()
    {
        Device.StartTimer(TimeSpan.FromMilliseconds(500), () =>
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await LoadInventoryItemsAsync();
            });
            return false;
        });
    }

    /// <summary>
    /// Handle page appearing event
    /// </summary>
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadPageDataAsync();

        // Refresh inventory list in case items were updated from QR scanner
        //_ = LoadInventoryItemsAsync();
    }

    private async void OnScanQRClicked(object sender, EventArgs e)
    {
        try
        {
            // Check camera permissions first
            var status = await Permissions.CheckStatusAsync<Permissions.Camera>();

            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.Camera>();
            }

            if (status == PermissionStatus.Granted)
            {
                // Navigate to QR scanner page
                await Navigation.PushAsync(new QRScannerPage(_inventoryService, _authService));
            }
            else
            {
                await DisplayAlert("Permission Denied",
                    "Camera permission is required to scan QR codes.",
                    "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error",
                $"Failed to open QR scanner: {ex.Message}",
                "OK");
        }
    }
}

/// <summary>
/// Extended InventoryItemDto that includes row information for alternating backgrounds
/// </summary>
public class InventoryItemDtoWithRowInfo : InventoryItemDto
{
    public bool IsOddRow { get; set; }

    public InventoryItemDtoWithRowInfo(InventoryItemDto source, bool isOddRow)
    {
        // Copy all properties from the source
        Id = source.Id;
        ItemCode = source.ItemCode;
        Name = source.Name;
        Description = source.Description;
        CurrentQuantity = source.CurrentQuantity;
        MinimumQuantity = source.MinimumQuantity;
        Unit = source.Unit;
        Location = source.Location;
        LocationDisplayName = source.LocationDisplayName;
        Category = source.Category;
        UnitCost = source.UnitCost;
        IsLowStock = source.IsLowStock;
        TotalValue = source.TotalValue;
        LastModifiedAt = source.LastModifiedAt;
        LastModifiedByUserName = source.LastModifiedByUserName;

        // Add the row information
        IsOddRow = isOddRow;
    }
}