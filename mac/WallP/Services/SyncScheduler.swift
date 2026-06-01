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

        // Auto-sync on launch if collections exist but cache is empty
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
                let collectionID = collection.id
                syncProgress = "Syncing \"\(collection.name)\"..."
                print("[WallP] Syncing collection: \(collection.name) (Wallhaven #\(collection.wallhavenCollectionID))")

                let newImages = try await imageCache.syncCollection(collection) { image in
                    await MainActor.run {
                        self.handleImageDownloaded(image, collectionID: collectionID)
                    }
                }

                print("[WallP] Downloaded \(newImages.count) new images for \"\(collection.name)\"")
                settings.collections[i].lastSynced = Date()
            }

            let totalImages = settings.collections.reduce(0) {
                $0 + settings.imagesForCollection($1.id).count
            }
            syncProgress = "Sync complete. \(totalImages) images cached."
            print("[WallP] Sync complete. Total cached images: \(totalImages)")

            // Final refresh in case active collection gained images without triggering start
            let rotator = WallpaperRotator.shared
            rotator.refreshImageList()
            if !rotator.isRunning && !settings.isPaused && totalImages > 0 {
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

            let newImages = try await imageCache.syncCollection(collection) { image in
                await MainActor.run {
                    self.handleImageDownloaded(image, collectionID: collectionID)
                }
            }

            settings.collections[index].lastSynced = Date()
            syncProgress = "Downloaded \(newImages.count) new images."
            print("[WallP] Downloaded \(newImages.count) new images for \"\(collection.name)\"")
        } catch {
            lastSyncError = error.localizedDescription
            syncProgress = ""
            print("[WallP] Sync error: \(error)")
        }

        isSyncing = false
    }

    // MARK: - Progressive Image Handler

    private func handleImageDownloaded(_ image: CachedImage, collectionID: UUID) {
        let wasEmpty = settings.imagesForCollection(collectionID).isEmpty

        settings.cachedImages.append(image)
        if let idx = settings.collections.firstIndex(where: { $0.id == collectionID }) {
            settings.collections[idx].cachedImageIDs.append(image.id)
        }

        // Only kick the rotator when this is the currently-displayed collection
        // and it just got its first image — avoids spurious wallpaper changes during
        // background collection syncs.
        guard wasEmpty, settings.activeCollection?.id == collectionID else { return }

        let rotator = WallpaperRotator.shared
        rotator.refreshImageList()
        if !rotator.isRunning && !settings.isPaused {
            rotator.start()
        }
        rotator.nextWallpaper()
        print("[WallP] First image for active collection — wallpaper set immediately")
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
