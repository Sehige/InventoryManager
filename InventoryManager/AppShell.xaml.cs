// AppShell.xaml.cs - Updated Navigation Logic with Inventory Management
// This file manages navigation between pages and ensures proper security controls
// Think of this as the traffic controller for your entire application

namespace InventoryManager;

/// <summary>
/// AppShell manages the overall navigation structure and routing for the application
/// This updated version includes proper inventory page navigation and enhanced security
/// It ensures users can only access features appropriate for their role and authentication status
/// </summary>
public partial class AppShell : Shell
{
    /// <summary>
    /// Constructor that sets up the navigation shell with inventory management
    /// This establishes the foundation for how users move through your application
    /// </summary>
    public AppShell()
    {
        InitializeComponent();

        // Register additional routes that can be navigated to programmatically
        // These are like "hidden" pages that don't appear in the main navigation
        RegisterAdditionalRoutes();

        // Set up navigation event handlers to implement security and user experience features
        SetupNavigationEvents();

        // Initialize with proper starting state
        InitializeNavigationState();
    }

    /// <summary>
    /// Register additional routes for programmatic navigation
    /// These routes allow you to navigate to specific pages with parameters
    /// Think of these as direct pathways to specialized features
    /// </summary>
    private void RegisterAdditionalRoutes()
    {
        // Register routes for future detailed pages
        // These will be useful as your application grows more sophisticated

        // Example routes you might add later:
        // Routing.RegisterRoute("item/details", typeof(ItemDetailsPage));
        // Routing.RegisterRoute("item/edit", typeof(EditItemPage));
        // Routing.RegisterRoute("admin/users", typeof(UserManagementPage));
        // Routing.RegisterRoute("admin/locations", typeof(LocationManagementPage));
        // Routing.RegisterRoute("reports/inventory", typeof(InventoryReportPage));

        // For now, we'll keep it simple and focus on the main navigation
        System.Diagnostics.Debug.WriteLine("AppShell routes registered successfully");
    }

    /// <summary>
    /// Set up event handlers for navigation events
    /// This is where we implement security checks and user experience enhancements
    /// Every time someone tries to navigate, we can intercept and validate the request
    /// </summary>
    private void SetupNavigationEvents()
    {
        // Handle navigation attempts before they occur
        // This is our security checkpoint where we verify permissions
        this.Navigating += OnShellNavigating;

        // Handle successful navigation completion
        // This is where we can update UI state and track user behavior
        this.Navigated += OnShellNavigated;
    }

    /// <summary>
    /// Initialize the navigation state when the app starts
    /// This ensures users start in the appropriate place based on their authentication status
    /// </summary>
    private void InitializeNavigationState()
    {
        // Check if we should start at login or go directly to the main app
        // This happens asynchronously so it doesn't block app startup
        _ = CheckInitialNavigationAsync();
    }

    /// <summary>
    /// Check initial navigation state based on stored authentication
    /// This determines whether users go to login or directly to the main app
    /// </summary>
    private async Task CheckInitialNavigationAsync()
    {
        try
        {
            // Check if there's a valid stored authentication session
            var userId = await SecureStorage.GetAsync("current_user_id");

            if (!string.IsNullOrEmpty(userId))
            {
                // User has a stored session - check if it's still valid
                var loginTimestamp = await SecureStorage.GetAsync("login_timestamp");

                if (!string.IsNullOrEmpty(loginTimestamp) &&
                    DateTime.TryParse(loginTimestamp, out var loginTime))
                {
                    // Check if the session hasn't expired (30 days in this example)
                    var sessionDuration = DateTime.UtcNow - loginTime;
                    if (sessionDuration.TotalDays < 30)
                    {
                        // Valid session exists - navigate to main app
                        await GoToAsync("//main/dashboard");
                        return;
                    }
                }
            }

            // No valid session - ensure we're at the login page
            await GoToAsync("//login");
        }
        catch (Exception ex)
        {
            // If anything goes wrong with session checking, default to login
            System.Diagnostics.Debug.WriteLine($"Error checking initial navigation: {ex.Message}");
            await GoToAsync("//login");
        }
    }

    /// <summary>
    /// Handle navigation attempts with security validation
    /// This is our security gateway that ensures users only access appropriate pages
    /// Think of this as a security guard checking IDs at each doorway
    /// </summary>
    private async void OnShellNavigating(object? sender, ShellNavigatingEventArgs e)
    {
        var targetRoute = e.Target.Location.OriginalString;
        System.Diagnostics.Debug.WriteLine($"Navigation attempt to: {targetRoute}");

        // Always allow navigation to the login page
        if (targetRoute.Contains("login"))
        {
            return; // Login page is always accessible
        }

        // For all other pages, verify authentication
        if (targetRoute.Contains("main") ||
            targetRoute.Contains("dashboard") ||
            targetRoute.Contains("inventory"))
        {
            try
            {
                // Check if user has a valid authentication session
                var userId = await SecureStorage.GetAsync("current_user_id");
                var userRole = await SecureStorage.GetAsync("current_user_role");

                if (string.IsNullOrEmpty(userId))
                {
                    // No authentication - redirect to login
                    e.Cancel();
                    await GoToAsync("//login");
                    return;
                }

                // Additional role-based checks for specific pages
                await ValidatePageAccess(targetRoute, userRole, e);
            }
            catch (Exception ex)
            {
                // If there's any error with authentication checking, redirect to login for safety
                System.Diagnostics.Debug.WriteLine($"Navigation security check error: {ex.Message}");
                e.Cancel();
                await GoToAsync("//login");
            }
        }
    }

    /// <summary>
    /// Validate that the user has permission to access specific pages
    /// This implements role-based access control at the navigation level
    /// </summary>
    private async Task ValidatePageAccess(string targetRoute, string? userRole, ShellNavigatingEventArgs e)
    {
        // Most pages are accessible to all authenticated users
        // but you might want to restrict certain features in the future

        if (targetRoute.Contains("admin") && userRole != "Admin")
        {
            // Admin-only pages require administrator role
            e.Cancel();
            await DisplayAlert("Access Denied",
                "You don't have permission to access administrative features.", "OK");
            return;
        }

        // Inventory page is accessible to all roles, but they'll see different content
        // based on their location access permissions (handled within the page itself)

        // Future role restrictions might include:
        // - Reports page only for Managers and Admins
        // - User management only for Admins
        // - Certain inventory functions only for Managers and above
    }

    /// <summary>
    /// Handle successful navigation completion
    /// This tracks where users go and can update UI state accordingly
    /// </summary>
    private void OnShellNavigated(object? sender, ShellNavigatedEventArgs e)
    {
        var currentRoute = e.Current.Location.OriginalString;
        System.Diagnostics.Debug.WriteLine($"Navigation completed to: {currentRoute}");

        // You could add analytics tracking here to understand user behavior
        // TrackPageView(currentRoute);

        // Update any global UI state based on the current page
        UpdateGlobalUIState(currentRoute);
    }

    /// <summary>
    /// Update global UI state based on current page
    /// This can change the app's appearance or behavior based on where the user is
    /// </summary>
    private void UpdateGlobalUIState(string currentRoute)
    {
        // Example: You might want to change the app title or show different toolbar items
        // based on which page is currently active

        if (currentRoute.Contains("inventory"))
        {
            // When on inventory page, you might want to show different options
        }
        else if (currentRoute.Contains("dashboard"))
        {
            // Dashboard might have different global UI needs
        }
    }

    /// <summary>
    /// Handle hardware back button behavior on Android
    /// This ensures the back button works appropriately on different pages
    /// </summary>
    protected override bool OnBackButtonPressed()
    {
        var currentRoute = Shell.Current.CurrentState.Location.OriginalString;

        if (currentRoute.Contains("login"))
        {
            // On login page, back button should exit the app
            return false; // Allow default behavior (exit app)
        }
        else if (currentRoute.Contains("dashboard"))
        {
            // On dashboard, back button should also exit rather than go to login
            return false; // Allow default behavior (exit app)
        }
        else if (currentRoute.Contains("inventory"))
        {
            // On inventory page, back button should go to dashboard
            _ = GoToAsync("//main/dashboard");
            return true; // We handled the back button
        }

        // For other pages, allow normal back navigation
        return base.OnBackButtonPressed();
    }

    /// <summary>
    /// Static method to safely navigate to any route with authentication checking
    /// This provides a centralized way to navigate that always includes security checks
    /// Use this method throughout your app instead of direct navigation calls
    /// </summary>
    public static async Task<bool> SafeNavigateToAsync(string route)
    {
        try
        {
            // Check authentication before attempting navigation
            var userId = await SecureStorage.GetAsync("current_user_id");

            if (string.IsNullOrEmpty(userId) && !route.Contains("login"))
            {
                // Not authenticated and trying to access protected content
                await Shell.Current.GoToAsync("//login");
                return false;
            }

            // Perform the navigation
            await Shell.Current.GoToAsync(route);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Safe navigation error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Navigate to the inventory page with proper authentication checking
    /// This is a convenience method for the most common navigation in your app
    /// </summary>
    public static async Task<bool> GoToInventoryAsync()
    {
        return await SafeNavigateToAsync("//main/inventory");
    }

    /// <summary>
    /// Navigate to the dashboard with proper authentication checking
    /// </summary>
    public static async Task<bool> GoToDashboardAsync()
    {
        return await SafeNavigateToAsync("//main/dashboard");
    }

    /// <summary>
    /// Log out the current user and return to login page
    /// This clears all authentication data and provides a clean logout experience
    /// </summary>
    public static async Task LogoutAndReturnToLoginAsync()
    {
        try
        {
            // Clear all stored authentication data
            SecureStorage.RemoveAll();

            // Navigate to login page
            await Shell.Current.GoToAsync("//login");

            System.Diagnostics.Debug.WriteLine("User logged out successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Logout error: {ex.Message}");
        }
    }

    /// <summary>
    /// Get display information about the current user for UI purposes
    /// This provides user context that can be shown in the interface
    /// </summary>
    public static async Task<(string name, string role)> GetCurrentUserDisplayInfoAsync()
    {
        try
        {
            var userName = await SecureStorage.GetAsync("current_user_name") ?? "Unknown User";
            var userRole = await SecureStorage.GetAsync("current_user_role") ?? "Unknown Role";

            return (userName, userRole);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error getting user display info: {ex.Message}");
            return ("Unknown User", "Unknown Role");
        }
    }
}