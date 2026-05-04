import AppIntents
import Foundation

// MARK: - Wallpaper Collection Entity for Focus Filter

struct WallpaperCollectionEntity: AppEntity {
    static let typeDisplayRepresentation = TypeDisplayRepresentation(name: "Wallpaper Collection")
    static let defaultQuery = WallpaperCollectionQuery()

    var id: String
    var name: String

    var displayRepresentation: DisplayRepresentation {
        DisplayRepresentation(title: LocalizedStringResource(stringLiteral: name))
    }
}

struct WallpaperCollectionQuery: EntityQuery {
    func entities(for identifiers: [String]) async throws -> [WallpaperCollectionEntity] {
        let collections = await MainActor.run { AppSettings.shared.collections }
        return collections
            .filter { identifiers.contains($0.id.uuidString) }
            .map { WallpaperCollectionEntity(id: $0.id.uuidString, name: $0.name) }
    }

    func suggestedEntities() async throws -> [WallpaperCollectionEntity] {
        let collections = await MainActor.run { AppSettings.shared.collections }
        return collections.map {
            WallpaperCollectionEntity(id: $0.id.uuidString, name: $0.name)
        }
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
        if let collectionIDString = collection?.id,
           let collectionID = UUID(uuidString: collectionIDString) {
            await MainActor.run {
                WallpaperRotator.shared.switchToCollection(collectionID)
            }
        } else {
            await MainActor.run {
                WallpaperRotator.shared.switchToDefaultCollection()
            }
        }
        return .result()
    }
}
