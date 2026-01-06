using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace SOUP.Services.External;

/// <summary>
/// Business Central API client using OAuth 2.0 authentication
/// Mirrors SAM's BC_Auth and BC_DAL patterns
/// </summary>
public sealed class BusinessCentralService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BusinessCentralService>? _logger;
    private string? _accessToken;
    private DateTime _tokenExpiry = DateTime.MinValue;
    private bool _disposed;
    private readonly bool _ownsHttpClient;

    /// <summary>
    /// Creates a new BusinessCentralService with an IHttpClientFactory (preferred)
    /// </summary>
    public BusinessCentralService(IHttpClientFactory httpClientFactory, ILogger<BusinessCentralService>? logger = null)
    {
        _httpClient = httpClientFactory.CreateClient("BusinessCentral");
        _logger = logger;
        _ownsHttpClient = false; // Factory manages the client lifecycle
    }

    /// <summary>
    /// Creates a new BusinessCentralService with a pre-configured HttpClient (for DI)
    /// </summary>
    public BusinessCentralService(HttpClient httpClient, ILogger<BusinessCentralService>? logger = null)
    {
        _httpClient = httpClient;
        _logger = logger;
        _ownsHttpClient = false; // DI container manages the client
    }

    /// <summary>
    /// Creates a new BusinessCentralService (legacy constructor for backwards compatibility)
    /// </summary>
    [Obsolete("Use constructor with IHttpClientFactory for better socket management")]
    public BusinessCentralService(ILogger<BusinessCentralService>? logger)
    {
        _httpClient = CreateConfiguredHttpClient();
        _logger = logger;
        _ownsHttpClient = true; // We created it, we dispose it
    }

    /// <summary>
    /// Creates a properly configured HttpClient
    /// </summary>
    private static HttpClient CreateConfiguredHttpClient()
    {
        var handler = new HttpClientHandler
        {
            // Enforce TLS 1.2 or higher for security
            SslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13
        };
        
        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        
        return client;
    }

    /// <summary>
    /// Test connection and authentication to Business Central
    /// </summary>
    public async Task<(bool Success, string Message)> TestConnectionAsync(ExternalConnectionConfig config)
    {
        try
        {
            var token = await GetAccessTokenAsync(config);
            if (string.IsNullOrEmpty(token))
                return (false, "Failed to obtain access token");

            // Test with a simple API call
            var baseUrl = GetApiBaseUrl(config);
            var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/companies");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            
            var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
                return (true, "Connection successful");
            
            return (false, $"API call failed: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Business Central connection test failed");
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Get OAuth 2.0 access token using client credentials flow
    /// </summary>
    private async Task<string?> GetAccessTokenAsync(ExternalConnectionConfig config)
    {
        // Return cached token if still valid
        if (!string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _tokenExpiry.AddMinutes(-5))
            return _accessToken;

        try
        {
            var tokenUrl = $"https://login.microsoftonline.com/{config.BcTenantId}/oauth2/v2.0/token";
            
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = config.BcClientId,
                ["client_secret"] = config.BcClientSecret,
                ["scope"] = "https://api.businesscentral.dynamics.com/.default"
            });

            var response = await _httpClient.PostAsync(tokenUrl, content);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(json);

            if (tokenResponse != null)
            {
                _accessToken = tokenResponse.AccessToken;
                _tokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
                _logger?.LogInformation("Obtained BC access token, expires in {Seconds}s", tokenResponse.ExpiresIn);
                return _accessToken;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get BC access token");
        }

        return null;
    }

    /// <summary>
    /// Build the BC API base URL
    /// </summary>
    private string GetApiBaseUrl(ExternalConnectionConfig config)
    {
        if (!string.IsNullOrWhiteSpace(config.BcBaseUrl))
            return config.BcBaseUrl.TrimEnd('/');

        // Standard BC API URL format
        return $"https://api.businesscentral.dynamics.com/v2.0/{config.BcTenantId}/{config.BcEnvironment}/api/v2.0";
    }

    /// <summary>
    /// Get items from Business Central
    /// </summary>
    public async Task<List<BcItem>> GetItemsAsync(ExternalConnectionConfig config)
    {
        var items = new List<BcItem>();

        try
        {
            var token = await GetAccessTokenAsync(config);
            if (string.IsNullOrEmpty(token))
            {
                _logger?.LogWarning("No BC access token available");
                return items;
            }

            var baseUrl = GetApiBaseUrl(config);
            var companyFilter = string.IsNullOrWhiteSpace(config.BcCompanyId) 
                ? "" 
                : $"/companies({config.BcCompanyId})";

            // OData query for items
            var url = $"{baseUrl}{companyFilter}/items?$select=number,displayName,unitPrice,inventory,blocked";
            
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ODataResponse<BcItem>>(json);
            
            if (result?.Value != null)
            {
                items.AddRange(result.Value);
                _logger?.LogInformation("Loaded {Count} items from Business Central", items.Count);
            }

            // Handle pagination
            while (!string.IsNullOrEmpty(result?.NextLink))
            {
                request = new HttpRequestMessage(HttpMethod.Get, result.NextLink);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                
                response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                
                json = await response.Content.ReadAsStringAsync();
                result = JsonSerializer.Deserialize<ODataResponse<BcItem>>(json);
                
                if (result?.Value != null)
                    items.AddRange(result.Value);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get items from Business Central");
        }

        return items;
    }

    /// <summary>
    /// Get locations/stores from Business Central
    /// </summary>
    public async Task<List<BcLocation>> GetLocationsAsync(ExternalConnectionConfig config)
    {
        var locations = new List<BcLocation>();

        try
        {
            var token = await GetAccessTokenAsync(config);
            if (string.IsNullOrEmpty(token)) return locations;

            var baseUrl = GetApiBaseUrl(config);
            var companyFilter = string.IsNullOrWhiteSpace(config.BcCompanyId) 
                ? "" 
                : $"/companies({config.BcCompanyId})";

            var url = $"{baseUrl}{companyFilter}/locations?$select=code,displayName";
            
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ODataResponse<BcLocation>>(json);
            
            if (result?.Value != null)
            {
                locations.AddRange(result.Value);
                _logger?.LogInformation("Loaded {Count} locations from Business Central", locations.Count);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get locations from Business Central");
        }

        return locations;
    }

    /// <summary>
    /// Get item vendors (for vendor item numbers/SKUs)
    /// </summary>
    public async Task<List<BcItemVendor>> GetItemVendorsAsync(ExternalConnectionConfig config)
    {
        var itemVendors = new List<BcItemVendor>();

        try
        {
            var token = await GetAccessTokenAsync(config);
            if (string.IsNullOrEmpty(token)) return itemVendors;

            var baseUrl = GetApiBaseUrl(config);
            var companyFilter = string.IsNullOrWhiteSpace(config.BcCompanyId) 
                ? "" 
                : $"/companies({config.BcCompanyId})";

            // Note: itemVendors might need a custom API page in BC
            var url = $"{baseUrl}{companyFilter}/itemVendors?$select=itemNumber,vendorNumber,vendorItemNumber";
            
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.SendAsync(request);
            
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ODataResponse<BcItemVendor>>(json);
                
                if (result?.Value != null)
                    itemVendors.AddRange(result.Value);
            }
            else
            {
                _logger?.LogWarning("Item vendors API not available: {Status}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get item vendors from Business Central");
        }

        return itemVendors;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            // Only dispose if we own the HttpClient
            if (_ownsHttpClient)
            {
                _httpClient.Dispose();
            }
            
            // Clear sensitive data
            _accessToken = null;
            _disposed = true;
        }
    }
}

#region Response Models

internal sealed class TokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = "";
    
    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }
    
    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = "";
}

internal sealed class ODataResponse<T>
{
    [JsonPropertyName("value")]
    public List<T> Value { get; set; } = new();
    
    [JsonPropertyName("@odata.nextLink")]
    public string? NextLink { get; set; }
}

#endregion

#region BC Entity Models

/// <summary>
/// Item from Business Central
/// </summary>
public class BcItem
{
    [JsonPropertyName("number")]
    public string Number { get; set; } = "";
    
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = "";
    
    [JsonPropertyName("unitPrice")]
    public decimal UnitPrice { get; set; }
    
    [JsonPropertyName("inventory")]
    public decimal Inventory { get; set; }
    
    [JsonPropertyName("blocked")]
    public bool Blocked { get; set; }
}

/// <summary>
/// Location/Store from Business Central
/// </summary>
public class BcLocation
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = "";
    
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = "";
}

/// <summary>
/// Item-Vendor relationship from Business Central
/// </summary>
public class BcItemVendor
{
    [JsonPropertyName("itemNumber")]
    public string ItemNumber { get; set; } = "";
    
    [JsonPropertyName("vendorNumber")]
    public string VendorNumber { get; set; } = "";
    
    [JsonPropertyName("vendorItemNumber")]
    public string VendorItemNumber { get; set; } = "";
}

#endregion
