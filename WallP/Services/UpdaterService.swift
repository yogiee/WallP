@preconcurrency import Sparkle
import Foundation

@Observable
@MainActor
final class UpdaterService {
    @MainActor static let shared = UpdaterService()

    private let controller: SPUStandardUpdaterController

    /// 0 = auto-update, 1 = download + ask, 2 = disabled
    var updateMode: Int {
        get { UserDefaults.standard.integer(forKey: "updateMode") }
        set {
            UserDefaults.standard.set(newValue, forKey: "updateMode")
            applyUpdateMode(newValue)
        }
    }

    private init() {
        controller = SPUStandardUpdaterController(
            startingUpdater: true,
            updaterDelegate: nil,
            userDriverDelegate: nil
        )
        applyUpdateMode(UserDefaults.standard.integer(forKey: "updateMode"))
    }

    func checkForUpdates() {
        controller.checkForUpdates(nil)
    }

    private func applyUpdateMode(_ mode: Int) {
        let updater = controller.updater
        switch mode {
        case 0:  // auto-update
            updater.automaticallyChecksForUpdates = true
            updater.automaticallyDownloadsUpdates = true
        case 1:  // download, ask to install
            updater.automaticallyChecksForUpdates = true
            updater.automaticallyDownloadsUpdates = false
        default: // disabled
            updater.automaticallyChecksForUpdates = false
            updater.automaticallyDownloadsUpdates = false
        }
    }
}
