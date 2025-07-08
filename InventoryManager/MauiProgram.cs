// MauiProgram.cs - Updated with language manager registration
using CommunityToolkit.Maui;
using InventoryManager.Services;
using Microsoft.Extensions.Logging;

namespace InventoryManager;

/// <summary>
/// Main entry point for configuring the MAUI application
/// Sets up all services, pages, and dependencies
/// </summary>
public static class MauiProgram
{
    /// <summary>
    /// Creates and configures the MAUI application
    /// This is called by the platform-specific code to start the app
    /// </summary>
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        // Configure the app to use the App class as the main application
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit() // FIXED: This must be chained directly after UseMauiApp<T>()
            .ConfigureFonts(fonts =>
            {
                // Add default fonts
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Register all services for dependency injection
        RegisterServices(builder);

        // Register all pages for dependency injection
        RegisterPages(builder);

#if DEBUG
        // Add debug logging in development
        builder.Logging.AddDebug();
        System.Diagnostics.Debug.WriteLine("MauiProgram: Debug logging enabled");
#endif

        // Build and return the configured app
        var app = builder.Build();
        System.Diagnostics.Debug.WriteLine("MauiProgram: App built successfully");

        return app;
    }

    /// <summary>
    /// Register all services for dependency injection
    /// Services are registered in dependency order (dependencies first)
    /// </summary>
    private static void RegisterServices(MauiAppBuilder builder)
    {
        // Core database service - Must be registered first since other services depend on it
        builder.Services.AddSingleton<DatabaseService>();

        // Authentication service - Depends on DatabaseService
        builder.Services.AddSingleton<AuthService>();

        // Inventory service - Depends on both DatabaseService and AuthService
        builder.Services.AddSingleton<InventoryService>();

        // Language manager - For multi-language support
        builder.Services.AddSingleton<LanguageManager>();

        System.Diagnostics.Debug.WriteLine("All core services registered successfully");
    }

    /// <summary>
    /// Register all pages for dependency injection and navigation
    /// Pages are registered as Transient so we get fresh instances when needed
    /// </summary>
    private static void RegisterPages(MauiAppBuilder builder)
    {
        // Authentication pages
        builder.Services.AddTransient<MainPage>(); // Login/Register page

        // Main application pages
        builder.Services.AddTransient<DashboardPage>(); // Overview and stats
        builder.Services.AddTransient<InventoryPage>(); // Inventory list and management
        builder.Services.AddTransient<SettingsPage>(); // Settings and language selection

        System.Diagnostics.Debug.WriteLine("All pages registered successfully");
    }
}