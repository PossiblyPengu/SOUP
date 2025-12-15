using System.Windows;
using System.Windows.Controls;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Extensions.DependencyInjection;
using SAP.ViewModels;

namespace SAP.Views;

/// <summary>
/// External data connection settings view with admin protection
/// </summary>
public partial class ExternalDataSettingsView : UserControl
{
    public ExternalDataSettingsView()
    {
        InitializeComponent();
        DataContext = App.GetService<ExternalDataViewModel>();
        
        // Check if already running as admin - auto-unlock
        CheckInitialAdminState();
    }

    private void CheckInitialAdminState()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            if (principal.IsInRole(WindowsBuiltInRole.Administrator))
            {
                Serilog.Log.Information("App running as administrator, auto-unlocking External Data");
                Unlock();
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Could not check admin state");
        }
    }

    private async void UnlockButton_Click(object sender, RoutedEventArgs e)
    {
        UnlockButton.IsEnabled = false;
        try
        {
            var authenticated = await PromptForAdminCredentialsAsync();
            if (authenticated)
            {
                Unlock();
            }
        }
        finally
        {
            UnlockButton.IsEnabled = true;
        }
    }

    private void Unlock()
    {
        LockOverlay.Visibility = Visibility.Collapsed;
        ContentPanel.Opacity = 1.0;
        ContentPanel.IsEnabled = true;
    }

    /// <summary>
    /// Prompts for Windows administrator credentials using the Windows Credential UI.
    /// </summary>
    private async System.Threading.Tasks.Task<bool> PromptForAdminCredentialsAsync()
    {
        return await System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
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
                    Serilog.Log.Information("Admin credentials validated successfully for External Data access");
                }
                else
                {
                    Serilog.Log.Warning("Provided credentials are not for an administrator account");
                    Dispatcher.Invoke(() => MessageBox.Show(
                        Window.GetWindow(this),
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

    private const uint CREDUIWIN_ENUMERATE_ADMINS = 0x100;

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
