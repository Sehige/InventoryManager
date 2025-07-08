// App.xaml.cs - Updated with simple language manager initialization
using InventoryManager.Services;

namespace InventoryManager;

/// <summary>
/// Main application class with language support
/// </summary>
public partial class App : Application
{
    // Services for handling authentication, database operations, and language
    private DatabaseService? _databaseService;
    private AuthService? _authService;
    private LanguageManager? _languageManager;

    /// <summary>
    /// Constructor - initializes the application
    /// </summary>
    public App()
    {
        InitializeComponent();

        // Initialize services including language manager
        InitializeServices();

        // Set the main page to our shell
        MainPage = new AppShell();
    }

    /// <summary>
    /// Initialize core services for the application
    /// This sets up the database, authentication, and language systems
    /// </summary>
    private void InitializeServices()
    {
        try
        {
            // Create service instances
            _databaseService = new DatabaseService();
            _authService = new AuthService(_databaseService);
            _languageManager = new LanguageManager();

            // Initialize the static language helper
            L.Initialize(_languageManager);

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
    /// Get the current language manager instance
    /// This allows other parts of the app to access language manager if needed
    /// </summary>
    public static LanguageManager? GetLanguageManager()
    {
        if (Current is App app)
        {
            return app._languageManager;
        }
        return null;
    }

    /// <summary>
    /// Override to handle when the app starts
    /// Good place for any startup logic
    /// </summary>
    protected override void OnStart()
    {
        // Handle when your app starts
        System.Diagnostics.Debug.WriteLine("InventoryManager App Started");
    }

    /// <summary>
    /// Override to handle when the app sleeps/goes to background
    /// Good place to save any pending data
    /// </summary>
    protected override void OnSleep()
    {
        // Handle when your app sleeps
        System.Diagnostics.Debug.WriteLine("InventoryManager App Sleeping");
    }

    /// <summary>
    /// Override to handle when the app resumes from background
    /// Good place to refresh any data
    /// </summary>
    protected override void OnResume()
    {
        // Handle when your app resumes
        System.Diagnostics.Debug.WriteLine("InventoryManager App Resumed");
    }
}