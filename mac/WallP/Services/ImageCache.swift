import Foundation

actor ImageCache {
    static let shared = ImageCache()

    static let cacheDirectory: URL = {
        let appSupport = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask).first!
        let cacheDir = appSupport.appendingPathComponent("WallP/cache", isDirectory: true)
        try? FileManager.default.createDirectory(at: cacheDir, withIntermediateDirectories: true)
        return cacheDir
    }()

    private let api = WallhavenAPIService.shared

    // MARK: - Ensure Collection Directory

    private func collectionDirectory(for collectionID: UUID) -> URL {
        let dir = Self.cacheDirectory.appendingPathComponent(collectionID.uuidString, isDirectory: true)
        try? FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        return dir
    }

    // MARK: - Sync Collection from Wallhaven

    func syncCollection(
        _ collection: WallPCollection,
        onImageCached: (@Sendable (CachedImage) async -> Void)? = nil
    ) async throws -> [CachedImage] {
        print("[WallP][Cache] Fetching wallpaper list for collection \(collection.wallhavenCollectionID)...")

        let wallpapers = try await api.fetchAllCollectionWallpapers(
            username: collection.wallhavenUsername,
            collectionID: collection.wallhavenCollectionID
        )

        print("[WallP][Cache] Found \(wallpapers.count) wallpapers in collection")

        let existingIDs: Set<String> = await MainActor.run {
            Set(AppSettings.shared.imagesForCollection(collection.id).map(\.wallhavenID))
        }
        let cacheLimit: Int = await MainActor.run { AppSettings.shared.cacheLimit.rawValue }
        let shouldOptimize: Bool = await MainActor.run { AppSettings.shared.optimizeImages }

        // Filter out already-cached images
        let newWallpapers = wallpapers.filter { !existingIDs.contains($0.id) }
        print("[WallP][Cache] \(newWallpapers.count) new wallpapers to download (already cached: \(existingIDs.count))")

        // Limit how many new images to download
        let remaining = cacheLimit - existingIDs.count
        let toDownload = Array(newWallpapers.prefix(max(0, remaining)))

        if toDownload.isEmpty {
            print("[WallP][Cache] Nothing new to download")
            return []
        }

        print("[WallP][Cache] Downloading \(toDownload.count) images...")
        var newCachedImages: [CachedImage] = []

        for (index, wallpaper) in toDownload.enumerated() {
            do {
                print("[WallP][Cache] Downloading \(index + 1)/\(toDownload.count): \(wallpaper.id) (\(wallpaper.resolution))")
                let cachedImage = try await downloadAndCache(
                    wallpaper: wallpaper,
                    collectionID: collection.id,
                    optimize: shouldOptimize
                )
                newCachedImages.append(cachedImage)
                print("[WallP][Cache] Cached: \(cachedImage.localFilename) (\(ByteCountFormatter.string(fromByteCount: cachedImage.fileSize, countStyle: .file)))")
                await onImageCached?(cachedImage)
            } catch {
                print("[WallP][Cache] Failed to cache \(wallpaper.id): \(error.localizedDescription)")
            }

            // Small delay to avoid hammering the server — use try? so cancellation doesn't kill the loop
            try? await Task.sleep(for: .milliseconds(500))
        }

        return newCachedImages
    }

    // MARK: - Download and Cache Single Image

    private func downloadAndCache(
        wallpaper: WallhavenWallpaper,
        collectionID: UUID,
        optimize: Bool
    ) async throws -> CachedImage {
        let imageData = try await api.downloadImage(from: wallpaper.path)

        let collectionDir = collectionDirectory(for: collectionID)
        let ext = URL(string: wallpaper.path)?.pathExtension ?? "jpg"
        let tempFilename = "\(wallpaper.id)_original.\(ext)"
        let tempURL = collectionDir.appendingPathComponent(tempFilename)

        // Write original to disk
        try imageData.write(to: tempURL)

        let finalURL: URL
        let finalFilename: String

        if optimize {
            let optimizedFilename = "\(wallpaper.id)_opt"
            let optimizedURL = collectionDir.appendingPathComponent(optimizedFilename)
            finalURL = try await ImageOptimizer.shared.optimize(sourceURL: tempURL, destinationURL: optimizedURL)
            finalFilename = finalURL.lastPathComponent

            // Remove original temp file
            try? FileManager.default.removeItem(at: tempURL)
        } else {
            let keepFilename = "\(wallpaper.id).\(ext)"
            let keepURL = collectionDir.appendingPathComponent(keepFilename)
            if FileManager.default.fileExists(atPath: keepURL.path) {
                try? FileManager.default.removeItem(at: keepURL)
            }
            try FileManager.default.moveItem(at: tempURL, to: keepURL)
            finalURL = keepURL
            finalFilename = keepFilename
        }

        let fileSize = (try? FileManager.default.attributesOfItem(atPath: finalURL.path)[.size] as? Int64) ?? 0

        return CachedImage(
            id: wallpaper.id,
            wallhavenID: wallpaper.id,
            originalURL: wallpaper.path,
            localFilename: finalFilename,
            width: wallpaper.dimensionX,
            height: wallpaper.dimensionY,
            fileSize: fileSize,
            dateAdded: Date(),
            collectionID: collectionID
        )
    }

    // MARK: - Clear Cache

    func clearCache(for collectionID: UUID) {
        let dir = collectionDirectory(for: collectionID)
        if FileManager.default.fileExists(atPath: dir.path) {
            try? FileManager.default.removeItem(at: dir)
        }
    }

    func clearAllCache() {
        if FileManager.default.fileExists(atPath: Self.cacheDirectory.path) {
            try? FileManager.default.removeItem(at: Self.cacheDirectory)
            try? FileManager.default.createDirectory(at: Self.cacheDirectory, withIntermediateDirectories: true)
        }
    }

    // MARK: - Cache Size

    func totalCacheSize() -> Int64 {
        guard let enumerator = FileManager.default.enumerator(
            at: Self.cacheDirectory,
            includingPropertiesForKeys: [.fileSizeKey],
            options: [.skipsHiddenFiles]
        ) else { return 0 }

        var total: Int64 = 0
        for case let fileURL as URL in enumerator {
            if let size = try? fileURL.resourceValues(forKeys: [.fileSizeKey]).fileSize {
                total += Int64(size)
            }
        }
        return total
    }

    func formattedCacheSize() -> String {
        let bytes = totalCacheSize()
        let formatter = ByteCountFormatter()
        formatter.allowedUnits = [.useGB, .useMB]
        formatter.countStyle = .file
        return formatter.string(fromByteCount: bytes)
    }
}
