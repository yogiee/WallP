import SwiftUI

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

        // Focus filter override — reads collection written by the App Intents Extension
        // when a Focus mode activates.  Only overrides if the key exists (set by extension).
        Task { @MainActor in
            AppDelegate.applyFocusFilter()
        }

        // Observe live Focus changes posted by the extension while the app is running.
        DistributedNotificationCenter.default().addObserver(
            self,
            selector: #selector(handleFocusFilterChanged),
            name: NSNotification.Name("com.wallp.app.focusFilterChanged"),
            object: nil
        )
    }

    func applicationWillTerminate(_ notification: Notification) {
        print("[WallP] App terminating")
        Task { @MainActor in
            WallpaperRotator.shared.stop()
            SyncScheduler.shared.stop()
            SystemStateMonitor.shared.stopMonitoring()
        }
    }

    // MARK: - Focus Filter

    @objc private func handleFocusFilterChanged() {
        Task { @MainActor in AppDelegate.applyFocusFilter() }
    }

    @MainActor
    private static func applyFocusFilter() {
        // The extension writes a collection ID (or empty string for "default") under this key
        // when a Focus activates.  If the key is absent no focus filter has ever fired.
        let suite = UserDefaults(suiteName: "group.com.wallp.app")
        guard suite?.object(forKey: "wallp_focus_active_collection") != nil else { return }

        if let idStr = suite?.string(forKey: "wallp_focus_active_collection"),
           !idStr.isEmpty,
           let id = UUID(uuidString: idStr) {
            print("[WallP] Focus filter active → switching to collection: \(idStr)")
            WallpaperRotator.shared.switchToCollection(id)
        } else {
            print("[WallP] Focus filter active → reverting to default collection")
            WallpaperRotator.shared.switchToDefaultCollection()
        }
    }
}
