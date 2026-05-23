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

        // Observe live Focus changes from the extension via CF Darwin notification center.
        // DistributedNotificationCenter is unreliable from sandboxed extensions;
        // CFNotificationCenterGetDarwinNotifyCenter() works across all process boundaries.
        CFNotificationCenterAddObserver(
            CFNotificationCenterGetDarwinNotifyCenter(),
            Unmanaged.passUnretained(self).toOpaque(),
            { _, _, _, _, _ in Task { @MainActor in AppDelegate.applyFocusFilter() } },
            "com.wallp.app.focusFilterChanged" as CFString,
            nil,
            .deliverImmediately
        )
    }

    func applicationWillTerminate(_ notification: Notification) {
        print("[WallP] App terminating")
        CFNotificationCenterRemoveObserver(
            CFNotificationCenterGetDarwinNotifyCenter(),
            Unmanaged.passUnretained(self).toOpaque(),
            CFNotificationName("com.wallp.app.focusFilterChanged" as CFString),
            nil
        )
        Task { @MainActor in
            WallpaperRotator.shared.stop()
            SyncScheduler.shared.stop()
            SystemStateMonitor.shared.stopMonitoring()
        }
    }

    @MainActor
    private static func applyFocusFilter() {
        // The extension writes a plain file to the shared group container.
        // UserDefaults(suiteName:) write is blocked by the sandbox in the extension
        // ("user-preference-write outside container"), but file-write-data is allowed.
        // File absent = no filter ever fired; empty string = revert to default.
        guard let containerURL = FileManager.default.containerURL(
            forSecurityApplicationGroupIdentifier: "group.com.wallp.app"
        ) else { return }

        let fileURL = containerURL.appendingPathComponent("wallp_focus_active_collection")
        guard let value = try? String(contentsOf: fileURL, encoding: .utf8) else { return }

        if !value.isEmpty, let id = UUID(uuidString: value) {
            print("[WallP] Focus filter active → switching to collection: \(value)")
            WallpaperRotator.shared.switchToCollection(id)
        } else {
            print("[WallP] Focus filter active → reverting to default collection")
            WallpaperRotator.shared.switchToDefaultCollection()
        }
    }
}
