import Foundation

// MARK: - Local Collection Model

struct WallPCollection: Codable, Identifiable, Hashable {
    let id: UUID
    var name: String
    var wallhavenCollectionID: Int
    var wallhavenUsername: String
    var lastSynced: Date?
    var cachedImageIDs: [String]

    init(
        id: UUID = UUID(),
        name: String,
        wallhavenCollectionID: Int,
        wallhavenUsername: String,
        lastSynced: Date? = nil,
        cachedImageIDs: [String] = []
    ) {
        self.id = id
        self.name = name
        self.wallhavenCollectionID = wallhavenCollectionID
        self.wallhavenUsername = wallhavenUsername
        self.lastSynced = lastSynced
        self.cachedImageIDs = cachedImageIDs
    }

    func hash(into hasher: inout Hasher) {
        hasher.combine(id)
    }

    static func == (lhs: WallPCollection, rhs: WallPCollection) -> Bool {
        lhs.id == rhs.id
    }
}

// MARK: - Cached Image Metadata

struct CachedImage: Codable, Identifiable {
    let id: String
    let wallhavenID: String
    let originalURL: String
    let localFilename: String
    let width: Int
    let height: Int
    let fileSize: Int64
    let dateAdded: Date
    let collectionID: UUID

    var localURL: URL {
        ImageCache.cacheDirectory
            .appendingPathComponent(collectionID.uuidString)
            .appendingPathComponent(localFilename)
    }
}
