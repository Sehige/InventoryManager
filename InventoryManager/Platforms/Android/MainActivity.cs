// Platforms/Android/MainActivity.cs
// Make sure your MainActivity looks like this

using Android.App;
using Android.Content.PM;
using Android.OS;
using System.Diagnostics;
using Debug = System.Diagnostics.Debug;

namespace InventoryManager;

[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        try
        {
            Debug.WriteLine("=== MAINACTIVITY OnCreate START ===");
            base.OnCreate(savedInstanceState);
            Debug.WriteLine("=== MAINACTIVITY OnCreate COMPLETE ===");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"CRITICAL ERROR IN MAINACTIVITY: {ex}");
            throw;
        }
    }
}