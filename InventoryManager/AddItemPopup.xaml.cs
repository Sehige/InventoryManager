// AddItemPopup.xaml.cs - Complete Add Item Modal with Validation
// This handles all form validation, duplicate checking, and item creation
// Provides real-time feedback and prevents invalid data submission

using InventoryManager.Services;
using InventoryManager.Models;

namespace InventoryManager;

/// <summary>
/// Modal popup for adding new inventory items with comprehensive validation
/// This provides a user-friendly interface with real-time validation and duplicate checking
/// </summary>
public partial class AddItemPopup : ContentPage
{
    // Services for data operations
    private readonly InventoryService _inventoryService;
    private readonly AuthService _authService;
    private readonly DatabaseService _databaseService;

    // Current user information
    private User? _currentUser;
    private List<WarehouseLocation> _accessibleLocations;

    // Validation state tracking
    private bool _isItemCodeValid = false;
    private bool _isItemNameValid = false;
    private bool _isQuantityValid = false;
    private bool _isLocationValid = false;

    // Available categories for suggestions
    private List<string> _availableCategories = new();

    // Callback for when item is successfully added
    public event EventHandler<InventoryItem>? ItemAdded;

    /// <summary>
    /// Constructor that sets up the add item popup
    /// </summary>
    public AddItemPopup()
    {
        InitializeComponent();

        // Initialize services
        _databaseService = new DatabaseService();
        _authService = new AuthService(_databaseService);
        _inventoryService = new InventoryService(_databaseService, _authService);

        // Initialize the form
        _ = InitializeFormAsync();
    }

    /// <summary>
    /// Initialize the form with user data and default values
    /// </summary>
    private async Task InitializeFormAsync()
    {
        try
        {
            // Get current user and accessible locations
            _currentUser = await _authService.GetCurrentUserAsync();
            if (_currentUser == null)
            {
                await DisplayAlert("Error", "You must be logged in to add items", "OK");
                await Shell.Current.GoToAsync("..");
                return;
            }

            // Get accessible locations for the current user
            _accessibleLocations = WarehouseLocationHelper.GetAccessibleLocations(_currentUser.Role);

            // Populate location picker
            var locationNames = _accessibleLocations.Select(loc => loc.GetDisplayName()).ToList();
            LocationPicker.ItemsSource = locationNames;

            // Get available categories for suggestions
            _availableCategories = await _inventoryService.GetAvailableCategoriesAsync();

            // Set default values
            UnitPicker.SelectedIndex = 0; // Default to "pieces"
            QuantityEntry.Text = "1"; // Default quantity
            MinQuantityEntry.Text = "1"; // Default minimum
            MaxQuantityEntry.Text = "100"; // Default maximum

            // Set focus to the first field
            ItemCodeEntry.Focus();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to initialize form: {ex.Message}", "OK");
        }
    }

    // Real-time Validation Event Handlers

    /// <summary>
    /// Validate item code as user types
    /// Checks for duplicates and proper format
    /// </summary>
    private async void OnItemCodeChanged(object sender, TextChangedEventArgs e)
    {
        var itemCode = e.NewTextValue?.Trim() ?? "";

        // Clear previous error
        ItemCodeErrorLabel.IsVisible = false;
        _isItemCodeValid = false;

        if (string.IsNullOrWhiteSpace(itemCode))
        {
            ShowFieldError(ItemCodeErrorLabel, "Item code is required");
            UpdateConfirmButtonState();
            return;
        }

        if (itemCode.Length < 3)
        {
            ShowFieldError(ItemCodeErrorLabel, "Item code must be at least 3 characters");
            UpdateConfirmButtonState();
            return;
        }

        // Check for invalid characters
        if (!System.Text.RegularExpressions.Regex.IsMatch(itemCode, @"^[A-Za-z0-9\-_]+$"))
        {
            ShowFieldError(ItemCodeErrorLabel, "Item code can only contain letters, numbers, hyphens, and underscores");
            UpdateConfirmButtonState();
            return;
        }

        // Check for duplicates (with debouncing)
        await CheckItemCodeDuplicateAsync(itemCode);
    }

    /// <summary>
    /// Check if item code already exists in database
    /// </summary>
    private async Task CheckItemCodeDuplicateAsync(string itemCode)
    {
        try
        {
            // Add a small delay to avoid excessive database queries while typing
            await Task.Delay(300);

            // Check if the text is still the same (user might have continued typing)
            if (ItemCodeEntry.Text?.Trim() != itemCode)
                return;

            // Query database for existing item with this code
            var existingItems = await _inventoryService.GetInventoryItemsAsync(new InventoryFilter
            {
                SearchText = itemCode,
                ShowActiveOnly = true
            });

            var duplicateItem = existingItems.FirstOrDefault(i =>
                string.Equals(i.ItemCode, itemCode, StringComparison.OrdinalIgnoreCase));

            if (duplicateItem != null)
            {
                ShowFieldError(ItemCodeErrorLabel, $"Item code '{itemCode}' already exists");
                _isItemCodeValid = false;
            }
            else
            {
                _isItemCodeValid = true;
            }

            UpdateConfirmButtonState();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error checking item code duplicate: {ex.Message}");
        }
    }

    /// <summary>
    /// Validate item name as user types
    /// Checks for duplicates and minimum length
    /// </summary>
    private async void OnItemNameChanged(object sender, TextChangedEventArgs e)
    {
        var itemName = e.NewTextValue?.Trim() ?? "";

        // Clear previous error
        ItemNameErrorLabel.IsVisible = false;
        _isItemNameValid = false;

        if (string.IsNullOrWhiteSpace(itemName))
        {
            ShowFieldError(ItemNameErrorLabel, "Item name is required");
            UpdateConfirmButtonState();
            return;
        }

        if (itemName.Length < 3)
        {
            ShowFieldError(ItemNameErrorLabel, "Item name must be at least 3 characters");
            UpdateConfirmButtonState();
            return;
        }

        // Check for duplicate names (with debouncing)
        await CheckItemNameDuplicateAsync(itemName);
    }

    /// <summary>
    /// Check if item name already exists in database
    /// </summary>
    private async Task CheckItemNameDuplicateAsync(string itemName)
    {
        try
        {
            await Task.Delay(300); // Debouncing

            if (ItemNameEntry.Text?.Trim() != itemName)
                return;

            var existingItems = await _inventoryService.GetInventoryItemsAsync(new InventoryFilter
            {
                SearchText = itemName,
                ShowActiveOnly = true
            });

            var duplicateItem = existingItems.FirstOrDefault(i =>
                string.Equals(i.Name, itemName, StringComparison.OrdinalIgnoreCase));

            if (duplicateItem != null)
            {
                ShowFieldError(ItemNameErrorLabel, $"An item named '{itemName}' already exists");
                _isItemNameValid = false;
            }
            else
            {
                _isItemNameValid = true;
            }

            UpdateConfirmButtonState();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error checking item name duplicate: {ex.Message}");
        }
    }

    /// <summary>
    /// Validate quantity as user types
    /// </summary>
    private void OnQuantityChanged(object sender, TextChangedEventArgs e)
    {
        var quantityText = e.NewTextValue?.Trim() ?? "";

        QuantityErrorLabel.IsVisible = false;
        _isQuantityValid = false;

        if (string.IsNullOrWhiteSpace(quantityText))
        {
            ShowFieldError(QuantityErrorLabel, "Quantity is required");
            UpdateConfirmButtonState();
            return;
        }

        if (!int.TryParse(quantityText, out var quantity) || quantity < 0)
        {
            ShowFieldError(QuantityErrorLabel, "Quantity must be a non-negative number");
            UpdateConfirmButtonState();
            return;
        }

        if (quantity > 1000000)
        {
            ShowFieldError(QuantityErrorLabel, "Quantity seems too large. Please verify.");
            UpdateConfirmButtonState();
            return;
        }

        _isQuantityValid = true;

        // Auto-suggest min/max quantities if not already set
        AutoSuggestMinMaxQuantities(quantity);

        UpdateConfirmButtonState();
    }

    /// <summary>
    /// Auto-suggest reasonable min/max quantities based on initial quantity
    /// </summary>
    private void AutoSuggestMinMaxQuantities(int quantity)
    {
        // Only suggest if fields are empty or have default values
        if (string.IsNullOrWhiteSpace(MinQuantityEntry.Text) || MinQuantityEntry.Text == "1")
        {
            var suggestedMin = Math.Max(1, quantity / 10); // 10% of initial quantity
            MinQuantityEntry.Text = suggestedMin.ToString();
        }

        if (string.IsNullOrWhiteSpace(MaxQuantityEntry.Text) || MaxQuantityEntry.Text == "100")
        {
            var suggestedMax = Math.Max(quantity * 3, 100); // 3x initial quantity or 100, whichever is larger
            MaxQuantityEntry.Text = suggestedMax.ToString();
        }
    }

    /// <summary>
    /// Validate location selection
    /// </summary>
    private void OnLocationChanged(object sender, EventArgs e)
    {
        LocationErrorLabel.IsVisible = false;
        _isLocationValid = LocationPicker.SelectedIndex >= 0;

        if (!_isLocationValid)
        {
            ShowFieldError(LocationErrorLabel, "Please select a location");
        }

        UpdateConfirmButtonState();
    }

    /// <summary>
    /// Suggest categories based on existing items
    /// </summary>
    private async void OnCategorySuggestClicked(object sender, EventArgs e)
    {
        try
        {
            if (!_availableCategories.Any())
            {
                await DisplayAlert("No Suggestions", "No existing categories found to suggest from.", "OK");
                return;
            }

            var selectedCategory = await DisplayActionSheet(
                "Select Category", "Cancel", null, _availableCategories.ToArray());

            if (selectedCategory != "Cancel" && !string.IsNullOrEmpty(selectedCategory))
            {
                CategoryEntry.Text = selectedCategory;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error suggesting category: {ex.Message}");
        }
    }

    /// <summary>
    /// Handle cancel button click
    /// </summary>
    private async void OnCancelClicked(object sender, EventArgs e)
    {
        // Check if user has entered any data
        if (HasUserEnteredData())
        {
            var confirm = await DisplayAlert("Discard Changes",
                "Are you sure you want to cancel? All entered data will be lost.", "Yes", "No");

            if (!confirm)
                return;
        }

        // Close the popup
        await Shell.Current.GoToAsync("..");
    }

    /// <summary>
    /// Handle confirm/add button click
    /// </summary>
    private async void OnConfirmClicked(object sender, EventArgs e)
    {
        try
        {
            ConfirmButton.IsEnabled = false;
            ConfirmButton.Text = "Adding...";

            // Final validation
            if (!ValidateAllFields())
            {
                ShowValidationSummary();
                return;
            }

            // Create the new inventory item
            var newItem = CreateInventoryItemFromForm();

            // Save to database
            var createdItem = await _inventoryService.CreateInventoryItemAsync(newItem);

            if (createdItem != null)
            {
                // Success - notify parent and close popup
                ItemAdded?.Invoke(this, createdItem);
                await DisplayAlert("Success", $"Item '{createdItem.Name}' has been added successfully!", "OK");
                await Shell.Current.GoToAsync("..");
            }
            else
            {
                await DisplayAlert("Error", "Failed to add the item. Please try again.", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Error adding item: {ex.Message}", "OK");
        }
        finally
        {
            ConfirmButton.IsEnabled = true;
            ConfirmButton.Text = "Add Item";
        }
    }

    // Helper Methods

    /// <summary>
    /// Show validation error for a specific field
    /// </summary>
    private void ShowFieldError(Label errorLabel, string message)
    {
        errorLabel.Text = message;
        errorLabel.IsVisible = true;
    }

    /// <summary>
    /// Update the state of the confirm button based on validation
    /// </summary>
    private void UpdateConfirmButtonState()
    {
        var isFormValid = _isItemCodeValid && _isItemNameValid && _isQuantityValid && _isLocationValid;

        ConfirmButton.IsEnabled = isFormValid;
        ConfirmButton.BackgroundColor = isFormValid ? Colors.Green : Colors.LightGray;

        // Hide validation summary if form becomes valid
        if (isFormValid)
        {
            ValidationSummaryBorder.IsVisible = false;
        }
    }

    /// <summary>
    /// Validate all fields and return overall validity
    /// </summary>
    private bool ValidateAllFields()
    {
        var errors = new List<string>();

        if (!_isItemCodeValid)
            errors.Add("• Item code is invalid or already exists");

        if (!_isItemNameValid)
            errors.Add("• Item name is invalid or already exists");

        if (!_isQuantityValid)
            errors.Add("• Quantity must be a valid non-negative number");

        if (!_isLocationValid)
            errors.Add("• Please select a location");

        // Validate min/max quantities if provided
        if (!string.IsNullOrWhiteSpace(MinQuantityEntry.Text))
        {
            if (!int.TryParse(MinQuantityEntry.Text, out var minQty) || minQty < 0)
                errors.Add("• Minimum quantity must be a non-negative number");
        }

        if (!string.IsNullOrWhiteSpace(MaxQuantityEntry.Text))
        {
            if (!int.TryParse(MaxQuantityEntry.Text, out var maxQty) || maxQty < 0)
                errors.Add("• Maximum quantity must be a non-negative number");
        }

        // Validate unit cost if provided
        if (!string.IsNullOrWhiteSpace(UnitCostEntry.Text))
        {
            if (!decimal.TryParse(UnitCostEntry.Text, out var cost) || cost < 0)
                errors.Add("• Unit cost must be a non-negative number");
        }

        return !errors.Any();
    }

    /// <summary>
    /// Show validation summary with all current errors
    /// </summary>
    private void ShowValidationSummary()
    {
        var errors = new List<string>();

        if (!_isItemCodeValid) errors.Add("• Fix item code issues");
        if (!_isItemNameValid) errors.Add("• Fix item name issues");
        if (!_isQuantityValid) errors.Add("• Fix quantity issues");
        if (!_isLocationValid) errors.Add("• Select a location");

        if (errors.Any())
        {
            ValidationSummaryLabel.Text = string.Join("\n", errors);
            ValidationSummaryBorder.IsVisible = true;
        }
    }

    /// <summary>
    /// Check if user has entered any data
    /// </summary>
    private bool HasUserEnteredData()
    {
        return !string.IsNullOrWhiteSpace(ItemCodeEntry.Text) ||
               !string.IsNullOrWhiteSpace(ItemNameEntry.Text) ||
               !string.IsNullOrWhiteSpace(DescriptionEditor.Text) ||
               QuantityEntry.Text != "1" ||
               LocationPicker.SelectedIndex >= 0 ||
               !string.IsNullOrWhiteSpace(CategoryEntry.Text);
    }

    /// <summary>
    /// Create InventoryItem object from form data
    /// </summary>
    private InventoryItem CreateInventoryItemFromForm()
    {
        var selectedLocation = _accessibleLocations[LocationPicker.SelectedIndex];

        // Parse quantities with defaults
        int.TryParse(QuantityEntry.Text, out var quantity);
        int.TryParse(MinQuantityEntry.Text, out var minQuantity);
        int.TryParse(MaxQuantityEntry.Text, out var maxQuantity);
        decimal.TryParse(UnitCostEntry.Text, out var unitCost);

        return new InventoryItem
        {
            ItemCode = ItemCodeEntry.Text.Trim(),
            Name = ItemNameEntry.Text.Trim(),
            Description = DescriptionEditor.Text?.Trim() ?? "",
            CurrentQuantity = quantity,
            MinimumQuantity = minQuantity > 0 ? minQuantity : 1,
            MaximumQuantity = maxQuantity > 0 ? maxQuantity : Math.Max(quantity * 3, 100),
            Unit = UnitPicker.SelectedItem?.ToString() ?? "pieces",
            Location = selectedLocation,
            Category = CategoryEntry.Text?.Trim() ?? "General",
            Supplier = SupplierEntry.Text?.Trim() ?? "",
            UnitCost = unitCost,
            CreatedAt = DateTime.UtcNow,
            LastModifiedAt = DateTime.UtcNow,
            CreatedByUserId = _currentUser?.Id ?? "",
            LastModifiedByUserId = _currentUser?.Id ?? "",
            IsActive = true
        };
    }
}