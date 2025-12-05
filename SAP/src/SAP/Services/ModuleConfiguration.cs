using System;
using System.IO;
using Serilog;

namespace SAP.Services;

/// <summary>
/// Manages module enable/disable configuration based on installer selections.
/// </summary>
/// <remarks>
/// <para>
/// This singleton service reads and writes module configuration from
/// <c>%APPDATA%\SAP\modules.ini</c>. The configuration controls which
/// modules are available in the application launcher.
/// </para>
/// <para>
/// If no configuration file exists (development mode or portable install),
/// all modules are enabled by default.
/// </para>
/// </remarks>
public class ModuleConfiguration
{
    private static readonly Lazy<ModuleConfiguration> _instance = new(() => new ModuleConfiguration());
    
    /// <summary>
    /// Gets the singleton instance of the module configuration service.
    /// </summary>
    public static ModuleConfiguration Instance => _instance.Value;
    
    /// <summary>
    /// Gets whether the AllocationBuddy module is enabled.
    /// </summary>
    public bool AllocationBuddyEnabled { get; private set; } = true;
    
    /// <summary>
    /// Gets whether the EssentialsBuddy module is enabled.
    /// </summary>
    public bool EssentialsBuddyEnabled { get; private set; } = true;
    
    /// <summary>
    /// Gets whether the ExpireWise module is enabled.
    /// </summary>
    public bool ExpireWiseEnabled { get; private set; } = true;
    
    /// <summary>
    /// Gets the installed version from the configuration.
    /// </summary>
    public string? InstalledVersion { get; private set; }
    
    /// <summary>
    /// Gets the installation date from the configuration.
    /// </summary>
    public string? InstallDate { get; private set; }
    
    private readonly string _configPath;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="ModuleConfiguration"/> class.
    /// </summary>
    private ModuleConfiguration()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _configPath = Path.Combine(appDataPath, "SAP", "modules.ini");
        
        LoadConfiguration();
    }
    
    /// <summary>
    /// Loads the configuration from the INI file.
    /// </summary>
    private void LoadConfiguration()
    {
        if (!File.Exists(_configPath))
        {
            // No config file = all modules enabled (development mode or portable install)
            return;
        }
        
        try
        {
            var lines = File.ReadAllLines(_configPath);
            string? currentSection = null;
            
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                
                // Skip empty lines and comments
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith(";") || trimmed.StartsWith("#"))
                    continue;
                
                // Section header
                if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                {
                    currentSection = trimmed[1..^1];
                    continue;
                }
                
                // Key=Value
                var equalIndex = trimmed.IndexOf('=');
                if (equalIndex > 0)
                {
                    var key = trimmed[..equalIndex].Trim();
                    var value = trimmed[(equalIndex + 1)..].Trim();
                    
                    if (currentSection == "Modules")
                    {
                        switch (key)
                        {
                            case "AllocationBuddy":
                                AllocationBuddyEnabled = ParseBool(value, true);
                                break;
                            case "EssentialsBuddy":
                                EssentialsBuddyEnabled = ParseBool(value, true);
                                break;
                            case "ExpireWise":
                                ExpireWiseEnabled = ParseBool(value, true);
                                break;
                        }
                    }
                    else if (currentSection == "Info")
                    {
                        switch (key)
                        {
                            case "InstalledVersion":
                                InstalledVersion = value;
                                break;
                            case "InstallDate":
                                InstallDate = value;
                                break;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // If there's any error reading config, enable all modules and log the error
            Log.Warning(ex, "Failed to load module configuration from {Path}, enabling all modules", _configPath);
            AllocationBuddyEnabled = true;
            EssentialsBuddyEnabled = true;
            ExpireWiseEnabled = true;
        }
    }
    
    /// <summary>
    /// Parses a boolean value from a string with flexible format support.
    /// </summary>
    /// <param name="value">The string to parse.</param>
    /// <param name="defaultValue">The default value if parsing fails.</param>
    /// <returns>The parsed boolean value.</returns>
    private static bool ParseBool(string value, bool defaultValue)
    {
        return value.ToLowerInvariant() switch
        {
            "true" or "1" or "yes" or "on" => true,
            "false" or "0" or "no" or "off" => false,
            _ => defaultValue
        };
    }
    
    /// <summary>
    /// Reloads the configuration from disk.
    /// </summary>
    public void Reload()
    {
        LoadConfiguration();
    }
    
    /// <summary>
    /// Saves the current configuration to disk.
    /// </summary>
    public void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(_configPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            var lines = new[]
            {
                "[Modules]",
                $"AllocationBuddy={AllocationBuddyEnabled.ToString().ToLower()}",
                $"EssentialsBuddy={EssentialsBuddyEnabled.ToString().ToLower()}",
                $"ExpireWise={ExpireWiseEnabled.ToString().ToLower()}",
                "",
                "[Info]",
                $"InstalledVersion={InstalledVersion ?? "unknown"}",
                $"InstallDate={InstallDate ?? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}"
            };
            
            File.WriteAllLines(_configPath, lines);
        }
        catch (Exception ex)
        {
            // Log save errors but don't crash
            Log.Warning(ex, "Failed to save module configuration to {Path}", _configPath);
        }
    }
    
    /// <summary>
    /// Enables or disables a module by name.
    /// </summary>
    /// <param name="moduleName">The name of the module ("AllocationBuddy", "EssentialsBuddy", or "ExpireWise").</param>
    /// <param name="enabled">Whether the module should be enabled.</param>
    public void SetModuleEnabled(string moduleName, bool enabled)
    {
        switch (moduleName)
        {
            case "AllocationBuddy":
                AllocationBuddyEnabled = enabled;
                break;
            case "EssentialsBuddy":
                EssentialsBuddyEnabled = enabled;
                break;
            case "ExpireWise":
                ExpireWiseEnabled = enabled;
                break;
        }
    }
}
