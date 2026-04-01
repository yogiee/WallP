import SwiftUI
import AppIntents

@main
struct WallPApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) var appDelegate

    // Hold a reference so Sparkle starts with the app
    private let updaterService = UpdaterService.shared

    var body: some Scene {
        MenuBarExtra("WallP", image: "MenuBarIcon") {
            MenuBarPopover()
        }
        .menuBarExtraStyle(.window)

        Settings {
            SettingsView()
        }
        .commands {
            CommandGroup(after: .appInfo) {
                Button("Check for Updates…") {
                    UpdaterService.shared.checkForUpdates()
                }
            }
        }
    }
}

// MARK: - App Delegate

final class AppDelegate: NSObject, NSApplicationDelegate {
    func applicationDidFinishLaunching(_ notification: Notification) {
        print("[WallP] App launched")

        // Core startup — never depends on Focus filter
        Task { @MainActor in
            let settings = AppSettings.shared

            // Start system state monitoring (sleep/lock/screen)
            SystemStateMonitor.shared.startMonitoring()
            print("[WallP] System state monitor started")

            // Start sync scheduler (timer for periodic checks)
            SyncScheduler.shared.start()
            print("[WallP] Sync scheduler started (interval: \(settings.syncInterval.displayName))")

            // Start rotation if we have cached images
            let rotator = WallpaperRotator.shared
            if let activeCollection = settings.activeCollection {
                let imageCount = settings.imagesForCollection(activeCollection.id).count
                print("[WallP] Active collection: \(activeCollection.name) (\(imageCount) cached images)")
                if imageCount > 0 {
                    rotator.start()
                    rotator.nextWallpaper()
                    print("[WallP] Rotator started")
                } else {
                    print("[WallP] No cached images — waiting for sync")
                }
            } else {
                print("[WallP] No active collection configured")
            }
        }

        // Focus filter check — isolated so it can't block the main startup
        Task { @MainActor in
            await checkFocusFilter()
        }
    }

    func applicationWillTerminate(_ notification: Notification) {
        print("[WallP] App terminating")
        Task { @MainActor in
            WallpaperRotator.shared.stop()
            SyncScheduler.shared.stop()
            SystemStateMonitor.shared.stopMonitoring()
        }
    }

    // MARK: - Focus Filter (isolated)

    @MainActor
    private func checkFocusFilter() async {
        do {
            let filter = try await WallPFocusFilter.current
            if let collectionIDString = filter.collection?.id,
               let collectionID = UUID(uuidString: collectionIDString) {
                print("[WallP] Focus filter active, switching to collection: \(collectionIDString)")
                WallpaperRotator.shared.switchToCollection(collectionID)
            } else {
                print("[WallP] Focus filter has no collection assigned — using default")
            }
        } catch {
            // Expected when no Focus filter is configured for this app yet.
            // This is normal on first launch — the user needs to configure it
            // in System Settings → Focus → [Mode] → Add Filter → WallP.
            print("[WallP] No Focus filter configured (this is normal): \(error.localizedDescription)")
        }
    }
}
