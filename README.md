# Universe Collections

[![Build Plugin](https://github.com/theimmortal68/jellyfin-universe-collections/actions/workflows/build.yml/badge.svg)](https://github.com/theimmortal68/jellyfin-universe-collections/actions/workflows/build.yml)

A Jellyfin plugin that syncs Trakt lists to collections with tagging for advanced client customizations.

## Features

- Sync Trakt lists to Jellyfin collections
- Tag collections and items with `universe-collection:{slug}` for client-side queries
- Custom collection names, sort titles, and posters
- Preserve list order via file mtime manipulation
- Configurable via Jellyfin admin UI
- Scheduled or manual sync

## Installation

### Via Jellyfin Plugin Catalog (Recommended)

1. In Jellyfin, go to `Dashboard -> Plugins -> Catalog`
2. Click the gear icon (⚙️) next to "Repositories" to add a new repository
3. Add repository URL: `https://raw.githubusercontent.com/theimmortal68/jellyfin-universe-collections/main/manifest.json`
4. Click "Save"
5. Search for "Universe Collections" and install
6. Restart Jellyfin

### Manual Installation

1. Download `universe-collections-x.x.x.x.zip` from the [latest release](https://github.com/theimmortal68/jellyfin-universe-collections/releases/latest)
2. Extract `UniverseCollections.dll` from the zip
3. Create the plugin folder:
   - Linux: `/var/lib/jellyfin/plugins/UniverseCollections/`
   - Docker: `/config/plugins/UniverseCollections/`
   - Windows: `C:\ProgramData\Jellyfin\Server\plugins\UniverseCollections\`
4. Copy `UniverseCollections.dll` to that folder
5. Restart Jellyfin

### Building from Source

Prerequisites: .NET 8 SDK

```bash
git clone https://github.com/theimmortal68/jellyfin-universe-collections.git
cd jellyfin-universe-collections
dotnet build --configuration Release
```

The plugin DLL will be in `bin/Release/net8.0/UniverseCollections.dll`

## Configuration

1. Go to **Dashboard → Plugins → Universe Collections**
2. Enter your Trakt API credentials:
   - Get Client ID/Secret from https://trakt.tv/oauth/applications
3. Click "Authenticate with Trakt" and follow the device auth flow
4. **Add Path Mappings** (required for sort order in Docker):
   - **From**: Jellyfin path (e.g., `/media`)
   - **To**: Actual filesystem path (e.g., `/mnt/user/media`)
5. Add your lists with:
   - **List ID**: Trakt list ID (e.g., `33354427`)
   - **Slug**: URL-safe identifier for tagging (e.g., `mcu`)
   - **Collection Name**: Optional override
   - **Sort Title**: For collection ordering (e.g., `!040 MCU`)
   - **Poster Path**: Path to custom poster image
6. Save and click "Sync Now"

## Example Configuration

For your MCU list:

| Field | Value |
|-------|-------|
| List ID | `33354427` |
| Slug | `mcu` |
| Sort Title | `!040 Marvel Cinematic Universe` |
| Poster Path | `/config/posters/mcu.jpg` |

### Path Mapping Example (Docker)

If Jellyfin sees media at `/media/movies` but the actual path is `/mnt/user/media/movies`:

| From | To |
|------|-----|
| `/media` | `/mnt/user/media` |

This is required for the "Preserve List Order" feature to work, as it needs to modify file timestamps.

## Client Integration

After sync, query collections and items by tag:

```kotlin
// Get all Universe Collections
GET /Items?IncludeItemTypes=BoxSet&Tags=universe-collection

// Get movies in MCU collection
GET /Items?IncludeItemTypes=Movie&Tags=universe-collection:mcu
```

## Tags Applied

| Tag | Applied To | Purpose |
|-----|-----------|---------|
| `universe-collection` | Collections | Identifies plugin-managed collections |
| `universe-collection:{slug}` | Collections + Items | Links items to their collection |

## Scheduled Task

The plugin registers a scheduled task "Sync Universe Collections" that runs at the configured interval. You can also run it manually from Dashboard → Scheduled Tasks.

## Troubleshooting

### Plugin not loading
- Check Jellyfin logs for errors
- Ensure DLL is in a subfolder (e.g., `plugins/UniverseCollections/`)
- Verify .NET 8 compatibility with your Jellyfin version

### Trakt auth failing
- Verify Client ID/Secret are correct
- Check if your Trakt app is approved
- Look at Jellyfin logs for API errors

### Items not matching
- Plugin matches by IMDB ID first, then title+year
- Check if your library items have IMDB provider IDs
- Enable debug logging for detailed match info

## License

MIT
