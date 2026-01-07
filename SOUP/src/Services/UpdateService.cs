using System.Diagnostics;
using System.IO;
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

    // GitHub repository info - update these for your repo
    private const string GitHubOwner = "PossiblyPengu";
    private const string GitHubRepo = "SOUP";
    private const string GitHubApiUrl = $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest";

    public UpdateService(ILogger<UpdateService>? logger = null)
    {
        _logger = logger;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "SOUP-Updater");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");

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
    /// Checks for updates from GitHub releases.
    /// </summary>
    /// <returns>Update info if available, null otherwise.</returns>
    public async Task<UpdateInfo?> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogInformation("Checking for updates. Current version: {Version}", _currentVersion);

            var response = await _httpClient.GetAsync(GitHubApiUrl, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogWarning("Failed to check for updates. Status: {StatusCode}", response.StatusCode);
                return null;
            }

            var release = await response.Content.ReadFromJsonAsync<GitHubRelease>(cancellationToken: cancellationToken);
            
            if (release == null)
            {
                _logger?.LogWarning("Failed to parse release response");
                return null;
            }

            var latestVersion = release.TagName.TrimStart('v');
            
            if (!IsNewerVersion(latestVersion, _currentVersion))
            {
                _logger?.LogInformation("No update available. Latest: {Latest}, Current: {Current}", 
                    latestVersion, _currentVersion);
                return null;
            }

            _logger?.LogInformation("Update available: {Version}", latestVersion);

            // Find the portable ZIP asset (self-contained)
            var portableAsset = release.Assets.FirstOrDefault(a => 
                a.Name.Contains("portable", StringComparison.OrdinalIgnoreCase) && 
                a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

            // Fall back to framework-dependent if portable not found
            var asset = portableAsset ?? release.Assets.FirstOrDefault(a => 
                a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

            return new UpdateInfo
            {
                Version = latestVersion,
                ReleaseNotes = release.Body ?? "",
                DownloadUrl = asset?.BrowserDownloadUrl ?? release.HtmlUrl,
                PublishedAt = release.PublishedAt,
                HtmlUrl = release.HtmlUrl,
                AssetName = asset?.Name,
                AssetSize = asset?.Size ?? 0
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error checking for updates");
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
/// GitHub release API response model.
/// </summary>
internal class GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string TagName { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; } = "";

    [JsonPropertyName("published_at")]
    public DateTime? PublishedAt { get; set; }

    [JsonPropertyName("assets")]
    public List<GitHubAsset> Assets { get; set; } = [];
}

/// <summary>
/// GitHub release asset model.
/// </summary>
internal class GitHubAsset
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("browser_download_url")]
    public string BrowserDownloadUrl { get; set; } = "";

    [JsonPropertyName("size")]
    public long Size { get; set; }
}
