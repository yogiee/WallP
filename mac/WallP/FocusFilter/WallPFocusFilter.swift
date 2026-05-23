import AppIntents
import Foundation

// MARK: - Wallpaper Collection Entity

struct WallpaperCollectionEntity: AppEntity {
    static let typeDisplayRepresentation = TypeDisplayRepresentation(name: "Wallpaper Collection")
    static let defaultQuery = WallpaperCollectionQuery()

    let id: UUID
    let name: String

    var displayRepresentation: DisplayRepresentation {
        DisplayRepresentation(title: LocalizedStringResource(stringLiteral: name))
    }

    // AppEntity identity is the id alone — name changes must not create phantom selections.
    static func == (lhs: WallpaperCollectionEntity, rhs: WallpaperCollectionEntity) -> Bool {
        lhs.id == rhs.id
    }

    func hash(into hasher: inout Hasher) {
        hasher.combine(id)
    }
}

// Reads collections from the shared app group container.
struct WallpaperCollectionQuery: EntityQuery {
    private static let collectionsKey = "wallp_collections"
    private static let appDomain = "group.com.wallp.app"

    private func allCollections() -> [WallPCollection] {
        guard let data = UserDefaults(suiteName: Self.appDomain)?.data(forKey: Self.collectionsKey),
              let decoded = try? JSONDecoder().decode([WallPCollection].self, from: data)
        else { return [] }
        return decoded
    }

    func entities(for identifiers: [UUID]) async throws -> [WallpaperCollectionEntity] {
        allCollections()
            .filter { identifiers.contains($0.id) }
            .map { WallpaperCollectionEntity(id: $0.id, name: $0.name) }
    }

    func suggestedEntities() async throws -> [WallpaperCollectionEntity] {
        allCollections().map { WallpaperCollectionEntity(id: $0.id, name: $0.name) }
    }

    func defaultResult() async -> WallpaperCollectionEntity? {
        allCollections().first.map { WallpaperCollectionEntity(id: $0.id, name: $0.name) }
    }
}

// MARK: - Focus Filter Intent

struct WallPFocusFilter: SetFocusFilterIntent {
    static let title: LocalizedStringResource = "WallP Wallpaper Collection"
    static let description: IntentDescription? = IntentDescription(
        "Automatically switch wallpaper collection when this Focus activates."
    )

    // SetFocusFilterIntent requires all parameters to be Optional.
    @Parameter(title: "Wallpaper Collection")
    var collection: WallpaperCollectionEntity?

    var displayRepresentation: DisplayRepresentation {
        if let c = collection {
            return DisplayRepresentation(title: "Collection: \(c.name)")
        }
        return DisplayRepresentation(title: "Default Collection")
    }

    func perform() async throws -> some IntentResult {
        // UserDefaults(suiteName:) write is blocked by the sandbox
        // ("user-preference-write outside container" error).
        // Writing a plain file via FileManager uses file-write-data access,
        // which the app-group entitlement does grant.
        let value = collection?.id.uuidString ?? ""
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
