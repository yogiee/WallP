import AppIntents
import Foundation

// MARK: - Shared collection access (app group)

private enum FocusFilterStore {
    static let collectionsKey = "wallp_collections"
    static let appGroup = "group.com.wallp.app"

    static func allCollections() -> [WallPCollection] {
        guard let data = UserDefaults(suiteName: appGroup)?.data(forKey: collectionsKey),
              let decoded = try? JSONDecoder().decode([WallPCollection].self, from: data)
        else { return [] }
        return decoded
    }
}

// MARK: - Collection options

// A String parameter backed by a DynamicOptionsProvider renders as a single-select
// list in the Focus filter UI and reliably commits the user's pick. An Optional
// AppEntity parameter instead renders as a broken toggle on macOS 26 where the
// selection never sticks (it resolves to the query's defaultResult, or to nil).
//
// Note: macOS 26's Focus-filter picker has two purely cosmetic bugs we can't fix
// from code (confirmed identical for AppEntity and String params): the open menu
// shows a checkmark on every option, and the collapsed control renders blank when
// the filter is reopened to edit. The committed value is always correct regardless.
// We surface the collection NAME as the option value and resolve it to an id in
// perform(); intentionally no defaultResult() so we never force an unwanted default.
struct WallpaperCollectionOptionsProvider: DynamicOptionsProvider {
    func results() async throws -> [String] {
        FocusFilterStore.allCollections().map(\.name)
    }
}

// MARK: - Focus Filter Intent

struct WallPFocusFilter: SetFocusFilterIntent {
    static let title: LocalizedStringResource = "WallP Wallpaper Collection"
    static let description: IntentDescription? = IntentDescription(
        "Automatically switch wallpaper collection when this Focus activates."
    )

    // Optional so the system can preview the filter before it's configured.
    // nil (no selection) means "revert to the app's default collection".
    @Parameter(title: "Wallpaper Collection", optionsProvider: WallpaperCollectionOptionsProvider())
    var collectionName: String?

    var displayRepresentation: DisplayRepresentation {
        if let name = collectionName {
            return DisplayRepresentation(title: "Collection: \(name)")
        }
        return DisplayRepresentation(title: "Default Collection")
    }

    func perform() async throws -> some IntentResult {
        // Resolve the selected name back to a collection id. If the name no longer
        // matches a collection (renamed/removed) the id is nil → empty string →
        // the main app reverts to its default collection.
        let id = collectionName.flatMap { name in
            FocusFilterStore.allCollections().first { $0.name == name }?.id
        }
        let value = id?.uuidString ?? ""

        // UserDefaults(suiteName:) write is blocked by the sandbox
        // ("user-preference-write outside container"). Writing a plain file via
        // FileManager uses file-write-data access, which the app-group entitlement grants.
        if let containerURL = FileManager.default.containerURL(
            forSecurityApplicationGroupIdentifier: "group.com.wallp.app"
        ) {
            let fileURL = containerURL.appendingPathComponent("wallp_focus_active_collection")
            try? value.write(to: fileURL, atomically: true, encoding: .utf8)
        }

        CFNotificationCenterPostNotification(
            CFNotificationCenterGetDarwinNotifyCenter(),
            CFNotificationName("com.wallp.app.focusFilterChanged" as CFString),
            nil, nil, true
        )
        return .result()
    }
}
