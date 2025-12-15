using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SAP.ViewModels;
using SAP.Windows;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace SAP.Views;

public partial class UnifiedSettingsWindow : Window
{
    private readonly UnifiedSettingsViewModel _viewModel;
    private bool _isExternalDataUnlocked = false;
    private RadioButton? _previousTab;

    public UnifiedSettingsWindow(UnifiedSettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        
        // Fire-and-forget initialization to avoid blocking window opening
        Loaded += (s, e) => _ = InitializeViewModelAsync();
    }

    private async System.Threading.Tasks.Task InitializeViewModelAsync()
    {
        try
        {
            await _viewModel.InitializeAsync().ConfigureAwait(false);
        }
        catch (System.Exception ex)
        {
            Serilog.Log.Error(ex, "Error initializing settings");
        }
    }

    /// <summary>
    /// Handles title bar dragging for borderless window
    /// </summary>
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            // Double-click doesn't maximize for dialog windows
            return;
        }
        DragMove();
    }

    /// <summary>
    /// Closes the window
    /// </summary>
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    /// <summary>
    /// Opens the About dialog
    /// </summary>
    private void OnAboutClick(object sender, RoutedEventArgs e)
    {
        var aboutWindow = new AboutWindow
        {
            Owner = this
        };
        aboutWindow.ShowDialog();
    }

    /// <summary>
    /// Handle tab navigation with the new RadioButton-based tabs
    /// </summary>
    private async void OnTabChanged(object sender, RoutedEventArgs e)
    {
        try
        {
            // Hide all panels
            PanelAllocation.Visibility = Visibility.Collapsed;
            PanelEssentials.Visibility = Visibility.Collapsed;
            PanelExpireWise.Visibility = Visibility.Collapsed;
            PanelDictionary.Visibility = Visibility.Collapsed;
            PanelExternalData.Visibility = Visibility.Collapsed;

            // Show the selected panel
            if (TabAllocation.IsChecked == true)
            {
                PanelAllocation.Visibility = Visibility.Visible;
            }
            else if (TabEssentials.IsChecked == true)
            {
                PanelEssentials.Visibility = Visibility.Visible;
            }
            else if (TabExpireWise.IsChecked == true)
            {
                PanelExpireWise.Visibility = Visibility.Visible;
            }
            else if (TabDictionary.IsChecked == true)
            {
                PanelDictionary.Visibility = Visibility.Visible;
                
                // Load dictionary lazily
                if (!_viewModel.DictionaryManagement.IsInitialized && !_viewModel.DictionaryManagement.IsLoading)
                {
                    Serilog.Log.Information("Dictionary tab selected, loading dictionary...");
                    await _viewModel.DictionaryManagement.LoadDictionaryAsync();
                }
            }
            else if (TabExternalData.IsChecked == true)
            {
                // Require Windows admin credentials to access External Data settings
                if (!_isExternalDataUnlocked)
                {
                    var authenticated = await PromptForAdminCredentialsAsync();
                    if (!authenticated)
                    {
                        // Revert to previous tab
                        TabExternalData.IsChecked = false;
                        if (_previousTab != null)
                        {
                            _previousTab.IsChecked = true;
                        }
                        else
                        {
                            TabAllocation.IsChecked = true;
                            PanelAllocation.Visibility = Visibility.Visible;
                        }
                        return;
                    }
                    _isExternalDataUnlocked = true;
                }
                PanelExternalData.Visibility = Visibility.Visible;
            }
            
            // Track previous tab for reverting on auth failure
            if (TabAllocation.IsChecked == true) _previousTab = TabAllocation;
            else if (TabEssentials.IsChecked == true) _previousTab = TabEssentials;
            else if (TabExpireWise.IsChecked == true) _previousTab = TabExpireWise;
            else if (TabDictionary.IsChecked == true) _previousTab = TabDictionary;
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Failed to handle tab change");
        }
    }

    /// <summary>
    /// Handle tab selection to load dictionary lazily when Dictionary Management tab is selected
    /// </summary>
    internal async void OnTabSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (sender is TabControl tabControl && 
                tabControl.SelectedItem is TabItem selectedTab)
            {
                var tabHeader = selectedTab.Header?.ToString() ?? "(null)";
                Serilog.Log.Information("Tab selected: {TabHeader}", tabHeader);
                
                if (tabHeader.Contains("Dictionary"))
                {
                    Serilog.Log.Information("Dictionary Management tab selected. IsInitialized={IsInit}, IsLoading={IsLoading}", 
                        _viewModel.DictionaryManagement.IsInitialized, 
                        _viewModel.DictionaryManagement.IsLoading);
                        
                    // Load dictionary only when the tab is selected and not already initialized
                    if (!_viewModel.DictionaryManagement.IsInitialized && !_viewModel.DictionaryManagement.IsLoading)
                    {
                        Serilog.Log.Information("Calling LoadDictionaryAsync...");
                        await _viewModel.DictionaryManagement.LoadDictionaryAsync();
                        Serilog.Log.Information("LoadDictionaryAsync completed. FilteredItems.Count={Count}", 
                            _viewModel.DictionaryManagement.FilteredItems?.Count ?? -1);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Failed to handle tab selection");
        }
    }

    /// <summary>
    /// Prompts for Windows administrator credentials using the Windows Credential UI.
    /// </summary>
    /// <returns>True if the user successfully authenticated as an administrator.</returns>
    private async System.Threading.Tasks.Task<bool> PromptForAdminCredentialsAsync()
    {
        return await System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                // Check if already running as admin
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                if (principal.IsInRole(WindowsBuiltInRole.Administrator))
                {
                    Serilog.Log.Information("User is already running as administrator");
                    return true;
                }

                // Prompt for admin credentials using Windows Credential UI
                var credResult = PromptForWindowsCredentials();
                if (!credResult.Success)
                {
                    Serilog.Log.Information("User cancelled admin credential prompt");
                    return false;
                }

                // Validate credentials belong to an admin
                bool isAdmin = ValidateAdminCredentials(credResult.Username, credResult.Password, credResult.Domain);
                
                // Clear password from memory
                credResult.Password = null;
                
                if (isAdmin)
                {
                    Serilog.Log.Information("Admin credentials validated successfully");
                }
                else
                {
                    Serilog.Log.Warning("Provided credentials are not for an administrator account");
                    Dispatcher.Invoke(() => MessageBox.Show(this, 
                        "The provided credentials do not have administrator privileges.", 
                        "Access Denied", 
                        MessageBoxButton.OK, 
                        MessageBoxImage.Warning));
                }
                
                return isAdmin;
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Error prompting for admin credentials");
                return false;
            }
        });
    }

    #region Windows Credential UI P/Invoke

    [DllImport("credui.dll", CharSet = CharSet.Unicode)]
    private static extern int CredUIPromptForWindowsCredentials(
        ref CREDUI_INFO pUiInfo,
        int dwAuthError,
        ref uint pulAuthPackage,
        IntPtr pvInAuthBuffer,
        uint ulInAuthBufferSize,
        out IntPtr ppvOutAuthBuffer,
        out uint pulOutAuthBufferSize,
        ref bool pfSave,
        uint dwFlags);

    [DllImport("credui.dll", CharSet = CharSet.Unicode)]
    private static extern bool CredUnPackAuthenticationBuffer(
        uint dwFlags,
        IntPtr pAuthBuffer,
        uint cbAuthBuffer,
        System.Text.StringBuilder pszUserName,
        ref uint pcchMaxUserName,
        System.Text.StringBuilder pszDomainName,
        ref uint pcchMaxDomainName,
        System.Text.StringBuilder pszPassword,
        ref uint pcchMaxPassword);

    [DllImport("ole32.dll")]
    private static extern void CoTaskMemFree(IntPtr pv);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CREDUI_INFO
    {
        public int cbSize;
        public IntPtr hwndParent;
        public string pszMessageText;
        public string pszCaptionText;
        public IntPtr hbmBanner;
    }

    private const uint CREDUIWIN_GENERIC = 0x1;
    private const uint CREDUIWIN_CHECKBOX = 0x2;
    private const uint CREDUIWIN_AUTHPACKAGE_ONLY = 0x10;
    private const uint CREDUIWIN_IN_CRED_ONLY = 0x20;
    private const uint CREDUIWIN_ENUMERATE_ADMINS = 0x100;
    private const uint CREDUIWIN_ENUMERATE_CURRENT_USER = 0x200;

    private class CredentialResult
    {
        public bool Success { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? Domain { get; set; }
    }

    private CredentialResult PromptForWindowsCredentials()
    {
        var result = new CredentialResult();
        
        var credui = new CREDUI_INFO
        {
            cbSize = Marshal.SizeOf(typeof(CREDUI_INFO)),
            hwndParent = IntPtr.Zero,
            pszCaptionText = "Administrator Authorization Required",
            pszMessageText = "Enter administrator credentials to access External Data settings.\nThis protects sensitive connection information."
        };

        uint authPackage = 0;
        bool save = false;
        
        int credResult = CredUIPromptForWindowsCredentials(
            ref credui,
            0,
            ref authPackage,
            IntPtr.Zero,
            0,
            out IntPtr outCredBuffer,
            out uint outCredSize,
            ref save,
            CREDUIWIN_ENUMERATE_ADMINS);

        if (credResult != 0)
        {
            return result; // User cancelled or error
        }

        var usernameBuf = new System.Text.StringBuilder(256);
        var domainBuf = new System.Text.StringBuilder(256);
        var passwordBuf = new System.Text.StringBuilder(256);
        uint usernameLen = 256, domainLen = 256, passwordLen = 256;

        if (CredUnPackAuthenticationBuffer(0, outCredBuffer, outCredSize,
            usernameBuf, ref usernameLen,
            domainBuf, ref domainLen,
            passwordBuf, ref passwordLen))
        {
            result.Success = true;
            result.Username = usernameBuf.ToString();
            result.Domain = domainBuf.ToString();
            result.Password = passwordBuf.ToString();
        }

        CoTaskMemFree(outCredBuffer);
        return result;
    }

    private bool ValidateAdminCredentials(string? username, string? password, string? domain)
    {
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            return false;

        try
        {
            // Use LogonUser to validate credentials
            bool logonSuccess = LogonUser(
                username,
                string.IsNullOrEmpty(domain) ? "." : domain,
                password,
                LOGON32_LOGON_NETWORK,
                LOGON32_PROVIDER_DEFAULT,
                out IntPtr token);

            if (!logonSuccess)
            {
                Serilog.Log.Warning("LogonUser failed: {Error}", Marshal.GetLastWin32Error());
                return false;
            }

            try
            {
                // Check if the user is in the Administrators group
                using var identity = new WindowsIdentity(token);
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            finally
            {
                CloseHandle(token);
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Error validating admin credentials");
            return false;
        }
    }

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool LogonUser(
        string lpszUsername,
        string lpszDomain,
        string lpszPassword,
        int dwLogonType,
        int dwLogonProvider,
        out IntPtr phToken);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    private const int LOGON32_LOGON_NETWORK = 3;
    private const int LOGON32_PROVIDER_DEFAULT = 0;

    #endregion
}
