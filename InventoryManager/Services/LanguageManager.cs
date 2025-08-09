// Services/LanguageManager.cs
// Simple language manager with local storage and easy extensibility

using System.Collections.Generic;

namespace InventoryManager.Services
{
    /// <summary>
    /// Simple language manager that handles app translations
    /// Stores language preference locally and supports easy addition of new languages
    /// </summary>
    public class LanguageManager
    {
        private const string LANGUAGE_KEY = "app_language";
        private const string DEFAULT_LANGUAGE = "en";

        private string _currentLanguage;
        private Dictionary<string, Dictionary<string, string>> _translations;

        public string CurrentLanguage => _currentLanguage;

        // Event fired when language changes
        public event EventHandler<string>? LanguageChanged;

        public LanguageManager()
        {
            InitializeTranslations();
            _currentLanguage = Preferences.Get(LANGUAGE_KEY, DEFAULT_LANGUAGE);
        }

        /// <summary>
        /// Initialize all translations - easy to add more languages here
        /// </summary>
        private void InitializeTranslations()
        {
            _translations = new Dictionary<string, Dictionary<string, string>>();

            // English translations
            _translations["en"] = new Dictionary<string, string>
            {
                // App General
                ["AppName"] = "Inventory Manager",
                ["Welcome"] = "Welcome to InventoryManager",
                ["Loading"] = "Loading...",
                ["OK"] = "OK",
                ["Cancel"] = "Cancel",
                ["Yes"] = "Yes",
                ["No"] = "No",
                ["Save"] = "Save",
                ["Delete"] = "Delete",
                ["Edit"] = "Edit",
                ["Add"] = "Add",
                ["Search"] = "Search",
                ["Refresh"] = "Refresh",
                ["Error"] = "Error",
                ["Success"] = "Success",
                ["Warning"] = "Warning",
                ["Info"] = "Information",

                // Login/Authentication
                ["Login"] = "Login",
                ["Register"] = "Register",
                ["Username"] = "Username",
                ["Password"] = "Password",
                ["FullName"] = "Full Name",
                ["Role"] = "Role",
                ["WelcomeBack"] = "Welcome Back!",
                ["CreateNewAccount"] = "Create New Account",
                ["RegisterNewUser"] = "Register New User",
                ["LoggingIn"] = "Logging in...",
                ["CreatingAccount"] = "Creating account...",
                ["Logout"] = "Logout",
                ["AreYouSureLogout"] = "Are you sure you want to logout?",
                ["InvalidCredentials"] = "Invalid username or password",
                ["PleaseEnterCredentials"] = "Please enter both username and password",
                ["AccountCreatedSuccessfully"] = "Account created successfully! You can now log in.",
                ["DefaultAdminLogin"] = "Default admin login: admin / admin123",

                // Roles
                ["Admin"] = "Administrator",
                ["Manager"] = "Manager",
                ["Operator"] = "Operator",
                ["SelectRole"] = "Select Role",

                // Dashboard
                ["Dashboard"] = "Dashboard",
                ["WelcomeUser"] = "Welcome, {0}! 👋",
                ["CurrentTime"] = "Current Time: {0}",
                ["YourAccountInfo"] = "Your Account Information",
                ["DatabaseStatus"] = "Database Status",
                ["SystemStatistics"] = "System Statistics",
                ["QuickActions"] = "Quick Actions",
                ["TestDatabase"] = "Test Database",
                ["CreateTestUser"] = "Create Test User",
                ["AppInfo"] = "App Info",
                ["RefreshUserList"] = "Refresh User List",
                // Dashboard Reser Inventory
                ["ReserDB"] = "Reset Database",
                ["ResetDBWarning"] = "⚠️ WARNING: This will DELETE all inventory items and transactions, then create new test data.\n\nAre you sure you want to continue?",
                ["ReserDBSuccess"] = "✅ Inventory database has been reset with test data.\n\n• 11 test inventory items created\n• 3 sample transactions added\n• Various categories and locations\n• Some items are low on stock for testing\n\nDefault login: admin / admin123",
                ["ResetDBInProgress"] = "Resetting...",
                ["ReserDBConfirm"] = "This action cannot be undone. All inventory data will be lost.\n\nAre you REALLY sure?",
                ["ResetDBComplete"] = "Reset Complete",
                ["ResetDBFailed"] = "Failed to reset inventory database. Check debug output for details.",

                // Inventory
                ["Inventory"] = "Inventory",
                ["InventoryManagement"] = "Inventory Management",
                ["InventoryOverview"] = "Inventory Overview",
                ["TotalItems"] = "Total Items",
                ["LowStock"] = "Low Stock",
                ["TotalValue"] = "Total Value",
                ["SearchItems"] = "Search items...",
                ["Location"] = "Location",
                ["Category"] = "Category",
                ["LowStockOnly"] = "Low Stock Only",
                ["AllLocations"] = "All Locations",
                ["AllCategories"] = "All Categories",

                // Inventory Table Headers
                ["Item"] = "Item",
                ["Code"] = "Code",
                ["Quantity"] = "Quantity",
                ["MinMax"] = "Min/Max",
                ["Status"] = "Status",
                ["Minimum"] = "Minimum",
                ["Maximum"] = "Maximum",

                // Add Item
                ["AddNewItem"] = "Add New Item",
                ["ItemID"] = "Item ID / Code",
                ["ItemName"] = "Item Name",
                ["Description"] = "Description",
                ["InitialQuantity"] = "Initial Quantity",
                ["Unit"] = "Unit",
                ["StockLevels"] = "Stock Levels",
                ["MinStock"] = "Min stock",
                ["MaxStock"] = "Max stock",
                ["SelectLocation"] = "Select location",
                ["Supplier"] = "Supplier",
                ["UnitCost"] = "Unit Cost ($)",
                ["AdditionalDetails"] = "Additional Details",
                ["RequiredFields"] = "* Required fields",
                ["AddItem"] = "Add Item",
                ["Suggest"] = "Suggest",
                ["FillInDetails"] = "Fill in the details for the new inventory item",
                ["PleaseFixIssues"] = "Please fix the following issues:",

                // Units
                ["pieces"] = "pieces",
                ["boxes"] = "boxes",
                ["sets"] = "sets",
                ["rolls"] = "rolls",
                ["sheets"] = "sheets",
                ["liters"] = "liters",
                ["kilograms"] = "kilograms",
                ["meters"] = "meters",
                ["packs"] = "packs",

                // Locations
                ["MainWarehouse"] = "Main Warehouse",
                ["ColdStorage"] = "Cold Storage",
                ["LoadingDock"] = "Loading Dock",
                ["Workshop"] = "Workshop",
                ["OfficeStorage"] = "Office Storage",
                ["OutdoorYard"] = "Outdoor Yard",
                ["SafetyStation"] = "Safety Station",
                ["ChemicalStorage"] = "Chemical Storage",

                // Messages
                ["NoInventoryItemsFound"] = "No inventory items found",
                ["TryAdjustingFilters"] = "Try adjusting your filters",
                ["QuantityUpdatedSuccessfully"] = "Quantity updated successfully",
                ["FailedToUpdateQuantity"] = "Failed to update quantity",
                ["InvalidQuantity"] = "Please enter a valid non-negative number",
                ["AccessDenied"] = "Access Denied",
                ["OnlyAdminsCanAdd"] = "Only administrators can add inventory items",
                ["DatabaseReady"] = "Database ready",
                ["DatabaseError"] = "Database error: {0}",

                // Settings
                ["Settings"] = "Settings",
                ["Language"] = "Language",
                ["ChooseLanguage"] = "Choose Language",
                ["LanguageChangedMessage"] = "Language has been changed. Some texts may require app restart to update."
            };

            // Romanian translations
            _translations["ro"] = new Dictionary<string, string>
            {
                // App General
                ["AppName"] = "Manager de Inventar",
                ["Welcome"] = "Bine ați venit la InventoryManager",
                ["Loading"] = "Se încarcă...",
                ["OK"] = "OK",
                ["Cancel"] = "Anulare",
                ["Yes"] = "Da",
                ["No"] = "Nu",
                ["Save"] = "Salvare",
                ["Delete"] = "Ștergere",
                ["Edit"] = "Editare",
                ["Add"] = "Adăugare",
                ["Search"] = "Căutare",
                ["Refresh"] = "Reîmprospătare",
                ["Error"] = "Eroare",
                ["Success"] = "Succes",
                ["Warning"] = "Avertisment",
                ["Info"] = "Informație",

                // Login/Authentication
                ["Login"] = "Autentificare",
                ["Register"] = "Înregistrare",
                ["Username"] = "Nume utilizator",
                ["Password"] = "Parolă",
                ["FullName"] = "Nume complet",
                ["Role"] = "Rol",
                ["WelcomeBack"] = "Bine ați revenit!",
                ["CreateNewAccount"] = "Creați cont nou",
                ["RegisterNewUser"] = "Înregistrare utilizator nou",
                ["LoggingIn"] = "Se autentifică...",
                ["CreatingAccount"] = "Se creează contul...",
                ["Logout"] = "Deconectare",
                ["AreYouSureLogout"] = "Sigur doriți să vă deconectați?",
                ["InvalidCredentials"] = "Nume de utilizator sau parolă incorectă",
                ["PleaseEnterCredentials"] = "Vă rugăm introduceți numele de utilizator și parola",
                ["AccountCreatedSuccessfully"] = "Cont creat cu succes! Vă puteți conecta acum.",
                ["DefaultAdminLogin"] = "Autentificare admin implicită: admin / admin123",

                // Roles
                ["Admin"] = "Administrator",
                ["Manager"] = "Manager",
                ["Operator"] = "Operator",
                ["SelectRole"] = "Selectați rolul",

                // Dashboard
                ["Dashboard"] = "Tablou de bord",
                ["WelcomeUser"] = "Bine ați venit, {0}! 👋",
                ["CurrentTime"] = "Ora curentă: {0}",
                ["YourAccountInfo"] = "Informațiile contului dvs.",
                ["DatabaseStatus"] = "Starea bazei de date",
                ["SystemStatistics"] = "Statistici sistem",
                ["QuickActions"] = "Acțiuni rapide",
                ["TestDatabase"] = "Test bază de date",
                ["CreateTestUser"] = "Creați utilizator test",
                ["AppInfo"] = "Informații aplicație",
                ["RefreshUserList"] = "Reîmprospătare listă utilizatori",

                // Inventory
                ["Inventory"] = "Inventar",
                ["InventoryManagement"] = "Gestionare inventar",
                ["InventoryOverview"] = "Prezentare generală inventar",
                ["TotalItems"] = "Total articole",
                ["LowStock"] = "Stoc scăzut",
                ["TotalValue"] = "Valoare totală",
                ["SearchItems"] = "Căutați articole...",
                ["Location"] = "Locație",
                ["Category"] = "Categorie",
                ["LowStockOnly"] = "Doar stoc scăzut",
                ["AllLocations"] = "Toate locațiile",
                ["AllCategories"] = "Toate categoriile",

                // Inventory Table Headers
                ["Item"] = "Articol",
                ["Code"] = "Cod",
                ["Quantity"] = "Cantitate",
                ["MinMax"] = "Min/Max",
                ["Status"] = "Stare",
                ["Minimum"] = "Minim",
                ["Maximum"] = "Maxim",

                // Add Item
                ["AddNewItem"] = "Adăugați articol nou",
                ["ItemID"] = "ID / Cod articol",
                ["ItemName"] = "Nume articol",
                ["Description"] = "Descriere",
                ["InitialQuantity"] = "Cantitate inițială",
                ["Unit"] = "Unitate",
                ["StockLevels"] = "Niveluri stoc",
                ["MinStock"] = "Stoc minim",
                ["MaxStock"] = "Stoc maxim",
                ["SelectLocation"] = "Selectați locația",
                ["Supplier"] = "Furnizor",
                ["UnitCost"] = "Cost unitar ($)",
                ["AdditionalDetails"] = "Detalii suplimentare",
                ["RequiredFields"] = "* Câmpuri obligatorii",
                ["AddItem"] = "Adaugă articol",
                ["Suggest"] = "Sugerează",
                ["FillInDetails"] = "Completați detaliile pentru noul articol de inventar",
                ["PleaseFixIssues"] = "Vă rugăm corectați următoarele probleme:",

                // Units
                ["pieces"] = "bucăți",
                ["boxes"] = "cutii",
                ["sets"] = "seturi",
                ["rolls"] = "role",
                ["sheets"] = "foi",
                ["liters"] = "litri",
                ["kilograms"] = "kilograme",
                ["meters"] = "metri",
                ["packs"] = "pachete",

                // Locations
                ["MainWarehouse"] = "Depozit principal",
                ["ColdStorage"] = "Depozit frigorific",
                ["LoadingDock"] = "Rampă de încărcare",
                ["Workshop"] = "Atelier",
                ["OfficeStorage"] = "Depozit birou",
                ["OutdoorYard"] = "Curte exterioară",
                ["SafetyStation"] = "Stație de siguranță",
                ["ChemicalStorage"] = "Depozit chimice",

                // Messages
                ["NoInventoryItemsFound"] = "Nu s-au găsit articole de inventar",
                ["TryAdjustingFilters"] = "Încercați să ajustați filtrele",
                ["QuantityUpdatedSuccessfully"] = "Cantitate actualizată cu succes",
                ["FailedToUpdateQuantity"] = "Actualizarea cantității a eșuat",
                ["InvalidQuantity"] = "Vă rugăm introduceți un număr valid nenegativ",
                ["AccessDenied"] = "Acces refuzat",
                ["OnlyAdminsCanAdd"] = "Doar administratorii pot adăuga articole de inventar",
                ["DatabaseReady"] = "Baza de date pregătită",
                ["DatabaseError"] = "Eroare bază de date: {0}",

                // Settings
                ["Settings"] = "Setări",
                ["Language"] = "Limbă",
                ["ChooseLanguage"] = "Alegeți limba",
                ["LanguageChangedMessage"] = "Limba a fost schimbată. Unele texte pot necesita repornirea aplicației pentru actualizare."
            };

            // TO ADD MORE LANGUAGES:
            // 1. Copy the entire dictionary structure above
            // 2. Change the key from "en" or "ro" to your language code (e.g., "de", "fr", "es")
            // 3. Translate all the values
            // Example:
            // _translations["de"] = new Dictionary<string, string> { ... };
        }

        /// <summary>
        /// Get a translated string for the current language
        /// </summary>
        public string Get(string key)
        {
            if (_translations.ContainsKey(_currentLanguage) &&
                _translations[_currentLanguage].ContainsKey(key))
            {
                return _translations[_currentLanguage][key];
            }

            // Fallback to English if translation not found
            if (_translations["en"].ContainsKey(key))
            {
                return _translations["en"][key];
            }

            // Return the key itself if no translation found
            return key;
        }

        /// <summary>
        /// Get a formatted translated string
        /// </summary>
        public string Get(string key, params object[] args)
        {
            var translation = Get(key);
            return string.Format(translation, args);
        }

        /// <summary>
        /// Change the app language
        /// </summary>
        public void SetLanguage(string languageCode)
        {
            if (!_translations.ContainsKey(languageCode))
            {
                languageCode = DEFAULT_LANGUAGE;
            }

            _currentLanguage = languageCode;
            Preferences.Set(LANGUAGE_KEY, languageCode);

            // Fire event to notify UI to update
            LanguageChanged?.Invoke(this, languageCode);
        }

        /// <summary>
        /// Get list of available languages
        /// </summary>
        public List<LanguageInfo> GetAvailableLanguages()
        {
            return new List<LanguageInfo>
            {
                new LanguageInfo { Code = "en", Name = "English", NativeName = "English", Flag = "🇬🇧" },
                new LanguageInfo { Code = "ro", Name = "Romanian", NativeName = "Română", Flag = "🇷🇴" }
                // TO ADD MORE LANGUAGES:
                // Add them here with their info
                // new LanguageInfo { Code = "de", Name = "German", NativeName = "Deutsch", Flag = "🇩🇪" },
            };
        }

        /// <summary>
        /// Toggle between English and Romanian (or cycle through all languages)
        /// </summary>
        public void ToggleLanguage()
        {
            var languages = GetAvailableLanguages();
            var currentIndex = languages.FindIndex(l => l.Code == _currentLanguage);
            var nextIndex = (currentIndex + 1) % languages.Count;
            SetLanguage(languages[nextIndex].Code);
        }
    }

    /// <summary>
    /// Information about a language
    /// </summary>
    public class LanguageInfo
    {
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string NativeName { get; set; } = "";
        public string Flag { get; set; } = "";
    }

    /// <summary>
    /// Static helper class for easy access to translations
    /// </summary>
    public static class L
    {
        private static LanguageManager? _languageManager;

        public static void Initialize(LanguageManager languageManager)
        {
            _languageManager = languageManager;
        }

        public static string Get(string key)
        {
            return _languageManager?.Get(key) ?? key;
        }

        public static string Get(string key, params object[] args)
        {
            return _languageManager?.Get(key, args) ?? key;
        }
    }
}