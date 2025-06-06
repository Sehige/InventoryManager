// App.xaml.cs - Application Lifecycle and Global Event Handling
// This file manages the overall application lifecycle and handles app-wide events
// Think of it as the master controller that oversees your entire application

using InventoryManager.Services;

namespace InventoryManager;

/// <summary>
/// Main application class that handles app lifecycle events
/// This class manages what happens when your app starts, goes to background, or resumes
/// </summary>
public partial class App : Application
{
    // Services for handling authentication and database operations
    private DatabaseService? _databaseService;
    private AuthService? _authService;

    /// <summary>
    /// Constructor - called when the application starts
    /// This is where you initialize the app and set up the main page
    /// </summary>
    public App()
    {
        InitializeComponent();

        // Initialize services
        InitializeServices();

        // Set up the main application shell
        MainPage = new AppShell();
    }

    /// <summary>
    /// Initialize core services for the application
    /// This sets up the database and authentication systems
    /// </summary>
    private void InitializeServices()
    {
        try
        {
            // Create service instances
            _databaseService = new DatabaseService();
            _authService = new AuthService(_databaseService);

            // Initialize database asynchronously
            // We don't await this to avoid blocking app startup
            _ = InitializeDatabaseAsync();
        }
        catch (Exception ex)
        {
            // Log initialization error
            System.Diagnostics.Debug.WriteLine($"Service initialization error: {ex.Message}");
        }
    }

    /// <summary>
    /// Initialize the database in the background
    /// This ensures the database is ready when users need it
    /// </summary>
    private async Task InitializeDatabaseAsync()
    {
        try
        {
            if (_databaseService != null)
            {
                await _databaseService.InitializeAsync();
                System.Diagnostics.Debug.WriteLine("Database initialized successfully");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Database initialization error: {ex.Message}");
        }
    }

    /// <summary>
    /// Handle application start event
    /// This is called when the app is launched for the first time
    /// </summary>
    protected override void OnStart()
    {
        base.OnStart();

        System.Diagnostics.Debug.WriteLine("Application started");

        // You might want to track app launches for analytics
        // Analytics.TrackEvent("AppLaunched");

        // Check for any pending background tasks or notifications
        _ = HandleAppStartAsync();
    }

    /// <summary>
    /// Handle application sleep event
    /// This is called when the app goes to the background (user switches apps or locks screen)
    /// </summary>
    protected override void OnSleep()
    {
        base.OnSleep();

        System.Diagnostics.Debug.WriteLine("Application going to sleep");

        // Save any pending changes or state
        _ = HandleAppSleepAsync();
    }

    /// <summary>
    /// Handle application resume event
    /// This is called when the app comes back to the foreground
    /// </summary>
    protected override void OnResume()
    {
        base.OnResume();

        System.Diagnostics.Debug.WriteLine("Application resumed");

        // Check if session is still valid and refresh data if needed
        _ = HandleAppResumeAsync();
    }

    /// <summary>
    /// Handle tasks that need to run when the app starts
    /// This includes checking for updates, validating sessions, etc.
    /// </summary>
    private async Task HandleAppStartAsync()
    {
        try
        {
            // Check if user has a valid session
            if (_authService != null)
            {
                var currentUser = await _authService.GetCurrentUserAsync();
                var isSessionValid = await _authService.IsSessionValidAsync();

                if (currentUser != null && isSessionValid)
                {
                    System.Diagnostics.Debug.WriteLine($"Valid session found for user: {currentUser.Username}");

                    // Navigate directly to dashboard if user is already logged in
                    await Shell.Current.GoToAsync("//dashboard");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("No valid session found, staying on login page");

                    // Clear any invalid session data
                    await _authService.LogoutAsync();
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error handling app start: {ex.Message}");
        }
    }

    /// <summary>
    /// Handle tasks that need to run when the app goes to sleep
    /// This is important for saving user data and maintaining security
    /// </summary>
    private async Task HandleAppSleepAsync()
    {
        try
        {
            // Save any pending changes to the database
            if (_databaseService != null)
            {
                // You might want to force save any pending transactions here
                // await _databaseService.SaveChangesAsync();
            }

            // Record when the app went to sleep for session management
            await SecureStorage.SetAsync("last_sleep_time", DateTime.UtcNow.ToString());

            System.Diagnostics.Debug.WriteLine("App sleep tasks completed");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error handling app sleep: {ex.Message}");
        }
    }

    /// <summary>
    /// Handle tasks that need to run when the app resumes
    /// This includes security checks and data refresh
    /// </summary>
    private async Task HandleAppResumeAsync()
    {
        try
        {
            // Check how long the app was asleep
            var lastSleepTimeStr = await SecureStorage.GetAsync("last_sleep_time");
            if (!string.IsNullOrEmpty(lastSleepTimeStr) && DateTime.TryParse(lastSleepTimeStr, out var lastSleepTime))
            {
                var sleepDuration = DateTime.UtcNow - lastSleepTime;

                // If app was asleep for more than 30 minutes, require re-authentication
                if (sleepDuration.TotalMinutes > 30)
                {
                    System.Diagnostics.Debug.WriteLine("App was asleep too long, requiring re-authentication");

                    if (_authService != null)
                    {
                        await _authService.LogoutAsync();
                    }

                    await Shell.Current.GoToAsync("//login");
                    return;
                }
            }

            // Verify current session is still valid
            if (_authService != null)
            {
                var isSessionValid = await _authService.IsSessionValidAsync();
                if (!isSessionValid)
                {
                    System.Diagnostics.Debug.WriteLine("Session expired, redirecting to login");
                    await Shell.Current.GoToAsync("//login");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Session still valid after resume");
                }
            }

            // You might want to refresh data from the server here
            // await RefreshDataFromServer();

        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error handling app resume: {ex.Message}");
        }
    }

    /// <summary>
    /// Get the current database service instance
    /// This allows other parts of the app to access the database if needed
    /// </summary>
    public static DatabaseService? GetDatabaseService()
    {
        if (Current is App app)
        {
            return app._databaseService;
        }
        return null;
    }

    /// <summary>
    /// Get the current authentication service instance
    /// This allows other parts of the app to access authentication if needed
    /// </summary>
    public static AuthService? GetAuthService()
    {
        if (Current is App app)
        {
            return app._authService;
        }
        return null;
    }

    /// <summary>
    /// Handle unhandled exceptions globally
    /// This is a safety net for any errors that aren't caught elsewhere
    /// </summary>
    public static void HandleGlobalException(Exception ex)
    {
        // Log the error
        System.Diagnostics.Debug.WriteLine($"Global exception: {ex.Message}");

        // In a production app, you might want to:
        // 1. Send error reports to a crash reporting service
        // 2. Show a user-friendly error message
        // 3. Attempt to recover gracefully

        // For now, we'll just log it
        System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
    }

    /// <summary>
    /// Check if the app is running in development mode
    /// This can be useful for enabling debug features
    /// </summary>
    public static bool IsDebugMode()
    {
#if DEBUG
        return true;
#else
        return false;
#endif
    }

    /// <summary>
    /// Get app version information
    /// This is useful for display in settings or about pages
    /// </summary>
    public static string GetAppVersion()
    {
        return AppInfo.VersionString;
    }

    /// <summary>
    /// Get platform information
    /// This helps with platform-specific features
    /// </summary>
    public static string GetPlatformInfo()
    {
        return $"{DeviceInfo.Platform} {DeviceInfo.VersionString}";
    }
}