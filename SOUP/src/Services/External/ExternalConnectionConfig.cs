using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SOUP.Services.External;

/// <summary>
/// Configuration for external data connections (MySQL and Business Central).
/// Sensitive fields (passwords, secrets) are encrypted using Windows DPAPI.
/// </summary>
public class ExternalConnectionConfig
{
    private static readonly string ConfigPath = Path.Combine(
        Core.AppPaths.AppData,
        "external_config.json"
    );

    // In-memory secure values - not serialized
    [JsonIgnore]
    private System.Security.SecureString? _mySqlPasswordSecure;
    [JsonIgnore]
    private System.Security.SecureString? _bcClientSecretSecure;

    #region MySQL Configuration

    /// <summary>
    /// MySQL server hostname or IP
    /// </summary>
    public string MySqlServer { get; set; } = "";

    /// <summary>
    /// MySQL database name
    /// </summary>
    public string MySqlDatabase { get; set; } = "";

    /// <summary>
    /// MySQL username
    /// </summary>
    public string MySqlUser { get; set; } = "";

    /// <summary>
    /// MySQL password (encrypted with DPAPI, base64 encoded for storage)
    /// </summary>
    public string MySqlPasswordEncrypted { get; set; } = "";

    /// <summary>
    /// MySQL password (decrypted, in-memory only - not serialized)
    /// </summary>
    [JsonIgnore]
    public string MySqlPassword
    {
        get
        {
            if (_mySqlPasswordSecure == null) return string.Empty;
            var ptr = IntPtr.Zero;
            try
            {
                ptr = System.Runtime.InteropServices.Marshal.SecureStringToGlobalAllocUnicode(_mySqlPasswordSecure);
                return System.Runtime.InteropServices.Marshal.PtrToStringUni(ptr) ?? string.Empty;
            }
            finally
            {
                if (ptr != IntPtr.Zero)
                    System.Runtime.InteropServices.Marshal.ZeroFreeGlobalAllocUnicode(ptr);
            }
        }
        set
        {
            // Store securely and update encrypted storage
            _mySqlPasswordSecure = new System.Security.SecureString();
            if (!string.IsNullOrEmpty(value))
            {
                foreach (var c in value)
                    _mySqlPasswordSecure.AppendChar(c);
                _mySqlPasswordSecure.MakeReadOnly();
            }
            MySqlPasswordEncrypted = EncryptString(value ?? string.Empty);
        }
    }

    /// <summary>
    /// MySQL port (default 3306)
    /// </summary>
    public int MySqlPort { get; set; } = 3306;

    #endregion

    private static System.Security.SecureString StringToSecureString(string? plain)
    {
        var ss = new System.Security.SecureString();
        if (string.IsNullOrEmpty(plain)) return ss;
        foreach (var c in plain)
            ss.AppendChar(c);
        ss.MakeReadOnly();
        return ss;
    }

    private static string SecureStringToString(System.Security.SecureString? ss)
    {
        if (ss == null || ss.Length == 0) return string.Empty;
        var ptr = IntPtr.Zero;
        try
        {
            ptr = System.Runtime.InteropServices.Marshal.SecureStringToGlobalAllocUnicode(ss);
            return System.Runtime.InteropServices.Marshal.PtrToStringUni(ptr) ?? string.Empty;
        }
        finally
        {
            if (ptr != IntPtr.Zero)
                System.Runtime.InteropServices.Marshal.ZeroFreeGlobalAllocUnicode(ptr);
        }
    }

    #region Business Central Configuration

    /// <summary>
    /// Azure AD Tenant ID for BC authentication
    /// </summary>
    public string BcTenantId { get; set; } = "";

    /// <summary>
    /// OAuth Client ID for BC API access
    /// </summary>
    public string BcClientId { get; set; } = "";

    /// <summary>
    /// OAuth Client Secret (encrypted with DPAPI, base64 encoded for storage)
    /// </summary>
    public string BcClientSecretEncrypted { get; set; } = "";

    /// <summary>
    /// OAuth Client Secret (decrypted, in-memory only - not serialized)
    /// </summary>
    [JsonIgnore]
    public string BcClientSecret
    {
        get
        {
            if (_bcClientSecretSecure == null) return string.Empty;
            var ptr = IntPtr.Zero;
            try
            {
                ptr = System.Runtime.InteropServices.Marshal.SecureStringToGlobalAllocUnicode(_bcClientSecretSecure);
                return System.Runtime.InteropServices.Marshal.PtrToStringUni(ptr) ?? string.Empty;
            }
            finally
            {
                if (ptr != IntPtr.Zero)
                    System.Runtime.InteropServices.Marshal.ZeroFreeGlobalAllocUnicode(ptr);
            }
        }
        set
        {
            _bcClientSecretSecure = new System.Security.SecureString();
            if (!string.IsNullOrEmpty(value))
            {
                foreach (var c in value)
                    _bcClientSecretSecure.AppendChar(c);
                _bcClientSecretSecure.MakeReadOnly();
            }
            BcClientSecretEncrypted = EncryptString(value ?? string.Empty);
        }
    }

    /// <summary>
    /// Business Central environment name (e.g., "Production", "Sandbox")
    /// </summary>
    public string BcEnvironment { get; set; } = "Production";

    /// <summary>
    /// Business Central company ID
    /// </summary>
    public string BcCompanyId { get; set; } = "";

    /// <summary>
    /// Business Central API base URL
    /// </summary>
    public string BcBaseUrl { get; set; } = "";

    #endregion

    #region Sync Settings

    /// <summary>
    /// Whether to auto-sync on startup
    /// </summary>
    public bool AutoSyncOnStartup { get; set; } = false;

    /// <summary>
    /// Last successful sync timestamp
    /// </summary>
    public DateTime? LastSyncTime { get; set; }

    /// <summary>
    /// Sync interval in hours (0 = manual only)
    /// </summary>
    public int SyncIntervalHours { get; set; } = 0;

    #endregion

    /// <summary>
    /// Build MySQL connection string
    /// </summary>
    public string GetMySqlConnectionString()
    {
        var pwd = MySqlPassword; // transient
        return $"Server={MySqlServer};Port={MySqlPort};Database={MySqlDatabase};User={MySqlUser};Password={pwd};";
    }

    /// <summary>
    /// Check if MySQL is configured
    /// </summary>
    public bool IsMySqlConfigured =>
        !string.IsNullOrWhiteSpace(MySqlServer) &&
        !string.IsNullOrWhiteSpace(MySqlDatabase) &&
        !string.IsNullOrWhiteSpace(MySqlUser);

    /// <summary>
    /// Check if Business Central is configured
    /// </summary>
    public bool IsBusinessCentralConfigured =>
        !string.IsNullOrWhiteSpace(BcTenantId) &&
        !string.IsNullOrWhiteSpace(BcClientId) &&
        !string.IsNullOrWhiteSpace(BcClientSecretEncrypted);

    #region DPAPI Encryption Helpers

    /// <summary>
    /// Encrypt a string using Windows DPAPI (current user scope)
    /// </summary>
    private static string EncryptString(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return "";

        try
        {
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var encryptedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encryptedBytes);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Failed to encrypt sensitive data");
            return "";
        }
    }

    /// <summary>
    /// Decrypt a string using Windows DPAPI (current user scope)
    /// </summary>
    private static string DecryptString(string encryptedBase64)
    {
        if (string.IsNullOrEmpty(encryptedBase64))
            return "";

        try
        {
            var encryptedBytes = Convert.FromBase64String(encryptedBase64);
            var plainBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Failed to decrypt sensitive data (may be from different user)");
            return "";
        }
    }

    #endregion

    /// <summary>
    /// Load configuration from file
    /// </summary>
    public static ExternalConnectionConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var config = JsonSerializer.Deserialize<ExternalConnectionConfig>(json) ?? new();

                // Decrypt sensitive fields into SecureString after loading (avoid long-lived plaintext)
                var mypwd = DecryptString(config.MySqlPasswordEncrypted);
                if (!string.IsNullOrEmpty(mypwd))
                {
                    config._mySqlPasswordSecure = new System.Security.SecureString();
                    foreach (var c in mypwd)
                        config._mySqlPasswordSecure.AppendChar(c);
                    config._mySqlPasswordSecure.MakeReadOnly();
                }

                var bcsecret = DecryptString(config.BcClientSecretEncrypted);
                if (!string.IsNullOrEmpty(bcsecret))
                {
                    config._bcClientSecretSecure = new System.Security.SecureString();
                    foreach (var c in bcsecret)
                        config._bcClientSecretSecure.AppendChar(c);
                    config._bcClientSecretSecure.MakeReadOnly();
                }

                // Migration: if old plaintext fields exist, encrypt them
                MigrateOldConfig(config, json);

                return config;
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Failed to load external connection config, using defaults");
        }
        return new();
    }

    /// <summary>
    /// Migrate old plaintext config to encrypted format
    /// </summary>
    private static void MigrateOldConfig(ExternalConnectionConfig config, string json)
    {
        try
        {
            // Check if old plaintext fields exist in the JSON
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Migrate MySqlPassword if present and encrypted version is empty
            if (root.TryGetProperty("MySqlPassword", out var oldPwd) &&
                string.IsNullOrEmpty(config.MySqlPasswordEncrypted))
            {
                var plainPwd = oldPwd.GetString();
                if (!string.IsNullOrEmpty(plainPwd))
                {
                    config.MySqlPassword = plainPwd; // This encrypts it
                    Serilog.Log.Information("Migrated MySQL password to encrypted storage");
                }
            }

            // Migrate BcClientSecret if present and encrypted version is empty
            if (root.TryGetProperty("BcClientSecret", out var oldSecret) &&
                string.IsNullOrEmpty(config.BcClientSecretEncrypted))
            {
                var plainSecret = oldSecret.GetString();
                if (!string.IsNullOrEmpty(plainSecret))
                {
                    config.BcClientSecret = plainSecret; // This encrypts it
                    Serilog.Log.Information("Migrated BC client secret to encrypted storage");
                }
            }

            // Save migrated config
            if (root.TryGetProperty("MySqlPassword", out _) || root.TryGetProperty("BcClientSecret", out _))
            {
                config.Save();
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Failed to migrate old config format");
        }
    }

    /// <summary>
    /// Save configuration to file
    /// </summary>
    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Failed to save external connection config");
            throw;
        }
    }
}
