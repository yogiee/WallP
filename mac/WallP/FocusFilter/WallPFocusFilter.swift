import AppIntents
import Foundation
import os

private let logger = Logger(subsystem: "com.wallp.app.AppIntentsExtension", category: "FocusFilter")

private func allCollections() -> [FocusFilterBridge.CollectionOption] {
    do {
        return try FocusFilterBridge.readCollections()
    } catch {
        logger.error("Could not read collections: \(error.localizedDescription, privacy: .public)")
        return []
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
        allCollections().map(\.name)
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
        // matches a collection (renamed/removed) the id is nil and the main app
        // falls back to its default collection.
        let id = collectionName.flatMap { name in
            allCollections().first { $0.name == name }?.id
        }

        do {
            try FocusFilterBridge.writeActiveCollection(id)
        } catch {
            // Don't notify: the app would re-read the previous value.
            logger.error("Could not record active collection: \(error.localizedDescription, privacy: .public)")
            return .result()
        }
        logger.notice("perform: collectionName=\(collectionName ?? "nil", privacy: .public) -> id=\(id?.uuidString ?? "nil", privacy: .public)")

        CFNotificationCenterPostNotification(
            CFNotificationCenterGetDarwinNotifyCenter(),
            CFNotificationName(FocusFilterBridge.notificationName as CFString),
            nil, nil, true
        )
        return .result()
    }
}
