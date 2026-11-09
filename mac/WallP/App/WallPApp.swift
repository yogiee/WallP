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

            // Restore any Focus override before the rotator starts, so launching
            // inside a Focus doesn't flash a wallpaper from the default collection.
            settings.focusOverrideCollectionID = FocusFilterBridge.readActiveCollection()

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

        // Observe live Focus changes from the extension via CF Darwin notification center.
        // DistributedNotificationCenter is unreliable from sandboxed extensions;
        // CFNotificationCenterGetDarwinNotifyCenter() works across all process boundaries.
        CFNotificationCenterAddObserver(
            CFNotificationCenterGetDarwinNotifyCenter(),
            Unmanaged.passUnretained(self).toOpaque(),
            { _, _, _, _, _ in Task { @MainActor in AppDelegate.applyFocusFilter() } },
            FocusFilterBridge.notificationName as CFString,
            nil,
            .deliverImmediately
        )
    }

    func applicationWillTerminate(_ notification: Notification) {
        print("[WallP] App terminating")
        CFNotificationCenterRemoveObserver(
            CFNotificationCenterGetDarwinNotifyCenter(),
            Unmanaged.passUnretained(self).toOpaque(),
            CFNotificationName(FocusFilterBridge.notificationName as CFString),
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
        WallpaperRotator.shared.applyFocusOverride(FocusFilterBridge.readActiveCollection())
    }
}
