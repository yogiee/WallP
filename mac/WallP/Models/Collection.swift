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

// MARK: - Focus Filter Bridge

/// Shared state between the main app and the sandboxed App Intents extension.
///
/// This deliberately does NOT use the `group.com.wallp.app` container. Since macOS 27,
/// System Policy denies ad-hoc-signed WallP writes there (kTCCServiceSystemPolicyAppData),
/// so writes silently never land. The extension reaches this folder through the
/// `temporary-exception.files.home-relative-path.read-write` entitlement instead; keep the
/// path in WallPAppIntentsExtension.entitlements in sync with `directory`.
enum FocusFilterBridge {
    struct CollectionOption: Codable, Equatable {
        let id: UUID
        let name: String
    }

    static let notificationName = "com.wallp.app.focusFilterChanged"

    static let directory: URL = {
        // Not FileManager/NSHomeDirectory: inside the sandbox those resolve to the
        // extension's own container, and the app and extension would disagree on the path.
        let home = getpwuid(getuid()).map { String(cString: $0.pointee.pw_dir) } ?? NSHomeDirectory()
        return URL(fileURLWithPath: home, isDirectory: true)
            .appendingPathComponent("Library/Application Support/WallP/FocusFilter", isDirectory: true)
    }()

    private static var collectionsURL: URL { directory.appendingPathComponent("collections.json") }
    private static var activeCollectionURL: URL { directory.appendingPathComponent("active_collection") }

    /// Main app: publishes the collection list for the Focus filter picker. Skips the write when
    /// nothing changed, because this runs on every collection mutation, including each download.
    static func publishCollections(_ collections: [WallPCollection]) throws {
        let options = collections.map { CollectionOption(id: $0.id, name: $0.name) }
        let encoder = JSONEncoder()
        encoder.outputFormatting = .sortedKeys
        let data = try encoder.encode(options)
        if (try? Data(contentsOf: collectionsURL)) == data { return }

        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        try data.write(to: collectionsURL, options: .atomic)
    }

    static func readCollections() throws -> [CollectionOption] {
        try JSONDecoder().decode([CollectionOption].self, from: Data(contentsOf: collectionsURL))
    }

    /// Extension: records the collection the active Focus selected. nil means "use the default".
    static func writeActiveCollection(_ id: UUID?) throws {
        try (id?.uuidString ?? "").write(to: activeCollectionURL, atomically: true, encoding: .utf8)
    }

    /// Main app: nil when no Focus override applies (file missing, empty, or unparseable).
    static func readActiveCollection() -> UUID? {
        guard let value = try? String(contentsOf: activeCollectionURL, encoding: .utf8) else { return nil }
        return UUID(uuidString: value)
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
        // Mirrors ImageCache.cacheDirectory — inlined so Collection.swift can compile
        // in the App Intents extension target without pulling in ImageCache.
        FileManager.default
            .urls(for: .applicationSupportDirectory, in: .userDomainMask)[0]
            .appendingPathComponent("WallP/cache", isDirectory: true)
            .appendingPathComponent(collectionID.uuidString)
            .appendingPathComponent(localFilename)
    }
}
