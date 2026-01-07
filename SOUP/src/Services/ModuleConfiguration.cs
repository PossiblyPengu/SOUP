using System;
using System.IO;
using System.Text.Json;
using Serilog;

namespace SOUP.Services;

/// <summary>
/// Manages module enable/disable configuration based on installer selections.
/// </summary>
/// <remarks>
/// <para>
/// This singleton service reads module configuration from either:
/// - <c>module_config.json</c> in the app directory (installer-created)
/// - <c>%APPDATA%\SOUP\modules.ini</c> (legacy/user-modified)
/// </para>
/// <para>
/// The configuration controls which modules are available in the application launcher.
/// If no configuration file exists (development mode), all modules are enabled by default.
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
    /// Gets whether the SwiftLabel module is enabled.
    /// </summary>
    public bool SwiftLabelEnabled { get; private set; } = true;
    
    /// <summary>
    /// Gets whether the OrderLog module is enabled.
    /// </summary>
    public bool OrderLogEnabled { get; private set; } = true;
    
    /// <summary>
    /// Gets whether Fun Stuff (Easter eggs) are enabled.
    /// </summary>
    public bool FunStuffEnabled { get; private set; } = true;
    
    /// <summary>
    /// Gets the installed version from the configuration.
    /// </summary>
    public string? InstalledVersion { get; private set; }
    
    /// <summary>
    /// Gets the installation date from the configuration.
    /// </summary>
    public string? InstallDate { get; private set; }
    
    private readonly string _iniConfigPath;
    private readonly string _jsonConfigPath;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="ModuleConfiguration"/> class.
    /// </summary>
    private ModuleConfiguration()
    {
        _iniConfigPath = Path.Combine(Core.AppPaths.AppData, "modules.ini");
        
        // JSON config is in the app directory (created by installer)
        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        _jsonConfigPath = Path.Combine(appDir, "module_config.json");
        
        LoadConfiguration();
    }
    
    /// <summary>
    /// Loads the configuration, preferring JSON format from installer.
    /// </summary>
    private void LoadConfiguration()
    {
        // First try JSON config from installer
        if (File.Exists(_jsonConfigPath))
        {
            try
            {
                LoadJsonConfiguration();
                return;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to load JSON module configuration, trying INI format");
            }
        }
        
        // Fall back to INI config
        if (File.Exists(_iniConfigPath))
        {
            LoadIniConfiguration();
        }
        // else: No config file = all modules enabled (development mode)
    }
    
    /// <summary>
    /// Loads configuration from JSON format (installer-created).
    /// </summary>
    private void LoadJsonConfiguration()
    {
        var json = File.ReadAllText(_jsonConfigPath);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        
        if (root.TryGetProperty("version", out var versionProp))
        {
            InstalledVersion = versionProp.GetString();
        }
        
        if (root.TryGetProperty("modules", out var modulesProp))
        {
            if (modulesProp.TryGetProperty("allocationBuddy", out var ab))
                AllocationBuddyEnabled = ab.GetBoolean();
                
            if (modulesProp.TryGetProperty("essentialsBuddy", out var eb))
                EssentialsBuddyEnabled = eb.GetBoolean();
                
            if (modulesProp.TryGetProperty("expireWise", out var ew))
                ExpireWiseEnabled = ew.GetBoolean();
                
            if (modulesProp.TryGetProperty("swiftLabel", out var sl))
                SwiftLabelEnabled = sl.GetBoolean();
                
            if (modulesProp.TryGetProperty("orderLog", out var ol))
                OrderLogEnabled = ol.GetBoolean();
                
            // Check both old and new key names for Easter egg
            if (modulesProp.TryGetProperty("sapNukem", out var sn))
                FunStuffEnabled = sn.GetBoolean();
            else if (modulesProp.TryGetProperty("funStuff", out var fs))
                FunStuffEnabled = fs.GetBoolean();
        }
        
        Log.Information("Loaded module config (JSON): AB={AB}, EB={EB}, EW={EW}, SL={SL}, OL={OL}, Fun={Fun}",
            AllocationBuddyEnabled, EssentialsBuddyEnabled, ExpireWiseEnabled, 
            SwiftLabelEnabled, OrderLogEnabled, FunStuffEnabled);
    }
    
    /// <summary>
    /// Loads the configuration from INI format (legacy).
    /// </summary>
    private void LoadIniConfiguration()
    {
        if (!File.Exists(_iniConfigPath))
        {
            return;
        }
        
        try
        {
            var lines = File.ReadAllLines(_iniConfigPath);
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
                            case "SwiftLabel":
                                SwiftLabelEnabled = ParseBool(value, true);
                                break;
                            case "OrderLog":
                                OrderLogEnabled = ParseBool(value, true);
                                break;
                            case "FunStuff":
                                FunStuffEnabled = ParseBool(value, true);
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
            Log.Warning(ex, "Failed to load module configuration from {Path}, enabling all modules", _iniConfigPath);
            AllocationBuddyEnabled = true;
            EssentialsBuddyEnabled = true;
            ExpireWiseEnabled = true;
            SwiftLabelEnabled = true;
            OrderLogEnabled = true;
            FunStuffEnabled = true;
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
            var directory = Path.GetDirectoryName(_iniConfigPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            var lines = new[]
            {
                "[Modules]",
                $"AllocationBuddy={AllocationBuddyEnabled.ToString().ToLowerInvariant()}",
                $"EssentialsBuddy={EssentialsBuddyEnabled.ToString().ToLowerInvariant()}",
                $"ExpireWise={ExpireWiseEnabled.ToString().ToLowerInvariant()}",
                $"SwiftLabel={SwiftLabelEnabled.ToString().ToLowerInvariant()}",
                $"OrderLog={OrderLogEnabled.ToString().ToLowerInvariant()}",
                $"FunStuff={FunStuffEnabled.ToString().ToLowerInvariant()}",
                "",
                "[Info]",
                $"InstalledVersion={InstalledVersion ?? "unknown"}",
                $"InstallDate={InstallDate ?? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}"
            };
            
            File.WriteAllLines(_iniConfigPath, lines);
        }
        catch (Exception ex)
        {
            // Log save errors but don't crash
            Log.Warning(ex, "Failed to save module configuration to {Path}", _iniConfigPath);
        }
    }
    
    /// <summary>
    /// Enables or disables a module by name.
    /// </summary>
    /// <param name="moduleName">The name of the module.</param>
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
            case "SwiftLabel":
                SwiftLabelEnabled = enabled;
                break;
            case "OrderLog":
                OrderLogEnabled = enabled;
                break;
            case "FunStuff":
                FunStuffEnabled = enabled;
                break;
        }
    }
}
