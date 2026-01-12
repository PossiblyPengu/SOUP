using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;

namespace SOUP.Services;

/// <summary>
/// Service for checking and downloading updates from GitHub releases.
/// </summary>
public sealed class UpdateService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UpdateService>? _logger;
    private readonly string _currentVersion;
    private bool _disposed;

    private const string GitHubOwner = "PossiblyPengu";
    private const string GitHubRepo = "SOUP";
    // Use /releases (not /releases/latest) to include prereleases
    private const string GitHubApiUrl = $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases";

    public UpdateService(ILogger<UpdateService>? logger = null)
    {
        _logger = logger;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "SOUP-Updater");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
        _httpClient.Timeout = TimeSpan.FromSeconds(15);

        // Get current version from assembly
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        _currentVersion = version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "0.0.0";
    }

    /// <summary>
    /// Gets the current application version.
    /// </summary>
    public string CurrentVersion => _currentVersion;

    /// <summary>
    /// Gets the last error message from update check, if any.
    /// </summary>
    public string? LastCheckError { get; private set; }

    /// <summary>
    /// Checks for updates from GitHub releases.
    /// </summary>
    /// <returns>Update info if available, null otherwise.</returns>
    public async Task<UpdateInfo?> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogInformation("Checking for updates on GitHub. Current version: {Version}", _currentVersion);
            LastCheckError = null;

            var response = await _httpClient.GetAsync(GitHubApiUrl, cancellationToken);
            
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger?.LogWarning("No releases found on GitHub");
                LastCheckError = "No releases found";
                return null;
            }
            
            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                _logger?.LogWarning("GitHub API rate limit exceeded");
                LastCheckError = "GitHub rate limit exceeded. Try again later.";
                return null;
            }
            
            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogWarning("Failed to check for updates. Status: {StatusCode}", response.StatusCode);
                LastCheckError = $"GitHub API error: {response.StatusCode}";
                return null;
            }

            // Parse as array since we're fetching all releases
            var releases = await response.Content.ReadFromJsonAsync<GitHubRelease[]>(cancellationToken: cancellationToken);
            
            if (releases == null || releases.Length == 0)
            {
                _logger?.LogWarning("No releases found on GitHub");
                LastCheckError = "No releases found";
                return null;
            }

            // Find the release with the highest version that has a portable zip asset
            GitHubRelease? bestRelease = null;
            Version? bestVersion = null;
            
            foreach (var rel in releases)
            {
                var tagVersion = rel.TagName?.TrimStart('v') ?? "";
                if (!Version.TryParse(tagVersion, out var ver))
                    continue;
                    
                // Check if this release has a portable zip
                var hasPortableZip = rel.Assets?.Any(a => 
                    a.Name?.Contains("portable", StringComparison.OrdinalIgnoreCase) == true && 
                    a.Name?.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) == true) ?? false;
                
                if (!hasPortableZip)
                    continue;
                    
                // Check if this is a newer version than what we've found
                if (bestVersion == null || ver > bestVersion)
                {
                    bestVersion = ver;
                    bestRelease = rel;
                }
            }
            
            if (bestRelease == null || bestVersion == null)
            {
                _logger?.LogWarning("No releases with downloadable assets found");
                LastCheckError = "No downloadable releases found";
                return null;
            }

            var latestVersion = bestVersion.ToString();
            
            if (!IsNewerVersion(latestVersion, _currentVersion))
            {
                _logger?.LogInformation("No update available. Latest: {Latest}, Current: {Current}", 
                    latestVersion, _currentVersion);
                return null;
            }

            _logger?.LogInformation("Update available: {Version}", latestVersion);

            // Find the portable zip asset
            var zipAsset = bestRelease.Assets?.FirstOrDefault(a => 
                a.Name?.Contains("portable", StringComparison.OrdinalIgnoreCase) == true && 
                a.Name?.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) == true);

            return new UpdateInfo
            {
                Version = latestVersion,
                ReleaseNotes = bestRelease.Body ?? "",
                DownloadUrl = zipAsset?.BrowserDownloadUrl,
                PublishedAt = bestRelease.PublishedAt,
                HtmlUrl = bestRelease.HtmlUrl,
                AssetName = zipAsset?.Name,
                AssetSize = zipAsset?.Size ?? 0
            };
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogWarning(ex, "Cannot reach GitHub");
            LastCheckError = "Cannot reach GitHub. Check your internet connection.";
            return null;
        }
        catch (TaskCanceledException)
        {
            _logger?.LogWarning("Update check timed out");
            LastCheckError = "Update check timed out";
            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error checking for updates");
            LastCheckError = "Failed to check for updates";
            return null;
        }
    }

    /// <summary>
    /// Downloads an update to a temporary location.
    /// </summary>
    public async Task<string?> DownloadUpdateAsync(UpdateInfo updateInfo, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(updateInfo.DownloadUrl) || !updateInfo.DownloadUrl.EndsWith(".zip"))
        {
            _logger?.LogWarning("No direct download URL available");
            return null;
        }

        try
        {
            var tempPath = Path.Combine(Path.GetTempPath(), "SOUP_Update");
            Directory.CreateDirectory(tempPath);
            
            var zipPath = Path.Combine(tempPath, updateInfo.AssetName ?? $"SOUP-{updateInfo.Version}.zip");

            _logger?.LogInformation("Downloading update to {Path}", zipPath);

            using var response = await _httpClient.GetAsync(updateInfo.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? updateInfo.AssetSize;
            
            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
            
            var buffer = new byte[8192];
            long totalRead = 0;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                totalRead += bytesRead;
                
                if (totalBytes > 0)
                {
                    progress?.Report((double)totalRead / totalBytes * 100);
                }
            }

            _logger?.LogInformation("Download complete: {Path}", zipPath);
            return zipPath;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error downloading update");
            return null;
        }
    }

    /// <summary>
    /// Applies a downloaded update by extracting it and restarting the application.
    /// </summary>
    /// <param name="zipPath">Path to the downloaded zip file</param>
    /// <returns>True if update was initiated successfully</returns>
    public bool ApplyUpdate(string zipPath)
    {
        try
        {
            if (!File.Exists(zipPath))
            {
                _logger?.LogError("Update zip file not found: {Path}", zipPath);
                return false;
            }

            var appPath = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(appPath))
            {
                _logger?.LogError("Could not determine application path");
                return false;
            }

            var appDir = Path.GetDirectoryName(appPath)!;
            var extractPath = Path.Combine(Path.GetTempPath(), "SOUP_Update_Extract");
            var updaterScript = Path.Combine(Path.GetTempPath(), "SOUP_Updater.bat");

            _logger?.LogInformation("Applying update from {ZipPath} to {AppDir}", zipPath, appDir);

            // Clean extract directory
            if (Directory.Exists(extractPath))
            {
                Directory.Delete(extractPath, true);
            }

            // Extract the update
            ZipFile.ExtractToDirectory(zipPath, extractPath);

            // Create the updater batch script
            // This script waits for the app to close, copies files, and restarts
            var batchContent = $@"@echo off
title SOUP Updater
echo Updating SOUP...
echo.

:: Wait for the application to close (up to 10 seconds)
set attempts=0
:waitloop
tasklist /fi ""imagename eq SOUP.exe"" 2>nul | find /i ""SOUP.exe"" >nul
if errorlevel 1 goto docopy
set /a attempts+=1
if %attempts% gtr 20 (
    echo Timeout waiting for SOUP to close.
    pause
    exit /b 1
)
timeout /t 1 /nobreak >nul
goto waitloop

:docopy
echo Copying new files...

:: Use robocopy for more reliable file replacement
robocopy ""{extractPath}"" ""{appDir}"" /E /IS /IT /IM /R:2 /W:1 /NP /NFL /NDL
if %errorlevel% geq 8 (
    echo Failed to copy files!
    pause
    exit /b 1
)

echo.
echo Update complete! Starting SOUP...
timeout /t 2 /nobreak >nul

:: Cleanup temp files first
rmdir /s /q ""{extractPath}"" 2>nul
del ""{zipPath}"" 2>nul

:: Restart the application
cd /d ""{appDir}""
start """" ""SOUP.exe""

:: Delete this script
timeout /t 1 /nobreak >nul
(goto) 2>nul & del ""%~f0""
";

            File.WriteAllText(updaterScript, batchContent);

            // Start the updater script
            var startInfo = new ProcessStartInfo
            {
                FileName = updaterScript,
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            Process.Start(startInfo);

            _logger?.LogInformation("Updater script started, application will restart");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error applying update");
            return false;
        }
    }

    /// <summary>
    /// Downloads and applies an update, then restarts the application.
    /// </summary>
    public async Task<bool> DownloadAndApplyUpdateAsync(UpdateInfo updateInfo, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        var zipPath = await DownloadUpdateAsync(updateInfo, progress, cancellationToken);
        if (string.IsNullOrEmpty(zipPath))
        {
            return false;
        }

        return ApplyUpdate(zipPath);
    }

    /// <summary>
    /// Opens the download page in the default browser.
    /// </summary>
    public void OpenReleasePage(UpdateInfo? updateInfo = null)
    {
        var url = updateInfo?.HtmlUrl ?? updateInfo?.DownloadUrl;
        
        if (string.IsNullOrEmpty(url))
        {
            _logger?.LogWarning("No download URL available");
            return;
        }
        
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to open download page");
        }
    }

    /// <summary>
    /// Compares two version strings to determine if the first is newer.
    /// </summary>
    private static bool IsNewerVersion(string latest, string current)
    {
        if (!Version.TryParse(latest, out var latestVer) || 
            !Version.TryParse(current, out var currentVer))
        {
            return false;
        }

        return latestVer > currentVer;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _httpClient.Dispose();
        _disposed = true;
    }
}

/// <summary>
/// Information about an available update.
/// </summary>
public class UpdateInfo
{
    public required string Version { get; init; }
    public string ReleaseNotes { get; init; } = "";
    public string? DownloadUrl { get; init; }
    public DateTime? PublishedAt { get; init; }
    public string? HtmlUrl { get; init; }
    public string? AssetName { get; init; }
    public long AssetSize { get; init; }
}

/// <summary>
/// GitHub release API response model.
/// </summary>
internal class GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string? TagName { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [JsonPropertyName("html_url")]
    public string? HtmlUrl { get; set; }

    [JsonPropertyName("published_at")]
    public DateTime? PublishedAt { get; set; }

    [JsonPropertyName("assets")]
    public List<GitHubAsset>? Assets { get; set; }
}

/// <summary>
/// GitHub release asset model.
/// </summary>
internal class GitHubAsset
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("browser_download_url")]
    public string? BrowserDownloadUrl { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }
}
