import Foundation

@MainActor
final class SyncScheduler: ObservableObject {
    static let shared = SyncScheduler()

    @Published var isSyncing = false
    @Published var lastSyncError: String?
    @Published var syncProgress: String = ""

    private var syncTimer: Timer?
    private let settings = AppSettings.shared
    private let imageCache = ImageCache.shared

    // MARK: - Start / Stop

    func start() {
        scheduleTimer()

        // Do an initial sync if we have collections but no cached images
        if !settings.collections.isEmpty {
            let totalCached = settings.collections.reduce(0) {
                $0 + settings.imagesForCollection($1.id).count
            }
            if totalCached == 0 {
                Task { await syncNow() }
            }
        }
    }

    func stop() {
        syncTimer?.invalidate()
        syncTimer = nil
    }

    func restart() {
        stop()
        start()
    }

    // MARK: - Manual Sync

    func syncNow() async {
        guard !isSyncing else { return }
        guard !settings.collections.isEmpty else {
            lastSyncError = "No collections configured. Add one in Settings."
            return
        }

        isSyncing = true
        lastSyncError = nil
        syncProgress = "Starting sync..."

        do {
            for i in settings.collections.indices {
                let collection = settings.collections[i]
                syncProgress = "Syncing \"\(collection.name)\"..."

                print("[WallP] Syncing collection: \(collection.name) (Wallhaven #\(collection.wallhavenCollectionID))")

                let newImages = try await imageCache.syncCollection(collection)

                print("[WallP] Downloaded \(newImages.count) new images for \"\(collection.name)\"")

                // Update cached images in settings
                settings.cachedImages.append(contentsOf: newImages)

                // Update collection's cached IDs and lastSynced
                settings.collections[i].cachedImageIDs.append(contentsOf: newImages.map(\.id))
                settings.collections[i].lastSynced = Date()
            }

            let totalImages = settings.collections.reduce(0) {
                $0 + settings.imagesForCollection($1.id).count
            }
            syncProgress = "Sync complete. \(totalImages) images cached."
            print("[WallP] Sync complete. Total cached images: \(totalImages)")

            // Refresh the rotator's image list and start if needed
            let rotator = WallpaperRotator.shared
            rotator.refreshImageList()
            if !rotator.isRunning && !settings.isPaused && totalImages > 0 {
                rotator.start()
                // Set the first wallpaper immediately
                rotator.nextWallpaper()
            }
        } catch {
            lastSyncError = error.localizedDescription
            syncProgress = ""
            print("[WallP] Sync error: \(error)")
        }

        isSyncing = false
    }

    // MARK: - Sync Single Collection

    func syncCollection(_ collectionID: UUID) async {
        guard !isSyncing else { return }
        isSyncing = true
        lastSyncError = nil

        do {
            guard let index = settings.collections.firstIndex(where: { $0.id == collectionID }) else {
                throw WallhavenError.notFound
            }

            let collection = settings.collections[index]
            syncProgress = "Syncing \"\(collection.name)\"..."
            print("[WallP] Syncing single collection: \(collection.name)")

            let newImages = try await imageCache.syncCollection(collection)

            settings.cachedImages.append(contentsOf: newImages)
            settings.collections[index].cachedImageIDs.append(contentsOf: newImages.map(\.id))
            settings.collections[index].lastSynced = Date()

            syncProgress = "Downloaded \(newImages.count) new images."
            print("[WallP] Downloaded \(newImages.count) new images for \"\(collection.name)\"")

            let rotator = WallpaperRotator.shared
            rotator.refreshImageList()
            if !rotator.isRunning && !settings.isPaused {
                rotator.start()
                rotator.nextWallpaper()
            }
        } catch {
            lastSyncError = error.localizedDescription
            syncProgress = ""
            print("[WallP] Sync error: \(error)")
        }

        isSyncing = false
    }

    // MARK: - Timer

    private func scheduleTimer() {
        syncTimer?.invalidate()
        guard settings.syncInterval != .manual else { return }

        let interval = TimeInterval(settings.syncInterval.rawValue)
        syncTimer = Timer.scheduledTimer(withTimeInterval: interval, repeats: true) { [weak self] _ in
            Task { @MainActor in
                await self?.syncNow()
            }
        }
    }
}
