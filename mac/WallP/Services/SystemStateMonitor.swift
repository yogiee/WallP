import Foundation
import AppKit
import IOKit.pwr_mgt

@MainActor
final class SystemStateMonitor: ObservableObject {
    static let shared = SystemStateMonitor()

    @Published private(set) var isSystemSleeping = false
    @Published private(set) var isScreenLocked = false
    @Published private(set) var isScreenOff = false

    private var observers: [NSObjectProtocol] = []
    private let settings = AppSettings.shared

    var shouldPause: Bool {
        (settings.pauseOnSleep && isSystemSleeping) ||
        (settings.pauseOnLock && isScreenLocked) ||
        (settings.pauseOnScreenOff && isScreenOff)
    }

    // MARK: - Start / Stop

    func startMonitoring() {
        let wsNC = NSWorkspace.shared.notificationCenter

        // System sleep / wake
        observers.append(wsNC.addObserver(
            forName: NSWorkspace.willSleepNotification, object: nil, queue: .main
        ) { [weak self] _ in
            Task { @MainActor in
                self?.isSystemSleeping = true
                self?.evaluatePauseState()
            }
        })

        observers.append(wsNC.addObserver(
            forName: NSWorkspace.didWakeNotification, object: nil, queue: .main
        ) { [weak self] _ in
            Task { @MainActor in
                self?.isSystemSleeping = false
                self?.evaluatePauseState()
            }
        })

        // Screen sleep / wake
        observers.append(wsNC.addObserver(
            forName: NSWorkspace.screensDidSleepNotification, object: nil, queue: .main
        ) { [weak self] _ in
            Task { @MainActor in
                self?.isScreenOff = true
                self?.evaluatePauseState()
            }
        })

        observers.append(wsNC.addObserver(
            forName: NSWorkspace.screensDidWakeNotification, object: nil, queue: .main
        ) { [weak self] _ in
            Task { @MainActor in
                self?.isScreenOff = false
                self?.evaluatePauseState()
            }
        })

        // Screen lock / unlock
        observers.append(wsNC.addObserver(
            forName: NSWorkspace.sessionDidResignActiveNotification, object: nil, queue: .main
        ) { [weak self] _ in
            Task { @MainActor in
                self?.isScreenLocked = true
                self?.evaluatePauseState()
            }
        })

        observers.append(wsNC.addObserver(
            forName: NSWorkspace.sessionDidBecomeActiveNotification, object: nil, queue: .main
        ) { [weak self] _ in
            Task { @MainActor in
                self?.isScreenLocked = false
                self?.evaluatePauseState()
            }
        })

        // Distributed notifications for lock/unlock (belt-and-suspenders)
        let distNC = DistributedNotificationCenter.default()
        observers.append(distNC.addObserver(
            forName: NSNotification.Name("com.apple.screenIsLocked"), object: nil, queue: .main
        ) { [weak self] _ in
            Task { @MainActor in
                self?.isScreenLocked = true
                self?.evaluatePauseState()
            }
        })

        observers.append(distNC.addObserver(
            forName: NSNotification.Name("com.apple.screenIsUnlocked"), object: nil, queue: .main
        ) { [weak self] _ in
            Task { @MainActor in
                self?.isScreenLocked = false
                self?.evaluatePauseState()
            }
        })
    }

    func stopMonitoring() {
        let wsNC = NSWorkspace.shared.notificationCenter
        let distNC = DistributedNotificationCenter.default()
        for observer in observers {
            wsNC.removeObserver(observer)
            distNC.removeObserver(observer)
        }
        observers.removeAll()
    }

    // MARK: - Evaluate Pause State

    private func evaluatePauseState() {
        let rotator = WallpaperRotator.shared
        if shouldPause {
            if rotator.isRunning {
                rotator.stop()
                print("[WallP] Auto-paused (sleep: \(isSystemSleeping), lock: \(isScreenLocked), screenOff: \(isScreenOff))")
            }
        } else {
            if !rotator.isRunning && !settings.isPaused {
                rotator.start()
                print("[WallP] Auto-resumed")
            }
        }
    }
}
