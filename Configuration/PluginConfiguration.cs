using System.Collections.Generic;
using MediaBrowser.Model.Plugins;

namespace UniverseCollections.Configuration;

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets the Trakt Client ID.
    /// </summary>
    public string TraktClientId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Trakt Client Secret.
    /// </summary>
    public string TraktClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Trakt Access Token (stored after OAuth).
    /// </summary>
    public string TraktAccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of Trakt collections to sync.
    /// </summary>
    public List<TraktListConfig> TraktLists { get; set; } = new();

    /// <summary>
    /// Gets or sets whether to run sync on startup.
    /// </summary>
    public bool SyncOnStartup { get; set; } = false;

    /// <summary>
    /// Gets or sets the sync interval in hours (0 = disabled).
    /// </summary>
    public int SyncIntervalHours { get; set; } = 24;

    /// <summary>
    /// Gets or sets path mappings for file mtime manipulation.
    /// Maps Jellyfin paths to actual filesystem paths (useful for Docker).
    /// </summary>
    public List<PathMapping> PathMappings { get; set; } = new();
}

/// <summary>
/// Configuration for path mapping (e.g., Docker container to host).
/// </summary>
public class PathMapping
{
    /// <summary>
    /// Gets or sets the path as seen by Jellyfin (e.g., /media).
    /// </summary>
    public string From { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the actual filesystem path (e.g., /mnt/user/media).
    /// </summary>
    public string To { get; set; } = string.Empty;
}

/// <summary>
/// Configuration for a single Trakt list.
/// </summary>
public class TraktListConfig
{
    /// <summary>
    /// Gets or sets the Trakt list ID.
    /// </summary>
    public string ListId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the custom collection name (optional, uses Trakt name if empty).
    /// </summary>
    public string? CollectionName { get; set; }

    /// <summary>
    /// Gets or sets the slug for Wholphin tagging.
    /// </summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the sort title for collection ordering.
    /// </summary>
    public string? SortTitle { get; set; }

    /// <summary>
    /// Gets or sets the path to a custom poster image.
    /// </summary>
    public string? PosterPath { get; set; }

    /// <summary>
    /// Gets or sets whether to clear the collection before syncing.
    /// </summary>
    public bool ClearBeforeSync { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to preserve list order using Date Modified.
    /// </summary>
    public bool PreserveSortOrder { get; set; } = true;

    /// <summary>
    /// Gets or sets whether this list is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
