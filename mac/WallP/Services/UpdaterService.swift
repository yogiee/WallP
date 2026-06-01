@preconcurrency import Sparkle
import Foundation

enum UpdateCheckSchedule: Int, CaseIterable {
    case daily = 86400
    case weekly = 604800
    case manual = 0

    var displayName: String {
        switch self {
        case .daily: "Every day"
        case .weekly: "Every week"
        case .manual: "Manual only"
        }
    }
}

@Observable
@MainActor
final class UpdaterService {
    @MainActor static let shared = UpdaterService()

    private let controller: SPUStandardUpdaterController

    var updateCheckSchedule: UpdateCheckSchedule {
        get {
            let raw = UserDefaults.standard.integer(forKey: "updateCheckSchedule")
            return UpdateCheckSchedule(rawValue: raw) ?? .weekly
        }
        set {
            UserDefaults.standard.set(newValue.rawValue, forKey: "updateCheckSchedule")
            applySchedule(newValue)
        }
    }

    private init() {
        controller = SPUStandardUpdaterController(
            startingUpdater: true,
            updaterDelegate: nil,
            userDriverDelegate: nil
        )
        let raw = UserDefaults.standard.integer(forKey: "updateCheckSchedule")
        applySchedule(UpdateCheckSchedule(rawValue: raw) ?? .weekly)
    }

    func checkForUpdates() {
        controller.checkForUpdates(nil)
    }

    private func applySchedule(_ schedule: UpdateCheckSchedule) {
        let updater = controller.updater
        switch schedule {
        case .daily, .weekly:
            updater.automaticallyChecksForUpdates = true
            updater.automaticallyDownloadsUpdates = true
            updater.updateCheckInterval = TimeInterval(schedule.rawValue)
        case .manual:
            updater.automaticallyChecksForUpdates = false
            updater.automaticallyDownloadsUpdates = false
        }
    }
}
