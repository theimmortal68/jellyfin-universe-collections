using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using UniverseCollections.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace UniverseCollections.Api;

/// <summary>
/// API controller for Universe Collections plugin.
/// </summary>
[ApiController]
[Route("UniverseCollections")]
[Authorize(Policy = "RequiresElevation")]
[Produces(MediaTypeNames.Application.Json)]
public class UniverseCollectionsController : ControllerBase
{
    private readonly CollectionSyncService _syncService;
    private readonly TraktService _traktService;
    private readonly ILogger<UniverseCollectionsController> _logger;

    public UniverseCollectionsController(
        CollectionSyncService syncService,
        TraktService traktService,
        ILogger<UniverseCollectionsController> logger)
    {
        _syncService = syncService;
        _traktService = traktService;
        _logger = logger;
    }

    /// <summary>
    /// Trigger a sync of all configured lists.
    /// </summary>
    [HttpPost("Sync")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> SyncAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Manual sync triggered via API");
        
        // Run sync in background
        _ = Task.Run(async () =>
        {
            await _syncService.SyncAllListsAsync(cancellationToken);
        }, cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Start Trakt device OAuth flow.
    /// </summary>
    [HttpPost("TraktAuth")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TraktAuthResponse>> StartTraktAuthAsync(
        [FromBody] TraktAuthRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("TraktAuth called with ClientId length: {Length}", request.ClientId?.Length ?? 0);
        
        if (string.IsNullOrEmpty(request.ClientId))
        {
            _logger.LogError("TraktAuth: Client ID is empty");
            return BadRequest("Client ID is required");
        }

        var deviceCode = await _traktService.GetDeviceCodeAsync(request.ClientId, cancellationToken);
        
        if (deviceCode == null)
        {
            _logger.LogError("TraktAuth: Failed to get device code from Trakt");
            return BadRequest("Failed to get device code from Trakt");
        }

        _logger.LogInformation("TraktAuth: Got device code, user code: {UserCode}", deviceCode.UserCode);
        
        return Ok(new TraktAuthResponse
        {
            DeviceCode = deviceCode.DeviceCode,
            UserCode = deviceCode.UserCode,
            VerificationUrl = deviceCode.VerificationUrl,
            Interval = deviceCode.Interval
        });
    }

    /// <summary>
    /// Poll for Trakt device token.
    /// </summary>
    [HttpPost("TraktAuthPoll")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<ActionResult<TraktTokenResponse>> PollTraktAuthAsync(
        [FromBody] TraktPollRequest request,
        CancellationToken cancellationToken)
    {
        var token = await _traktService.PollDeviceTokenAsync(
            request.ClientId,
            request.ClientSecret,
            request.DeviceCode,
            cancellationToken);

        if (token == null)
        {
            // Still pending
            return Accepted(new TraktTokenResponse { AccessToken = null });
        }

        // Save token to config
        var config = Plugin.Instance?.Configuration;
        if (config != null)
        {
            config.TraktAccessToken = token.AccessToken;
            Plugin.Instance?.SaveConfiguration();
        }

        return Ok(new TraktTokenResponse { AccessToken = token.AccessToken });
    }
}

#region Request/Response Models

public class TraktAuthRequest
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
}

public class TraktAuthResponse
{
    public string DeviceCode { get; set; } = string.Empty;
    public string UserCode { get; set; } = string.Empty;
    public string VerificationUrl { get; set; } = string.Empty;
    public int Interval { get; set; }
}

public class TraktPollRequest
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string DeviceCode { get; set; } = string.Empty;
}

public class TraktTokenResponse
{
    public string? AccessToken { get; set; }
}

#endregion
