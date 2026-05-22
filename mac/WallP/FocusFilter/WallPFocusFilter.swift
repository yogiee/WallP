import AppIntents
import Foundation

// MARK: - Wallpaper Collection Entity

struct WallpaperCollectionEntity: AppEntity {
    static let typeDisplayRepresentation = TypeDisplayRepresentation(name: "Wallpaper Collection")
    static let defaultQuery = WallpaperCollectionQuery()

    var id: String
    var name: String

    var displayRepresentation: DisplayRepresentation {
        DisplayRepresentation(title: LocalizedStringResource(stringLiteral: name))
    }
}

// Reads collections from the main app's UserDefaults domain — accessible to the extension
// without requiring AppSettings (which depends on the full app environment).
struct WallpaperCollectionQuery: EntityQuery {
    private static let collectionsKey = "wallp_collections"
    private static let appDomain = "group.com.wallp.app"

    private func allCollections() -> [WallPCollection] {
        guard let data = UserDefaults(suiteName: Self.appDomain)?.data(forKey: Self.collectionsKey),
              let decoded = try? JSONDecoder().decode([WallPCollection].self, from: data)
        else { return [] }
        return decoded
    }

    func entities(for identifiers: [String]) async throws -> [WallpaperCollectionEntity] {
        allCollections()
            .filter { identifiers.contains($0.id.uuidString) }
            .map { WallpaperCollectionEntity(id: $0.id.uuidString, name: $0.name) }
    }

    func suggestedEntities() async throws -> [WallpaperCollectionEntity] {
        allCollections().map { WallpaperCollectionEntity(id: $0.id.uuidString, name: $0.name) }
    }
}

// MARK: - Focus Filter Intent

struct WallPFocusFilter: SetFocusFilterIntent {
    static let title: LocalizedStringResource = "WallP Wallpaper Collection"
    static let description: IntentDescription? = IntentDescription(
        "Automatically switch wallpaper collection when this Focus activates."
    )

    @Parameter(title: "Wallpaper Collection")
    var collection: WallpaperCollectionEntity?

    var displayRepresentation: DisplayRepresentation {
        if let c = collection {
            return DisplayRepresentation(title: "Collection: \(c.name)")
        }
        return DisplayRepresentation(title: "Default Collection")
    }

    func perform() async throws -> some IntentResult {
        let suite = UserDefaults(suiteName: "group.com.wallp.app")
        suite?.set(collection?.id ?? "", forKey: "wallp_focus_active_collection")
        suite?.synchronize()

        // Wake up the running main app process immediately if possible.
        DistributedNotificationCenter.default().postNotificationName(
            NSNotification.Name("com.wallp.app.focusFilterChanged"),
            object: nil
        )
        return .result()
    }
}
