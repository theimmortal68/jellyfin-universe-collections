using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UniverseCollections.Services;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace UniverseCollections.ScheduledTasks;

/// <summary>
/// Scheduled task to sync Trakt lists to Jellyfin collections.
/// </summary>
public class CollectionSyncTask : IScheduledTask
{
    private readonly CollectionSyncService _syncService;
    private readonly ILogger<CollectionSyncTask> _logger;

    public CollectionSyncTask(CollectionSyncService syncService, ILogger<CollectionSyncTask> logger)
    {
        _syncService = syncService;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "Sync Universe Collections";

    /// <inheritdoc />
    public string Key => "UniverseCollectionsSync";

    /// <inheritdoc />
    public string Description => "Syncs configured Trakt lists to Jellyfin collections with Wholphin tagging.";

    /// <inheritdoc />
    public string Category => "Library";

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Auto Collections sync task");
        progress.Report(0);

        try
        {
            await _syncService.SyncAllListsAsync(cancellationToken);
            progress.Report(100);
            _logger.LogInformation("Auto Collections sync task completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Auto Collections sync task failed");
            throw;
        }
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        var config = Plugin.Instance?.Configuration;
        var intervalHours = config?.SyncIntervalHours ?? 24;

        if (intervalHours <= 0)
        {
            return Array.Empty<TaskTriggerInfo>();
        }

        return new[]
        {
            new TaskTriggerInfo
            {
                Type = TaskTriggerInfo.TriggerInterval,
                IntervalTicks = TimeSpan.FromHours(intervalHours).Ticks
            }
        };
    }
}
