using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;

namespace SOUP.Services;

/// <summary>
/// Service for checking and downloading updates from a local/network server.
/// </summary>
public sealed class UpdateService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UpdateService>? _logger;
    private readonly string _currentVersion;
    private bool _disposed;

    // Local/Network update server URL - update this to your server location
    // Examples:
    //   - Local IIS: "http://localhost/soup/version.json"
    //   - Network share via IIS: "http://server-name/soup/version.json"  
    //   - File share (use file:// URI): Configure via settings instead
    private const string UpdateManifestUrl = "http://localhost/soup/version.json";

    public UpdateService(ILogger<UpdateService>? logger = null)
    {
        _logger = logger;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "SOUP-Updater");
        _httpClient.Timeout = TimeSpan.FromSeconds(10);

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
    /// Checks for updates from the local update server.
    /// </summary>
    /// <returns>Update info if available, null otherwise.</returns>
    public async Task<UpdateInfo?> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogInformation("Checking for updates. Current version: {Version}", _currentVersion);
            LastCheckError = null;

            var response = await _httpClient.GetAsync(UpdateManifestUrl, cancellationToken);
            
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger?.LogWarning("Update manifest not found at {Url}", UpdateManifestUrl);
                LastCheckError = "Update server not configured";
                return null;
            }
            
            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogWarning("Failed to check for updates. Status: {StatusCode}", response.StatusCode);
                LastCheckError = $"Update server error: {response.StatusCode}";
                return null;
            }

            var manifest = await response.Content.ReadFromJsonAsync<UpdateManifest>(cancellationToken: cancellationToken);
            
            if (manifest == null)
            {
                _logger?.LogWarning("Failed to parse update manifest");
                LastCheckError = "Invalid update manifest";
                return null;
            }

            if (!IsNewerVersion(manifest.Version, _currentVersion))
            {
                _logger?.LogInformation("No update available. Latest: {Latest}, Current: {Current}", 
                    manifest.Version, _currentVersion);
                return null;
            }

            _logger?.LogInformation("Update available: {Version}", manifest.Version);

            return new UpdateInfo
            {
                Version = manifest.Version,
                ReleaseNotes = manifest.ReleaseNotes ?? "",
                DownloadUrl = manifest.DownloadUrl,
                PublishedAt = manifest.PublishedAt,
                HtmlUrl = manifest.DownloadUrl,
                AssetName = manifest.AssetName,
                AssetSize = manifest.AssetSize
            };
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogWarning(ex, "Cannot reach update server");
            LastCheckError = "Cannot reach update server";
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
    /// Opens the GitHub releases page in the default browser.
    /// </summary>
    public void OpenReleasePage(UpdateInfo? updateInfo = null)
    {
        var url = updateInfo?.HtmlUrl ?? $"https://github.com/{GitHubOwner}/{GitHubRepo}/releases/latest";
        
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
            _logger?.LogError(ex, "Failed to open release page");
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
/// Update manifest model for local/network update server.
/// Place this JSON file on your update server.
/// </summary>
internal class UpdateManifest
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "";

    [JsonPropertyName("downloadUrl")]
    public string? DownloadUrl { get; set; }

    [JsonPropertyName("releaseNotes")]
    public string? ReleaseNotes { get; set; }

    [JsonPropertyName("publishedAt")]
    public DateTime? PublishedAt { get; set; }

    [JsonPropertyName("assetName")]
    public string? AssetName { get; set; }

    [JsonPropertyName("assetSize")]
    public long AssetSize { get; set; }
}
