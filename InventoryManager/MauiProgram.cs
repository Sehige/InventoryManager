// MauiProgram.cs - Fixed with proper method chaining
using Microsoft.Extensions.Logging;
using InventoryManager.Services;
using InventoryManager.Models;
using CommunityToolkit.Maui;
using System.Diagnostics;

namespace InventoryManager;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        Debug.WriteLine("=== MAUIPROGRAM START ===");

        var builder = MauiApp.CreateBuilder();

        Debug.WriteLine("Configuring MauiApp...");
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit() // Chain this directly after UseMauiApp
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        Debug.WriteLine("Registering services...");

        // Register existing services
        try
        {
            // Database must be singleton
            builder.Services.AddSingleton<DatabaseService>();
            Debug.WriteLine("DatabaseService registered");

            // Auth service depends on database
            builder.Services.AddSingleton<AuthService>();
            Debug.WriteLine("AuthService registered");

            // Inventory service depends on both
            builder.Services.AddSingleton<InventoryService>();
            Debug.WriteLine("InventoryService registered");

            // Language manager
            builder.Services.AddSingleton<LanguageManager>();
            Debug.WriteLine("LanguageManager registered");
        }
            catch (Exception ex)
            {
            Debug.WriteLine($"ERROR registering core services: {ex}");
            throw;
        }

        // Register sync configuration
        builder.Services.AddSingleton<SyncConfiguration>(sp =>
        {
            // Load from preferences or use defaults
            return new SyncConfiguration
            {
                AutoSyncEnabled = Preferences.Get("AutoSyncEnabled", true),
                SyncInterval = TimeSpan.FromMinutes(Preferences.Get("SyncIntervalMinutes", 15)),
                DefaultConflictResolution = Enum.Parse<ConflictResolutionStrategy>(
                    Preferences.Get("DefaultConflictResolution", "ServerWins")),
                SyncOnlyOnWifi = Preferences.Get("SyncOnlyOnWifi", false),
                MaxRetryAttempts = Preferences.Get("MaxRetryAttempts", 3),
                RetryDelay = TimeSpan.FromSeconds(Preferences.Get("RetryDelaySeconds", 30)),
                EnableBackgroundSync = Preferences.Get("EnableBackgroundSync", true)
            };
        });

        // Register HTTP client for cloud API
        builder.Services.AddHttpClient<ICloudApiClient, CloudApiClient>(client =>
        {
            // Configure base URL from preferences or configuration
            var baseUrl = Preferences.Get("CloudApiBaseUrl", "https://api.inventorymanager.com");
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // Register sync services
        Debug.WriteLine("Registering pages...");
        builder.Services.AddSingleton<IConnectivityService, ConnectivityService>();
        builder.Services.AddScoped<IConflictResolver, ConflictResolver>();
        builder.Services.AddScoped<IOfflineQueueService, OfflineQueueService>();
        builder.Services.AddScoped<ISyncService, SyncEngine>();

        // Register pages
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<DashboardPage>();
        builder.Services.AddTransient<InventoryPage>();

#if DEBUG
        builder.Logging.AddDebug();
        Debug.WriteLine("Debug logging enabled");
#endif

        Debug.WriteLine("Building MauiApp...");
        return builder.Build();
    }
}