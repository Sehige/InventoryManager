// MainPage.xaml.cs - Updated Login Logic with Proper Navigation to Inventory
// This updated version ensures users are directed to the main app after successful login
// The key change is in the NavigateToMainApp method which now goes to the tabbed interface

using InventoryManager.Services;
using InventoryManager.Models;

namespace InventoryManager;

/// <summary>
/// Main page that handles user authentication (login and registration)
/// This updated version properly integrates with the new tabbed navigation structure
/// After successful login, users are directed to the main application where they can access inventory
/// </summary>
public partial class MainPage : ContentPage
{
    // Private fields to hold our services (unchanged from original)
    private readonly DatabaseService _databaseService;
    private readonly AuthService _authService;

    // Track whether we're in login mode (true) or registration mode (false)
    private bool _isLoginMode = true;

    /// <summary>
    /// Constructor - sets up the page and initializes services
    /// This is exactly the same as your original version
    /// </summary>
    public MainPage()
    {
        InitializeComponent();

        _databaseService = new DatabaseService();
        _authService = new AuthService(_databaseService);

        // Initialize database asynchronously
        _ = InitializeDatabaseAsync();
    }

    /// <summary>
    /// Initialize the database and show the result to the user
    /// This method is unchanged from your original version
    /// </summary>
    private async Task InitializeDatabaseAsync()
    {
        try
        {
            await _databaseService.InitializeAsync();
            ShowStatus("Database ready", false);

            await Task.Delay(3000);
            StatusLabel.IsVisible = false;
        }
        catch (Exception ex)
        {
            ShowStatus($"Database error: {ex.Message}", true);
        }
    }

    // All your existing UI mode switching methods remain exactly the same
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
            ActionButton.Text = "Login";
            WelcomeLabel.Text = "Welcome Back!";
            LoginModeBtn.BackgroundColor = Colors.DarkBlue;
            RegisterModeBtn.BackgroundColor = Colors.Gray;
            FullNameEntry.IsVisible = false;
            RolePicker.IsVisible = false;
        }
        else
        {
            ActionButton.Text = "Register New User";
            WelcomeLabel.Text = "Create New Account";
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
    /// Handle user login attempt - this method is mostly unchanged
    /// The key difference is in the NavigateToMainApp call at the end
    /// </summary>
    private async Task HandleLoginAsync()
    {
        var username = UsernameEntry.Text?.Trim();
        var password = PasswordEntry.Text?.Trim();

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            ShowStatus("Please enter both username and password", true);
            return;
        }

        try
        {
            ShowStatus("Logging in...", false);
            ActionButton.Text = "Logging in...";

            var user = await _authService.LoginAsync(username, password);

            if (user != null)
            {
                await _authService.SaveUserSessionAsync(user);
                ShowStatus($"Welcome back, {user.FullName}!", false);

                await Task.Delay(1500);

                // THIS IS THE KEY CHANGE: Navigate to the main tabbed interface
                await NavigateToMainApp(user);
            }
            else
            {
                ShowStatus("Invalid username or password", true);
                ActionButton.Text = "Login";
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"Login failed: {ex.Message}", true);
            ActionButton.Text = "Login";
        }
    }

    /// <summary>
    /// Handle user registration - this method is unchanged from your original
    /// </summary>
    private async Task HandleRegisterAsync()
    {
        var fullName = FullNameEntry.Text?.Trim();
        var username = UsernameEntry.Text?.Trim();
        var password = PasswordEntry.Text?.Trim();
        var selectedRole = RolePicker.SelectedItem?.ToString();

        if (string.IsNullOrWhiteSpace(fullName))
        {
            ShowStatus("Please enter your full name", true);
            return;
        }

        if (string.IsNullOrWhiteSpace(username))
        {
            ShowStatus("Please enter a username", true);
            return;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            ShowStatus("Please enter a password", true);
            return;
        }

        if (string.IsNullOrWhiteSpace(selectedRole))
        {
            ShowStatus("Please select a role", true);
            return;
        }

        if (password.Length < 6)
        {
            ShowStatus("Password must be at least 6 characters long", true);
            return;
        }

        if (username.Length < 3)
        {
            ShowStatus("Username must be at least 3 characters long", true);
            return;
        }

        try
        {
            ShowStatus("Creating account...", false);
            ActionButton.Text = "Creating Account...";

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
                ShowStatus("Account created successfully! You can now log in.", false);
                await Task.Delay(2000);
                SetLoginMode(true);
                UsernameEntry.Text = username;
            }
            else
            {
                ShowStatus("Registration failed. Username might already exist.", true);
                ActionButton.Text = "Register New User";
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"Registration failed: {ex.Message}", true);
            ActionButton.Text = "Register New User";
        }
    }

    /// <summary>
    /// Navigate to the main application after successful login
    /// UPDATED: This now navigates to the main tabbed interface instead of just dashboard
    /// This is where users will spend most of their time managing inventory
    /// </summary>
    private async Task NavigateToMainApp(User user)
    {
        try
        {
            // Store user session information for use throughout the app
            await SecureStorage.SetAsync("current_user_id", user.Id);
            await SecureStorage.SetAsync("current_user_name", user.FullName);
            await SecureStorage.SetAsync("current_user_role", user.Role);
            await SecureStorage.SetAsync("current_username", user.Username);
            await SecureStorage.SetAsync("login_timestamp", DateTime.UtcNow.ToString());

            // CHANGED: Navigate to the main tabbed interface
            // This gives users immediate access to both dashboard and inventory
            // The route "//main/dashboard" goes to the main tab bar and selects the dashboard tab
            // Users can then easily switch to the inventory tab to see and manage items
            await Shell.Current.GoToAsync("//main/dashboard");

            System.Diagnostics.Debug.WriteLine($"User {user.FullName} logged in successfully and navigated to main app");
        }
        catch (Exception ex)
        {
            ShowStatus($"Navigation error: {ex.Message}", true);
            System.Diagnostics.Debug.WriteLine($"Navigation error: {ex.Message}");
        }
    }

    // All the remaining utility methods are unchanged from your original version
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

    /// <summary>
    /// UPDATED: Check for existing session and navigate appropriately
    /// This now directs users to the main tabbed interface if they have a valid session
    /// </summary>
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await CheckExistingSession();
    }

    /// <summary>
    /// Check if there's already a valid user session
    /// UPDATED: Navigates to main app instead of just dashboard
    /// This allows users to resume their work without having to log in every time
    /// </summary>
    private async Task CheckExistingSession()
    {
        try
        {
            var currentUser = await _authService.GetCurrentUserAsync();
            if (currentUser != null && await _authService.IsSessionValidAsync())
            {
                ShowStatus($"Welcome back, {currentUser.FullName}!", false);
                await Task.Delay(1000);

                // Navigate to main app with tabbed interface
                await Shell.Current.GoToAsync("//main/dashboard");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Session check error: {ex.Message}");
        }
    }
}