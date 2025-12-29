#!/bin/bash

# Build the Universe Collections plugin

set -e

echo "Building Universe Collections..."

# Clean previous builds
rm -rf bin obj publish

# Build
dotnet build --configuration Release

# Publish
dotnet publish --configuration Release --output ./publish

echo ""
echo "Build complete!"
echo ""
echo "Plugin files are in ./publish/"
echo ""
echo "To install:"
echo "  1. Create folder: /path/to/jellyfin/plugins/UniverseCollections/"
echo "  2. Copy ./publish/UniverseCollections.dll to that folder"
echo "  3. Restart Jellyfin"
