// Controls/LanguageToggle.xaml.cs - Language toggle component code-behind
using InventoryManager.Services;

namespace InventoryManager.Controls
{
    /// <summary>
    /// Simple language toggle control that can be added to any page
    /// </summary>
    public partial class LanguageToggle : ContentView
    {
        private readonly LanguageManager _languageManager;

        public LanguageToggle()
        {
            InitializeComponent();

            _languageManager = App.GetLanguageManager() ?? new LanguageManager();
            _languageManager.LanguageChanged += OnLanguageChanged;

            UpdateButtonDisplay();
        }

        /// <summary>
        /// Handle toggle button click - switches between languages
        /// </summary>
        private void OnToggleClicked(object sender, EventArgs e)
        {
            _languageManager.ToggleLanguage();

            // Optional: Show a quick confirmation
            if (Application.Current?.MainPage != null)
            {
                Application.Current.MainPage.DisplayAlert(
                    L.Get("Success"),
                    L.Get("LanguageChangedMessage"),
                    L.Get("OK"));
            }
        }

        /// <summary>
        /// Update button display based on current language
        /// </summary>
        private void UpdateButtonDisplay()
        {
            var currentLang = _languageManager.CurrentLanguage;

            switch (currentLang)
            {
                case "en":
                    ToggleButton.Text = "???? EN";
                    break;
                case "ro":
                    ToggleButton.Text = "???? RO";
                    break;
                default:
                    ToggleButton.Text = currentLang.ToUpper();
                    break;
            }
        }

        /// <summary>
        /// Handle language change event
        /// </summary>
        private void OnLanguageChanged(object? sender, string newLanguage)
        {
            Device.BeginInvokeOnMainThread(() => UpdateButtonDisplay());
        }

        /// <summary>
        /// Clean up event subscriptions
        /// </summary>
        ~LanguageToggle()
        {
            if (_languageManager != null)
            {
                _languageManager.LanguageChanged -= OnLanguageChanged;
            }
        }
    }
}