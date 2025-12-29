export VERSION := 1.0.0.0
export GITHUB_REPO := theimmortal68/jellyfin-universe-collections
export FILE := universe-collections-${VERSION}.zip

build:
	dotnet build --configuration Release

zip:
	zip "${FILE}" bin/Release/net8.0/UniverseCollections.dll

csum:
	md5sum "${FILE}"

create-tag:
	git tag v${VERSION}
	git push origin v${VERSION}

create-gh-release:
	gh release create v${VERSION} "${FILE}" --generate-notes --verify-tag

release: build zip create-tag create-gh-release
	@echo "Release v${VERSION} complete!"
	@echo "Don't forget to update manifest.json with the checksum:"
	@md5sum "${FILE}"
