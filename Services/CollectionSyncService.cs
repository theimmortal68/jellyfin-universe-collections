using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UniverseCollections.Configuration;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace UniverseCollections.Services;

/// <summary>
/// Service for syncing Trakt lists to Jellyfin collections with tagging.
/// </summary>
public class CollectionSyncService
{
    private readonly ILibraryManager _libraryManager;
    private readonly ICollectionManager _collectionManager;
    private readonly IProviderManager _providerManager;
    private readonly TraktService _traktService;
    private readonly ILogger<CollectionSyncService> _logger;

    public CollectionSyncService(
        ILibraryManager libraryManager,
        ICollectionManager collectionManager,
        IProviderManager providerManager,
        TraktService traktService,
        ILogger<CollectionSyncService> logger)
    {
        _libraryManager = libraryManager;
        _collectionManager = collectionManager;
        _providerManager = providerManager;
        _traktService = traktService;
        _logger = logger;
    }

    /// <summary>
    /// Generate a URL-safe slug from a name.
    /// </summary>
    public static string GenerateSlug(string name)
    {
        var slug = name.ToLowerInvariant();
        slug = Regex.Replace(slug, @"[\s_]+", "-");
        slug = Regex.Replace(slug, @"[^a-z0-9\-]", "");
        slug = Regex.Replace(slug, @"-+", "-");
        return slug.Trim('-');
    }

    /// <summary>
    /// Sync all configured Trakt lists.
    /// </summary>
    public async Task SyncAllListsAsync(CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration;
        if (config == null)
        {
            _logger.LogError("Plugin configuration is null");
            return;
        }

        if (string.IsNullOrEmpty(config.TraktClientId) || string.IsNullOrEmpty(config.TraktAccessToken))
        {
            _logger.LogError("Trakt API credentials not configured");
            return;
        }

        foreach (var listConfig in config.TraktLists.Where(l => l.Enabled))
        {
            try
            {
                await SyncListAsync(listConfig, config, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing list {ListId}", listConfig.ListId);
            }
        }
    }

    /// <summary>
    /// Sync a single Trakt list.
    /// </summary>
    public async Task SyncListAsync(TraktListConfig listConfig, PluginConfiguration config, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Syncing Trakt list {ListId}", listConfig.ListId);

        // Fetch list from Trakt
        var traktList = await _traktService.GetListAsync(
            listConfig.ListId,
            config.TraktClientId,
            config.TraktAccessToken,
            cancellationToken);

        if (traktList == null)
        {
            _logger.LogError("Failed to fetch Trakt list {ListId}", listConfig.ListId);
            return;
        }

        var collectionName = !string.IsNullOrEmpty(listConfig.CollectionName) 
            ? listConfig.CollectionName 
            : traktList.Name;

        var slug = !string.IsNullOrEmpty(listConfig.Slug) 
            ? listConfig.Slug 
            : GenerateSlug(collectionName);

        _logger.LogInformation("Collection: {Name}, Slug: {Slug}, Items: {Count}", 
            collectionName, slug, traktList.Items.Count);

        // Find or create collection
        var collection = await FindOrCreateCollectionAsync(collectionName, cancellationToken);
        if (collection == null)
        {
            _logger.LogError("Failed to create collection {Name}", collectionName);
            return;
        }

        // Clear collection if configured
        if (listConfig.ClearBeforeSync)
        {
            await ClearCollectionAsync(collection, cancellationToken);
        }

        // Build tags for this collection
        var collectionTags = new List<string> { "jac", $"jac:{slug}" };

        // Tag the collection
        await SetItemTagsAsync(collection, collectionTags, cancellationToken);

        // Set sort title if configured
        if (!string.IsNullOrEmpty(listConfig.SortTitle))
        {
            collection.ForcedSortName = listConfig.SortTitle;
            await collection.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, cancellationToken);
            _logger.LogInformation("Set sort title: {SortTitle}", listConfig.SortTitle);
        }

        // Match and add items
        var matchedItems = new List<BaseItem>();

        foreach (var traktItem in traktList.Items)
        {
            var (title, year, imdbId) = traktItem.Type switch
            {
                "movie" => (traktItem.Movie?.Title, traktItem.Movie?.Year, traktItem.Movie?.Ids.Imdb),
                "show" => (traktItem.Show?.Title, traktItem.Show?.Year, traktItem.Show?.Ids.Imdb),
                _ => (null, null, null)
            };

            if (string.IsNullOrEmpty(title))
            {
                continue;
            }

            var matchedItem = FindItemInLibrary(title, year, imdbId, traktItem.Type);
            
            if (matchedItem != null)
            {
                // Add to collection
                await _collectionManager.AddToCollectionAsync(collection.Id, new[] { matchedItem.Id });
                matchedItems.Add(matchedItem);

                // Tag the item
                await AddTagToItemAsync(matchedItem, $"jac:{slug}", cancellationToken);

                _logger.LogInformation("Added: {Title} ({Year})", title, year);
            }
            else
            {
                _logger.LogWarning("Not found: {Title} ({Year})", title, year);
            }
        }

        // Set file mtime to preserve sort order
        if (listConfig.PreserveSortOrder && matchedItems.Count > 0)
        {
            SetSortOrderByMtime(matchedItems, config.PathMappings);
            
            // Set collection display order to DateModified
            collection.DisplayOrder = "DateModified";
            await collection.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, cancellationToken);
        }

        // Upload custom poster if configured
        if (!string.IsNullOrEmpty(listConfig.PosterPath) && File.Exists(listConfig.PosterPath))
        {
            await UploadPosterAsync(collection, listConfig.PosterPath, cancellationToken);
        }

        _logger.LogInformation("Collection '{Name}' synced with {Count} items", collectionName, matchedItems.Count);
    }

    private async Task<BoxSet?> FindOrCreateCollectionAsync(string name, CancellationToken cancellationToken)
    {
        // Search for existing collection
        var existingCollections = _libraryManager.GetItemList(new InternalItemsQuery
        {
            IncludeItemTypes = new[] { BaseItemKind.BoxSet },
            Name = name,
            Recursive = true
        });

        var existing = existingCollections.FirstOrDefault() as BoxSet;
        if (existing != null)
        {
            _logger.LogInformation("Found existing collection: {Name}", name);
            return existing;
        }

        // Create new collection
        _logger.LogInformation("Creating new collection: {Name}", name);
        var options = new CollectionCreationOptions
        {
            Name = name,
            IsLocked = false
        };

        var collection = await _collectionManager.CreateCollectionAsync(options);
        return collection;
    }

    private async Task ClearCollectionAsync(BoxSet collection, CancellationToken cancellationToken)
    {
        var children = collection.GetChildren(null, true).ToList();
        if (children.Count > 0)
        {
            var childIds = children.Select(c => c.Id).ToArray();
            await _collectionManager.RemoveFromCollectionAsync(collection.Id, childIds);
            _logger.LogInformation("Cleared {Count} items from collection", children.Count);
        }
    }

    private BaseItem? FindItemInLibrary(string title, int? year, string? imdbId, string mediaType)
    {
        var itemTypes = mediaType switch
        {
            "movie" => new[] { BaseItemKind.Movie },
            "show" => new[] { BaseItemKind.Series },
            _ => new[] { BaseItemKind.Movie, BaseItemKind.Series }
        };

        // First try IMDB match
        if (!string.IsNullOrEmpty(imdbId))
        {
            var items = _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = itemTypes,
                Recursive = true,
                HasImdbId = true
            });

            var imdbMatch = items.FirstOrDefault(i => 
                i.GetProviderId(MetadataProvider.Imdb) == imdbId);
            
            if (imdbMatch != null)
            {
                return imdbMatch;
            }
        }

        // Fall back to title + year match
        var titleMatches = _libraryManager.GetItemList(new InternalItemsQuery
        {
            IncludeItemTypes = itemTypes,
            SearchTerm = title,
            Recursive = true
        });

        // Exact title match with year
        var exactMatch = titleMatches.FirstOrDefault(i =>
            i.Name.Equals(title, StringComparison.OrdinalIgnoreCase) &&
            (!year.HasValue || i.ProductionYear == year));

        if (exactMatch != null)
        {
            return exactMatch;
        }

        // Just year match
        if (year.HasValue)
        {
            var yearMatch = titleMatches.FirstOrDefault(i => i.ProductionYear == year);
            if (yearMatch != null)
            {
                return yearMatch;
            }
        }

        // Return first result if only one
        if (titleMatches.Count == 1)
        {
            return titleMatches[0];
        }

        return null;
    }

    private async Task SetItemTagsAsync(BaseItem item, List<string> tags, CancellationToken cancellationToken)
    {
        var existingTags = item.Tags?.ToList() ?? new List<string>();
        
        // Remove existing jac tags
        existingTags.RemoveAll(t => t.StartsWith("jac"));
        
        // Add new tags
        existingTags.AddRange(tags);
        item.Tags = existingTags.Distinct().ToArray();
        
        await item.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, cancellationToken);
    }

    private async Task AddTagToItemAsync(BaseItem item, string tag, CancellationToken cancellationToken)
    {
        var existingTags = item.Tags?.ToList() ?? new List<string>();
        
        if (!existingTags.Contains(tag))
        {
            existingTags.Add(tag);
            item.Tags = existingTags.ToArray();
            await item.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, cancellationToken);
        }
    }

    private async Task UploadPosterAsync(BoxSet collection, string posterPath, CancellationToken cancellationToken)
    {
        try
        {
            var imageBytes = await File.ReadAllBytesAsync(posterPath, cancellationToken);
            var mimeType = posterPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase) 
                ? "image/png" 
                : "image/jpeg";

            await _providerManager.SaveImage(collection, imageBytes, mimeType, ImageType.Primary, null, cancellationToken);
            _logger.LogInformation("Uploaded custom poster from {Path}", posterPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload poster from {Path}", posterPath);
        }
    }

    /// <summary>
    /// Apply path mappings to convert Jellyfin paths to actual filesystem paths.
    /// </summary>
    private string ApplyPathMappings(string path, List<PathMapping> mappings)
    {
        foreach (var mapping in mappings)
        {
            if (!string.IsNullOrEmpty(mapping.From) && path.StartsWith(mapping.From, StringComparison.OrdinalIgnoreCase))
            {
                var mappedPath = mapping.To + path.Substring(mapping.From.Length);
                _logger.LogDebug("Path mapped: {Original} -> {Mapped}", path, mappedPath);
                return mappedPath;
            }
        }
        return path;
    }

    /// <summary>
    /// Set file modification times to preserve sort order.
    /// Files are touched in reverse order so first item has newest mtime.
    /// </summary>
    private void SetSortOrderByMtime(List<BaseItem> items, List<PathMapping> pathMappings)
    {
        if (items.Count == 0) return;

        var baseTime = DateTime.UtcNow;
        var successCount = 0;

        // Process in reverse order: last item gets oldest time, first item gets newest
        for (int i = items.Count - 1; i >= 0; i--)
        {
            var item = items[i];
            var jellyfinPath = item.Path;

            if (string.IsNullOrEmpty(jellyfinPath))
            {
                _logger.LogWarning("No path for item: {Name}", item.Name);
                continue;
            }

            var actualPath = ApplyPathMappings(jellyfinPath, pathMappings);

            // For folders (like movie folders), find the main video file
            var targetPath = actualPath;
            if (Directory.Exists(actualPath))
            {
                var videoExtensions = new[] { ".mkv", ".mp4", ".avi", ".m4v", ".mov", ".wmv", ".ts", ".m2ts" };
                var videoFile = Directory.GetFiles(actualPath)
                    .FirstOrDefault(f => videoExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()));
                
                if (videoFile != null)
                {
                    targetPath = videoFile;
                }
                else
                {
                    _logger.LogWarning("No video file found in folder: {Path}", actualPath);
                    continue;
                }
            }

            if (!File.Exists(targetPath))
            {
                _logger.LogWarning("File not found: {Path}", targetPath);
                continue;
            }

            try
            {
                // Each item gets a time 1 second older than the previous
                var newTime = baseTime.AddSeconds(-(items.Count - 1 - i));
                File.SetLastWriteTimeUtc(targetPath, newTime);
                successCount++;
                _logger.LogDebug("Set mtime for {Name}: {Time}", item.Name, newTime);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set mtime for: {Path}", targetPath);
            }
        }

        _logger.LogInformation("Set mtime for {Count}/{Total} items", successCount, items.Count);
    }
}
