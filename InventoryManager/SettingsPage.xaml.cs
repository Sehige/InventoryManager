// SettingsPage.xaml.cs - Simple settings page with language management
using InventoryManager.Services;

namespace InventoryManager
{
    /// <summary>
    /// Settings page that allows users to configure app preferences
    /// </summary>
    public partial class SettingsPage : ContentPage
    {
        private readonly LanguageManager _languageManager;
        private readonly AuthService _authService;

        public SettingsPage()
        {
            InitializeComponent();

            // Get services
            _languageManager = App.GetLanguageManager() ?? new LanguageManager();
            _authService = App.GetAuthService() ?? throw new InvalidOperationException("Auth service not available");

            // Subscribe to language changes
            _languageManager.LanguageChanged += OnLanguageChanged;

            // Load initial data
            _ = LoadSettingsAsync();
        }

        private async Task LoadSettingsAsync()
        {
            try
            {
                // Update UI with localized text
                UpdateUIText();

                // Load user info
                await LoadUserInfoAsync();

                // Load app info
                LoadAppInfo();
            }
            catch (Exception ex)
            {
                await DisplayAlert(L.Get("Error"), ex.Message, L.Get("OK"));
            }
        }

        private void UpdateUIText()
        {
            // Update all UI text with localized strings
            Title = L.Get("Settings");

            SettingsHeaderLabel.Text = L.Get("Settings");

            LanguageSectionLabel.Text = L.Get("Language");
            UpdateCurrentLanguageDisplay();
            LanguageHintLabel.Text = "Click to toggle between English and Romanian";

            UserInfoSectionLabel.Text = "User Information";
            AppInfoSectionLabel.Text = "Application Information";

            BackButton.Text = "Back to " + L.Get("Dashboard");
            LogoutButton.Text = L.Get("Logout");
        }

        private void UpdateCurrentLanguageDisplay()
        {
            var currentLang = _languageManager.CurrentLanguage;
            var langInfo = _languageManager.GetAvailableLanguages()
                .FirstOrDefault(l => l.Code == currentLang);

            if (langInfo != null)
            {
                CurrentLanguageLabel.Text = $"Current: {langInfo.Flag} {langInfo.NativeName}";
            }
            else
            {
                CurrentLanguageLabel.Text = $"Current: {currentLang}";
            }
        }

        private void OnLanguageChanged(object? sender, string newLanguage)
        {
            Device.BeginInvokeOnMainThread(() => {
                UpdateUIText();
                _ = LoadUserInfoAsync();
            });
        }

        private async Task LoadUserInfoAsync()
        {
            try
            {
                var currentUser = await _authService.GetCurrentUserAsync();
                if (currentUser != null)
                {
                    UserDetailsLabel.Text =
                        $"{L.Get("Username")}: {currentUser.Username}\n" +
                        $"{L.Get("FullName")}: {currentUser.FullName}\n" +
                        $"{L.Get("Role")}: {L.Get(currentUser.Role)}";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading user info: {ex.Message}");
            }
        }

        private void LoadAppInfo()
        {
            AppVersionLabel.Text = $"Version: {AppInfo.VersionString}";
            DeviceInfoLabel.Text = $"Platform: {DeviceInfo.Platform}\n" +
                                  $"Model: {DeviceInfo.Model}\n" +
                                  $"OS: {DeviceInfo.VersionString}";
        }

        private async void OnBackClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("//main/dashboard");
        }

        private async void OnLogoutClicked(object sender, EventArgs e)
        {
            var confirm = await DisplayAlert(
                L.Get("Logout"),
                L.Get("AreYouSureLogout"),
                L.Get("Yes"),
                L.Get("No"));

            if (confirm)
            {
                await _authService.LogoutAsync();
                await AppShell.LogoutAndReturnToLoginAsync();
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _languageManager.LanguageChanged -= OnLanguageChanged;
        }
    }
}