// MainPage.xaml.cs - Login page with language support
using InventoryManager.Services;
using InventoryManager.Models;

namespace InventoryManager;

/// <summary>
/// Main page that handles user authentication with language support
/// </summary>
public partial class MainPage : ContentPage
{
    private readonly DatabaseService _databaseService;
    private readonly AuthService _authService;
    private readonly LanguageManager _languageManager;
    private bool _isLoginMode = true;

    /// <summary>
    /// Constructor - sets up the page and initializes services
    /// </summary>
    public MainPage()
    {
        InitializeComponent();

        _databaseService = new DatabaseService();
        _authService = new AuthService(_databaseService);
        _languageManager = App.GetLanguageManager() ?? new LanguageManager();

        // Subscribe to language changes
        _languageManager.LanguageChanged += OnLanguageChanged;

        // Update UI with correct language
        UpdateUIText();

        // Initialize database asynchronously
        _ = InitializeDatabaseAsync();
    }

    /// <summary>
    /// Update all UI text based on current language
    /// </summary>
    private void UpdateUIText()
    {
        // Update button texts
        LoginModeBtn.Text = L.Get("Login");
        RegisterModeBtn.Text = L.Get("Register");

        // Update placeholders
        FullNameEntry.Placeholder = L.Get("FullName");
        UsernameEntry.Placeholder = L.Get("Username");
        PasswordEntry.Placeholder = L.Get("Password");
        RolePicker.Title = L.Get("SelectRole");

        // Update welcome text based on mode
        if (_isLoginMode)
        {
            WelcomeLabel.Text = L.Get("WelcomeBack");
            ActionButton.Text = L.Get("Login");
        }
        else
        {
            WelcomeLabel.Text = L.Get("CreateNewAccount");
            ActionButton.Text = L.Get("RegisterNewUser");
        }

        // Update help text
        DefaultAdminHelpLabel.Text = L.Get("DefaultAdminLogin");
    }

    /// <summary>
    /// Handle language change event
    /// </summary>
    private void OnLanguageChanged(object? sender, string newLanguage)
    {
        Device.BeginInvokeOnMainThread(() => UpdateUIText());
    }

    /// <summary>
    /// Initialize the database and show the result to the user
    /// </summary>
    private async Task InitializeDatabaseAsync()
    {
        try
        {
            await _databaseService.InitializeAsync();
            ShowStatus(L.Get("DatabaseReady"), false);

            await Task.Delay(3000);
            StatusLabel.IsVisible = false;
        }
        catch (Exception ex)
        {
            ShowStatus(L.Get("DatabaseError", ex.Message), true);
        }
    }

    private void OnLoginModeClicked(object sender, EventArgs e)
    {
        SetLoginMode(true);
    }

    private void OnRegisterModeClicked(object sender, EventArgs e)
    {
        SetLoginMode(false);
    }

    private void SetLoginMode(bool isLogin)
    {
        _isLoginMode = isLogin;

        if (isLogin)
        {
            ActionButton.Text = L.Get("Login");
            WelcomeLabel.Text = L.Get("WelcomeBack");
            LoginModeBtn.BackgroundColor = Colors.DarkBlue;
            RegisterModeBtn.BackgroundColor = Colors.Gray;
            FullNameEntry.IsVisible = false;
            RolePicker.IsVisible = false;
        }
        else
        {
            ActionButton.Text = L.Get("RegisterNewUser");
            WelcomeLabel.Text = L.Get("CreateNewAccount");
            LoginModeBtn.BackgroundColor = Colors.Gray;
            RegisterModeBtn.BackgroundColor = Colors.DarkBlue;
            FullNameEntry.IsVisible = true;
            RolePicker.IsVisible = true;
            RolePicker.SelectedIndex = 0;
        }

        StatusLabel.IsVisible = false;
        ClearInputFields();
    }

    private async void OnActionButtonClicked(object sender, EventArgs e)
    {
        ActionButton.IsEnabled = false;

        try
        {
            if (_isLoginMode)
            {
                await HandleLoginAsync();
            }
            else
            {
                await HandleRegisterAsync();
            }
        }
        finally
        {
            ActionButton.IsEnabled = true;
        }
    }

    /// <summary>
    /// Handle user login attempt
    /// </summary>
    private async Task HandleLoginAsync()
    {
        var username = UsernameEntry.Text?.Trim();
        var password = PasswordEntry.Text?.Trim();

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            ShowStatus(L.Get("PleaseEnterCredentials"), true);
            return;
        }

        try
        {
            ShowStatus(L.Get("LoggingIn"), false);
            ActionButton.Text = L.Get("LoggingIn");

            var user = await _authService.LoginAsync(username, password);

            if (user != null)
            {
                await _authService.SaveUserSessionAsync(user);
                ShowStatus(L.Get("WelcomeUser", user.FullName), false);

                await Task.Delay(1500);
                await NavigateToMainApp(user);
            }
            else
            {
                ShowStatus(L.Get("InvalidCredentials"), true);
                ActionButton.Text = L.Get("Login");
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"{L.Get("Error")}: {ex.Message}", true);
            ActionButton.Text = L.Get("Login");
        }
    }

    /// <summary>
    /// Handle user registration
    /// </summary>
    private async Task HandleRegisterAsync()
    {
        var fullName = FullNameEntry.Text?.Trim();
        var username = UsernameEntry.Text?.Trim();
        var password = PasswordEntry.Text?.Trim();
        var selectedRole = RolePicker.SelectedItem?.ToString();

        if (string.IsNullOrWhiteSpace(fullName))
        {
            ShowStatus($"{L.Get("PleaseEnterCredentials")} - {L.Get("FullName")}", true);
            return;
        }

        if (string.IsNullOrWhiteSpace(username))
        {
            ShowStatus($"{L.Get("PleaseEnterCredentials")} - {L.Get("Username")}", true);
            return;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            ShowStatus($"{L.Get("PleaseEnterCredentials")} - {L.Get("Password")}", true);
            return;
        }

        if (string.IsNullOrWhiteSpace(selectedRole))
        {
            ShowStatus(L.Get("SelectRole"), true);
            return;
        }

        if (password.Length < 6)
        {
            ShowStatus($"{L.Get("Error")}: Password must be at least 6 characters", true);
            return;
        }

        if (username.Length < 3)
        {
            ShowStatus($"{L.Get("Error")}: Username must be at least 3 characters", true);
            return;
        }

        try
        {
            ShowStatus(L.Get("CreatingAccount"), false);
            ActionButton.Text = L.Get("CreatingAccount");

            var newUser = new User
            {
                Id = Guid.NewGuid().ToString(),
                Username = username,
                FullName = fullName,
                Role = selectedRole,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            var success = await _authService.RegisterAsync(newUser, password);

            if (success)
            {
                ShowStatus(L.Get("AccountCreatedSuccessfully"), false);
                await Task.Delay(2000);
                SetLoginMode(true);
                UsernameEntry.Text = username;
            }
            else
            {
                ShowStatus($"{L.Get("Error")}: Username already exists", true);
                ActionButton.Text = L.Get("RegisterNewUser");
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"{L.Get("Error")}: {ex.Message}", true);
            ActionButton.Text = L.Get("RegisterNewUser");
        }
    }

    /// <summary>
    /// Navigate to the main application after successful login
    /// </summary>
    private async Task NavigateToMainApp(User user)
    {
        try
        {
            await SecureStorage.SetAsync("current_user_id", user.Id);
            await SecureStorage.SetAsync("current_user_name", user.FullName);
            await SecureStorage.SetAsync("current_user_role", user.Role);
            await SecureStorage.SetAsync("current_username", user.Username);
            await SecureStorage.SetAsync("login_timestamp", DateTime.UtcNow.ToString());

            await Shell.Current.GoToAsync("//main/dashboard");

            System.Diagnostics.Debug.WriteLine($"User {user.FullName} logged in successfully and navigated to main app");
        }
        catch (Exception ex)
        {
            ShowStatus($"{L.Get("Error")}: {ex.Message}", true);
            System.Diagnostics.Debug.WriteLine($"Navigation error: {ex.Message}");
        }
    }

    private void ShowStatus(string message, bool isError)
    {
        StatusLabel.Text = message;
        StatusLabel.TextColor = isError ? Colors.Red : Colors.Green;
        StatusLabel.IsVisible = true;

        if (!isError)
        {
            Device.StartTimer(TimeSpan.FromSeconds(5), () =>
            {
                StatusLabel.IsVisible = false;
                return false;
            });
        }
    }

    private void ClearInputFields()
    {
        FullNameEntry.Text = "";
        UsernameEntry.Text = "";
        PasswordEntry.Text = "";
        RolePicker.SelectedIndex = -1;
    }

    protected override bool OnBackButtonPressed()
    {
        return false;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await CheckExistingSession();
    }

    /// <summary>
    /// Check if there's already a valid user session
    /// </summary>
    private async Task CheckExistingSession()
    {
        try
        {
            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser != null && await _authService.IsSessionValidAsync())
            {
                ShowStatus(L.Get("WelcomeUser", currentUser.FullName), false);
                await Task.Delay(1000);
                await Shell.Current.GoToAsync("//main/dashboard");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Session check error: {ex.Message}");
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // Unsubscribe from language changes to prevent memory leaks
        _languageManager.LanguageChanged -= OnLanguageChanged;
    }
}