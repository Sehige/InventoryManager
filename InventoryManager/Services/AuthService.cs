// Services/AuthService.cs
// This is your authentication business logic layer - it handles all the "smart" decisions about users
// Think of this as the security guard that checks IDs and decides who gets access to what

using InventoryManager.Models;

namespace InventoryManager.Services
{
    /// <summary>
    /// Authentication service that handles user login, registration, and session management
    /// This class acts as the bridge between your UI and the database for all user-related operations
    /// It encapsulates all the business rules about authentication and authorization
    /// </summary>
    public class AuthService
    {
        private readonly DatabaseService _databaseService;

        /// <summary>
        /// Constructor that accepts a DatabaseService instance
        /// This is called "dependency injection" - instead of creating our own database connection,
        /// we accept one that's passed to us. This makes testing easier and follows good design principles.
        /// </summary>
        public AuthService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        /// <summary>
        /// Authenticate a user with username and password
        /// This is the core login functionality that verifies credentials
        /// Returns the User object if successful, null if credentials are invalid
        /// </summary>
        public async Task<User?> LoginAsync(string username, string password)
        {
            // Input validation - always check your inputs before processing them
            // This prevents null reference exceptions and improves security
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                return null; // Invalid input, don't even try to authenticate
            }

            try
            {
                // First, find the user by username
                // We're only looking in active users - inactive accounts can't log in
                var user = await _databaseService.GetUserByUsernameAsync(username);

                // Check if user exists and verify the password
                if (user != null && BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                {
                    // BCrypt.Verify compares the plain text password with the stored hash
                    // It's cryptographically secure and protects against timing attacks

                    // Update the last login timestamp for audit purposes
                    // This helps track user activity and can be useful for security monitoring
                    await _databaseService.UpdateUserLastLoginAsync(user.Id);

                    // Return the authenticated user
                    return user;
                }

                // If we reach here, either the user doesn't exist or password is wrong
                // We return null in both cases to avoid giving attackers information
                // about which usernames exist in the system
                return null;
            }
            catch (Exception ex)
            {
                // Log the error for debugging, but don't expose internal details to the UI
                System.Diagnostics.Debug.WriteLine($"Login error: {ex.Message}");

                // Always return null on any error to maintain security
                // The UI will treat this as invalid credentials
                return null;
            }
        }

        /// <summary>
        /// Register a new user in the system
        /// This handles creating new user accounts with proper validation and security
        /// Returns true if registration succeeds, false if there are problems (like duplicate username)
        /// </summary>
        public async Task<bool> RegisterAsync(User user, string password)
        {
            // Comprehensive input validation
            // We check each field to ensure we have valid data before proceeding
            if (user == null ||
                string.IsNullOrWhiteSpace(user.Username) ||
                string.IsNullOrWhiteSpace(user.FullName) ||
                string.IsNullOrWhiteSpace(user.Role) ||
                string.IsNullOrWhiteSpace(password))
            {
                return false; // Invalid input data
            }

            // Password strength validation
            // Require at least 6 characters - you might want to add more rules later
            // Consider requiring uppercase, lowercase, numbers, and special characters
            if (password.Length < 6)
            {
                return false; // Password too weak
            }

            try
            {
                // Check if username already exists
                // Usernames must be unique across the entire system
                var existingUser = await _databaseService.GetUserByUsernameAsync(user.Username);
                if (existingUser != null)
                {
                    return false; // Username already taken
                }

                // Validate the role is one of our allowed values
                // This prevents someone from trying to create invalid roles
                var validRoles = new[] { "Admin", "Manager", "Operator" };
                if (!validRoles.Contains(user.Role))
                {
                    return false; // Invalid role specified
                }

                // Hash the password using BCrypt
                // BCrypt automatically handles salt generation and uses a secure work factor
                // The work factor makes it computationally expensive to crack passwords
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);

                // Set audit fields
                // These help track when accounts were created and ensure proper data integrity
                user.CreatedAt = DateTime.UtcNow;
                user.LastLoginAt = DateTime.MinValue; // Will be set on first login
                user.IsActive = true;

                // Generate a unique ID if one wasn't provided
                // GUIDs are globally unique and much harder to guess than sequential integers
                if (string.IsNullOrEmpty(user.Id))
                {
                    user.Id = Guid.NewGuid().ToString();
                }

                // Attempt to create the user in the database
                return await _databaseService.CreateUserAsync(user);
            }
            catch (Exception ex)
            {
                // Log the error for debugging
                System.Diagnostics.Debug.WriteLine($"Registration error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get all users in the system
        /// This is typically used by administrators to manage user accounts
        /// Only returns active users to keep the interface clean
        /// </summary>
        public async Task<List<User>> GetAllUsersAsync()
        {
            try
            {
                return await _databaseService.GetAllUsersAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting users: {ex.Message}");
                return new List<User>(); // Return empty list on error
            }
        }

        /// <summary>
        /// Get the currently logged-in user from secure storage
        /// This method reconstructs the user session after the app is reopened
        /// It uses MAUI's SecureStorage which encrypts data on the device
        /// </summary>
        public async Task<User?> GetCurrentUserAsync()
        {
            try
            {
                // Retrieve the stored user ID from secure storage
                // SecureStorage encrypts this data using the device's secure enclave
                var userId = await SecureStorage.GetAsync("current_user_id");

                if (!string.IsNullOrEmpty(userId))
                {
                    // Look up the full user record from the database
                    // We don't store the entire user object in SecureStorage because:
                    // 1. It's more secure to store just the ID
                    // 2. We get fresh data from the database (in case roles changed)
                    // 3. SecureStorage has size limitations
                    var user = await _databaseService.Users.FindAsync(userId);

                    // Verify the user is still active
                    // This handles cases where an admin might have deactivated an account
                    if (user != null && user.IsActive)
                    {
                        return user;
                    }
                }

                return null; // No valid session found
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting current user: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Store user session information securely on the device
        /// This allows the user to stay logged in between app launches
        /// We store multiple pieces of information for quick access without database queries
        /// </summary>
        public async Task SaveUserSessionAsync(User user)
        {
            try
            {
                // Store essential user information in secure storage
                // This creates a "session" that persists across app restarts
                await SecureStorage.SetAsync("current_user_id", user.Id);
                await SecureStorage.SetAsync("current_user_name", user.FullName);
                await SecureStorage.SetAsync("current_user_role", user.Role);
                await SecureStorage.SetAsync("current_username", user.Username);

                // Store the login timestamp for session management
                await SecureStorage.SetAsync("login_timestamp", DateTime.UtcNow.ToString());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving user session: {ex.Message}");
                // We don't throw here because login can still work without persistent session
            }
        }

        /// <summary>
        /// Check if the current user has permission to perform a specific action
        /// This is where you implement role-based access control (RBAC)
        /// Different roles have different capabilities in the system
        /// </summary>
        public async Task<bool> HasPermissionAsync(string permission)
        {
            var currentUser = await GetCurrentUserAsync();
            if (currentUser == null)
            {
                return false; // Not logged in, no permissions
            }

            // Define what each role can do
            // This is a simple implementation - you might want to use a more sophisticated system later
            return currentUser.Role switch
            {
                "Admin" => true, // Admins can do everything
                "Manager" => permission != "manage_users", // Managers can do most things but not user management
                "Operator" => permission == "scan_items" || permission == "view_inventory", // Operators have limited access
                _ => false // Unknown role, no permissions
            };
        }

        /// <summary>
        /// Log out the current user
        /// This clears all session data from secure storage
        /// Always call this when a user explicitly logs out or when session expires
        /// </summary>
        public async Task LogoutAsync()
        {
            try
            {
                // Clear all stored session data
                // SecureStorage.RemoveAll() clears everything, which is the safest approach
                SecureStorage.RemoveAll();

                // You might also want to record the logout in your audit log
                // This helps with security monitoring and compliance
                System.Diagnostics.Debug.WriteLine("User logged out successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during logout: {ex.Message}");
                // Even if there's an error, we still want to clear what we can
                try
                {
                    // Try to remove items individually if RemoveAll() failed
                    SecureStorage.Remove("current_user_id");
                    SecureStorage.Remove("current_user_name");
                    SecureStorage.Remove("current_user_role");
                    SecureStorage.Remove("current_username");
                    SecureStorage.Remove("login_timestamp");
                }
                catch
                {
                    // If even individual removal fails, there's not much we can do
                    // The user will need to reinstall the app in extreme cases
                }
            }
        }

        /// <summary>
        /// Check if the current session is still valid
        /// This helps implement automatic logout after a period of inactivity
        /// You might want to call this periodically or before sensitive operations
        /// </summary>
        public async Task<bool> IsSessionValidAsync()
        {
            try
            {
                var loginTimestampStr = await SecureStorage.GetAsync("login_timestamp");
                if (string.IsNullOrEmpty(loginTimestampStr))
                {
                    return false; // No login timestamp found
                }

                if (DateTime.TryParse(loginTimestampStr, out var loginTimestamp))
                {
                    // Check if login was within the last 30 days (configurable)
                    var sessionDuration = TimeSpan.FromDays(30);
                    return DateTime.UtcNow - loginTimestamp < sessionDuration;
                }

                return false; // Invalid timestamp format
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking session validity: {ex.Message}");
                return false; // Assume invalid on error
            }
        }
    }
}