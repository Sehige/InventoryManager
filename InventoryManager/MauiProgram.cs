// MauiProgram.cs - FIXED Configuration Issues
// This version fixes the UseMauiCommunityToolkit and AddConsole errors
// All builder chain methods are properly ordered and console logging is removed to avoid dependency issues

using CommunityToolkit.Maui;
using InventoryManager.Services;
using Microsoft.Extensions.Logging;

namespace InventoryManager;

/// <summary>
/// Fixed MauiProgram that resolves the builder chain and logging configuration errors
/// This version properly configures MAUI Community Toolkit and removes problematic console logging
/// </summary>
public static class MauiProgram
{
    /// <summary>
    /// Create and configure the MAUI application with fixed configuration chain
    /// FIXED: Proper builder chain order and removed problematic console logging
    /// </summary>
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        // Configure the MAUI app with Community Toolkit - THIS IS THE FIXED CHAIN ORDER
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit() // FIXED: This must be chained directly after UseMauiApp<T>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Configure logging for debugging and monitoring
        ConfigureLogging(builder);

        // Register all services for dependency injection
        RegisterServices(builder);

        // Register all pages for dependency injection and navigation
        RegisterPages(builder);

        // Build and return the configured app
        return builder.Build();
    }

    /// <summary>
    /// Configure logging for the application
    /// FIXED: Removed AddConsole() to avoid dependency issues
    /// </summary>
    private static void ConfigureLogging(MauiAppBuilder builder)
    {
        // AddDebug is always available in MAUI projects
        builder.Logging.AddDebug();

#if DEBUG
        // Set minimum log level for debug builds
        builder.Logging.SetMinimumLevel(LogLevel.Debug);

        // REMOVED: AddConsole() because it requires additional NuGet packages
        // Debug logging is sufficient for development and doesn't require extra dependencies
#endif

        System.Diagnostics.Debug.WriteLine("Logging configured successfully");
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

        System.Diagnostics.Debug.WriteLine("All pages registered successfully");
    }
}