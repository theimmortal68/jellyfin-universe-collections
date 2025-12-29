using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace UniverseCollections.Services;

/// <summary>
/// Service for interacting with the Trakt API.
/// </summary>
public class TraktService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TraktService> _logger;
    private const string TraktApiBaseUrl = "https://api.trakt.tv";

    public TraktService(IHttpClientFactory httpClientFactory, ILogger<TraktService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    private HttpClient CreateClient(string clientId, string? accessToken = null)
    {
        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(TraktApiBaseUrl);
        client.DefaultRequestHeaders.Add("trakt-api-version", "2");
        client.DefaultRequestHeaders.Add("trakt-api-key", clientId);
        client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        
        if (!string.IsNullOrEmpty(accessToken))
        {
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
        }

        return client;
    }

    /// <summary>
    /// Start device OAuth flow.
    /// </summary>
    public async Task<TraktDeviceCode?> GetDeviceCodeAsync(string clientId, CancellationToken cancellationToken)
    {
        using var client = CreateClient(clientId);
        
        var content = new StringContent(
            JsonSerializer.Serialize(new { client_id = clientId }),
            System.Text.Encoding.UTF8,
            "application/json");
        
        var response = await client.PostAsync("/oauth/device/code", content, cancellationToken);
        
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<TraktDeviceCode>(cancellationToken: cancellationToken);
        }

        var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogError("Failed to get device code: {Status} - {Error}", response.StatusCode, errorContent);
        return null;
    }

    /// <summary>
    /// Poll for device token.
    /// </summary>
    public async Task<TraktAccessToken?> PollDeviceTokenAsync(string clientId, string clientSecret, string deviceCode, CancellationToken cancellationToken)
    {
        using var client = CreateClient(clientId);

        var content = new StringContent(
            JsonSerializer.Serialize(new
            {
                client_id = clientId,
                client_secret = clientSecret,
                code = deviceCode
            }),
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await client.PostAsync("/oauth/device/token", content, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<TraktAccessToken>(cancellationToken: cancellationToken);
        }

        // 400 = pending, keep polling
        return null;
    }

    /// <summary>
    /// Get items from a Trakt list by ID.
    /// </summary>
    public async Task<TraktListResult?> GetListAsync(string listId, string clientId, string accessToken, CancellationToken cancellationToken)
    {
        using var client = CreateClient(clientId, accessToken);

        try
        {
            // First get list metadata
            var listResponse = await client.GetAsync($"/lists/{listId}", cancellationToken);
            if (!listResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to get list metadata: {Status}", listResponse.StatusCode);
                return null;
            }

            var listMeta = await listResponse.Content.ReadFromJsonAsync<TraktListMeta>(cancellationToken: cancellationToken);

            // Then get list items
            var itemsResponse = await client.GetAsync($"/lists/{listId}/items", cancellationToken);
            if (!itemsResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to get list items: {Status}", itemsResponse.StatusCode);
                return null;
            }

            var items = await itemsResponse.Content.ReadFromJsonAsync<List<TraktListItem>>(cancellationToken: cancellationToken);

            return new TraktListResult
            {
                Name = listMeta?.Name ?? "Unknown List",
                Description = listMeta?.Description,
                Items = items ?? new List<TraktListItem>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Trakt list {ListId}", listId);
            return null;
        }
    }
}

#region Trakt API Models

public class TraktDeviceCode
{
    [JsonPropertyName("device_code")]
    public string DeviceCode { get; set; } = string.Empty;

    [JsonPropertyName("user_code")]
    public string UserCode { get; set; } = string.Empty;

    [JsonPropertyName("verification_url")]
    public string VerificationUrl { get; set; } = string.Empty;

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("interval")]
    public int Interval { get; set; }
}

public class TraktAccessToken
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = string.Empty;

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; } = string.Empty;
}

public class TraktListMeta
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

public class TraktListItem
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("movie")]
    public TraktMovie? Movie { get; set; }

    [JsonPropertyName("show")]
    public TraktShow? Show { get; set; }
}

public class TraktMovie
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("year")]
    public int? Year { get; set; }

    [JsonPropertyName("ids")]
    public TraktIds Ids { get; set; } = new();
}

public class TraktShow
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("year")]
    public int? Year { get; set; }

    [JsonPropertyName("ids")]
    public TraktIds Ids { get; set; } = new();
}

public class TraktIds
{
    [JsonPropertyName("trakt")]
    public int? Trakt { get; set; }

    [JsonPropertyName("slug")]
    public string? Slug { get; set; }

    [JsonPropertyName("imdb")]
    public string? Imdb { get; set; }

    [JsonPropertyName("tmdb")]
    public int? Tmdb { get; set; }
}

public class TraktListResult
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<TraktListItem> Items { get; set; } = new();
}

#endregion
