import Foundation
import AppKit

@MainActor
final class WallpaperRotator: ObservableObject {
    static let shared = WallpaperRotator()

    @Published var currentImageID: String?
    @Published var isRunning = false

    private var rotationTimer: Timer?
    private var orderedImages: [CachedImage] = []
    private var currentIndex: Int = -1
    private var shuffledIndices: [Int] = []

    private let settings = AppSettings.shared

    // A 1x1 transparent PNG used to reset the wallpaper between changes.
    // This forces macOS to re-apply scaling options on the next real image.
    private lazy var blankImageURL: URL? = {
        let dir = ImageCache.cacheDirectory
        let blankURL = dir.appendingPathComponent(".wallp_blank.png")
        if !FileManager.default.fileExists(atPath: blankURL.path) {
            // Create a tiny 1x1 transparent PNG
            let image = NSImage(size: NSSize(width: 1, height: 1))
            image.lockFocus()
            NSColor.black.setFill()
            NSRect(x: 0, y: 0, width: 1, height: 1).fill()
            image.unlockFocus()
            if let tiff = image.tiffRepresentation,
               let bitmap = NSBitmapImageRep(data: tiff),
               let png = bitmap.representation(using: .png, properties: [:]) {
                try? png.write(to: blankURL)
            }
        }
        return blankURL
    }()

    // MARK: - Start / Stop

    func start() {
        guard !settings.isPaused else { return }
        isRunning = true
        refreshImageList()
        scheduleTimer()
    }

    func stop() {
        rotationTimer?.invalidate()
        rotationTimer = nil
        isRunning = false
    }

    func restart() {
        stop()
        start()
    }

    // MARK: - Switch Collection

    func switchToCollection(_ collectionID: UUID) {
        settings.defaultCollectionID = collectionID
        refreshImageList()
        currentIndex = -1

        if orderedImages.isEmpty {
            // No cached images yet — trigger a sync; the progressive callback in
            // SyncScheduler will set the wallpaper as soon as the first image arrives.
            Task { await SyncScheduler.shared.syncCollection(collectionID) }
            return
        }

        nextWallpaper()
        // Reset the rotation timer so the full interval starts from now
        if isRunning {
            scheduleTimer()
        }
    }

    func switchToDefaultCollection() {
        if let first = settings.collections.first {
            switchToCollection(first.id)
        }
    }

    // MARK: - Next / Previous

    func nextWallpaper() {
        guard !orderedImages.isEmpty else { return }

        let screens = NSScreen.screens
        guard !screens.isEmpty else { return }

        if screens.count > 1 && settings.multiMonitorMode == .differentPerMonitor {
            // Multi-monitor: pick a different random image for each screen
            var indices: [Int]
            if orderedImages.count >= screens.count {
                // Enough images — pick unique random indices
                indices = Array(Array(0..<orderedImages.count).shuffled().prefix(screens.count))
            } else {
                // Fewer images than screens — allow repeats
                indices = (0..<screens.count).map { _ in Int.random(in: 0..<orderedImages.count) }
            }

            for (i, screen) in screens.enumerated() {
                applyWallpaper(orderedImages[indices[i]], to: screen)
            }

            // Track the primary screen's image for UI display
            let mainIndex = screens.firstIndex(where: { $0 == NSScreen.main }) ?? 0
            currentIndex = indices[mainIndex]
            currentImageID = orderedImages[currentIndex].id
        } else {
            // Single monitor, or "same image on all displays" — pick one image
            // using the display-order setting and apply it to every screen.
            currentIndex = nextOrderedIndex()
            let image = orderedImages[currentIndex]
            for screen in screens {
                applyWallpaper(image, to: screen)
            }
            currentImageID = image.id
        }
    }

    /// Advances to the next image index according to the active display order.
    private func nextOrderedIndex() -> Int {
        switch settings.displayOrder {
        case .random:
            if shuffledIndices.isEmpty {
                reshuffleIndices()
            }
            return shuffledIndices.removeFirst()
        case .name, .dateCreated:
            return (currentIndex + 1) % orderedImages.count
        }
    }

    func previousWallpaper() {
        guard !orderedImages.isEmpty else { return }

        currentIndex = currentIndex > 0 ? currentIndex - 1 : orderedImages.count - 1
        let image = orderedImages[currentIndex]
        // Previous always applies the same image to all screens
        for screen in NSScreen.screens {
            applyWallpaper(image, to: screen)
        }
        currentImageID = image.id
    }

    // MARK: - Apply Wallpaper

    private func applyWallpaper(_ image: CachedImage, to screen: NSScreen) {
        let url = image.localURL
        guard FileManager.default.fileExists(atPath: url.path) else {
            print("[WallP] Wallpaper file missing: \(url.path)")
            return
        }

        // Fill mode: scale proportionally to cover the entire screen, clip overflow
        let fillOptions: [NSWorkspace.DesktopImageOptionKey: Any] = [
            .imageScaling: NSImageScaling.scaleProportionallyUpOrDown.rawValue,
            .allowClipping: true,
            .fillColor: NSColor.black
        ]

        do {
            // Workaround: macOS sometimes ignores scaling options if the URL
            // is the same as the current wallpaper, or on first set.
            // Briefly set to a blank image to force a full re-apply.
            let currentURL = NSWorkspace.shared.desktopImageURL(for: screen)
            if currentURL == url, let blank = blankImageURL {
                try NSWorkspace.shared.setDesktopImageURL(blank, for: screen, options: fillOptions)
            }

            try NSWorkspace.shared.setDesktopImageURL(url, for: screen, options: fillOptions)
            print("[WallP] Set wallpaper on \(screen.localizedName): \(image.wallhavenID)")
        } catch {
            print("[WallP] Failed to set wallpaper on \(screen.localizedName): \(error.localizedDescription)")
        }
    }

    // MARK: - Image List Management

    func refreshImageList() {
        guard let activeCollection = settings.activeCollection else {
            orderedImages = []
            return
        }

        var images = settings.imagesForCollection(activeCollection.id)

        switch settings.displayOrder {
        case .random:
            // Keep original order; shuffledIndices handles randomization
            break
        case .name:
            images.sort { $0.localFilename.localizedStandardCompare($1.localFilename) == .orderedAscending }
        case .dateCreated:
            images.sort { $0.dateAdded < $1.dateAdded }
        }

        orderedImages = images
        reshuffleIndices()
    }

    private func reshuffleIndices() {
        shuffledIndices = Array(0..<orderedImages.count).shuffled()
    }

    // MARK: - Timer

    private func scheduleTimer() {
        rotationTimer?.invalidate()
        let interval = TimeInterval(settings.rotationInterval.rawValue)
        rotationTimer = Timer.scheduledTimer(withTimeInterval: interval, repeats: true) { [weak self] _ in
            Task { @MainActor in
                self?.nextWallpaper()
            }
        }
    }

    // MARK: - Pause / Resume

    func pause() {
        settings.isPaused = true
        stop()
    }

    func resume() {
        settings.isPaused = false
        start()
    }
}
