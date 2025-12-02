using System;
using System.IO;

namespace SAP.Services;

/// <summary>
/// Manages module enable/disable configuration based on installer selections.
/// Reads from %APPDATA%\SAP\modules.ini
/// </summary>
public class ModuleConfiguration
{
    private static readonly Lazy<ModuleConfiguration> _instance = new(() => new ModuleConfiguration());
    
    public static ModuleConfiguration Instance => _instance.Value;
    
    public bool AllocationBuddyEnabled { get; private set; } = true;
    public bool EssentialsBuddyEnabled { get; private set; } = true;
    public bool ExpireWiseEnabled { get; private set; } = true;
    
    public string? InstalledVersion { get; private set; }
    public string? InstallDate { get; private set; }
    
    private readonly string _configPath;
    
    private ModuleConfiguration()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _configPath = Path.Combine(appDataPath, "SAP", "modules.ini");
        
        LoadConfiguration();
    }
    
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
        catch
        {
            // If there's any error reading config, enable all modules
            AllocationBuddyEnabled = true;
            EssentialsBuddyEnabled = true;
            ExpireWiseEnabled = true;
        }
    }
    
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
    /// Reload configuration from disk
    /// </summary>
    public void Reload()
    {
        LoadConfiguration();
    }
    
    /// <summary>
    /// Save current configuration to disk
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
        catch
        {
            // Ignore save errors
        }
    }
    
    /// <summary>
    /// Enable or disable a module (for settings UI)
    /// </summary>
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
