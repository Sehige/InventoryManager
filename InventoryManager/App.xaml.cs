// App.xaml.cs - Updated with comprehensive error handling and debugging
using InventoryManager.Services;
using System.Diagnostics;

namespace InventoryManager;

public partial class App : Application
{
    private static LanguageManager? _languageManager;
    private static AuthService? _authService;
    private readonly ISyncService? _syncService;
    private readonly DatabaseService _databaseService;

    public App(IServiceProvider serviceProvider)
    {
        try
        {
            Debug.WriteLine("=== APP STARTUP BEGIN ===");
            InitializeComponent();
            Debug.WriteLine("InitializeComponent completed");

            // Get services from DI with error handling
            try
            {
                _databaseService = serviceProvider.GetRequiredService<DatabaseService>();
                Debug.WriteLine("DatabaseService obtained");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CRITICAL: Failed to get DatabaseService: {ex.Message}");
                throw;
            }

            try
            {
                _authService = serviceProvider.GetRequiredService<AuthService>();
                Debug.WriteLine("AuthService obtained");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CRITICAL: Failed to get AuthService: {ex.Message}");
                throw;
            }

            try
            {
                _syncService = serviceProvider.GetService<ISyncService>();
                Debug.WriteLine("SyncService obtained");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WARNING: Failed to get SyncService: {ex.Message}");
                // Don't throw - sync is optional
            }

            try
            {
                _languageManager = serviceProvider.GetRequiredService<LanguageManager>();
                Debug.WriteLine("LanguageManager obtained");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CRITICAL: Failed to get LanguageManager: {ex.Message}");
                throw;
            }

            // Initialize database in background with error handling
            Task.Run(async () =>
            {
                try
                {
                    Debug.WriteLine("Starting database initialization...");
                    await _databaseService.InitializeAsync();
                    Debug.WriteLine("Database initialized successfully");

                    // Start background sync if configured
                    if (_syncService != null)
                    {
                        Debug.WriteLine("Starting background sync...");
                        _syncService.StartBackgroundSync();
                        Debug.WriteLine("Background sync started");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"ERROR in database initialization: {ex}");

                    // Try to show error to user
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        try
                        {
                            if (MainPage != null)
                            {
                                await MainPage.DisplayAlert("Database Error",
                                    $"Failed to initialize database: {ex.Message}", "OK");
                            }
                        }
                        catch
                        {
                            // Can't show alert
                        }
                    });
                }
            });

            Debug.WriteLine("Creating AppShell...");
            MainPage = new AppShell();
            Debug.WriteLine("=== APP STARTUP COMPLETE ===");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"CRITICAL ERROR IN APP CONSTRUCTOR: {ex}");

            // Try to show a basic error page
            try
            {
                MainPage = new ContentPage
                {
                    Content = new VerticalStackLayout
                    {
                        Padding = 20,
                        Children =
                        {
                            new Label
                            {
                                Text = "Critical Startup Error",
                                FontSize = 24,
                                TextColor = Colors.Red,
                                HorizontalOptions = LayoutOptions.Center
                            },
                            new Label
                            {
                                Text = ex.Message,
                                FontSize = 16,
                                Margin = new Thickness(0, 20)
                            },
                            new Label
                            {
                                Text = ex.StackTrace ?? "No stack trace available",
                                FontSize = 12,
                                TextColor = Colors.Gray
                            }
                        }
                    }
                };
            }
            catch
            {
                // Even the error page failed - app will crash
            }

            throw; // Re-throw to see in debugger
        }
    }

    // Static getters with null checks
    public static AuthService? GetAuthService()
    {
        Debug.WriteLine($"GetAuthService called, returning: {_authService != null}");
        return _authService;
    }

    public static LanguageManager? GetLanguageManager()
    {
        Debug.WriteLine($"GetLanguageManager called, returning: {_languageManager != null}");
        return _languageManager;
    }

    protected override void OnStart()
    {
        Debug.WriteLine("App.OnStart called");
        base.OnStart();

        try
        {
            _syncService?.StartBackgroundSync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in OnStart: {ex.Message}");
        }
    }

    protected override void OnSleep()
    {
        Debug.WriteLine("App.OnSleep called");
        base.OnSleep();

        try
        {
            _syncService?.StopBackgroundSync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in OnSleep: {ex.Message}");
        }
    }

    protected override void OnResume()
    {
        Debug.WriteLine("App.OnResume called");
        base.OnResume();

        try
        {
            _syncService?.StartBackgroundSync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in OnResume: {ex.Message}");
        }
    }
}