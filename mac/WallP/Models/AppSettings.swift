import Foundation
import SwiftUI

// MARK: - Enums

enum SyncInterval: Int, CaseIterable, Codable {
    case oneHour = 3600
    case twoHours = 7200
    case fourHours = 14400
    case eightHours = 28800
    case manual = 0

    var displayName: String {
        switch self {
        case .oneHour: "Every 1 hour"
        case .twoHours: "Every 2 hours"
        case .fourHours: "Every 4 hours"
        case .eightHours: "Every 8 hours"
        case .manual: "Manual only"
        }
    }
}

enum RotationInterval: Int, CaseIterable, Codable {
    case fiveMinutes = 300
    case fifteenMinutes = 900
    case thirtyMinutes = 1800
    case oneHour = 3600
    case twoHours = 7200
    case fourHours = 14400

    var displayName: String {
        switch self {
        case .fiveMinutes: "Every 5 minutes"
        case .fifteenMinutes: "Every 15 minutes"
        case .thirtyMinutes: "Every 30 minutes"
        case .oneHour: "Every 1 hour"
        case .twoHours: "Every 2 hours"
        case .fourHours: "Every 4 hours"
        }
    }
}

enum DisplayOrder: String, CaseIterable, Codable {
    case random = "random"
    case name = "name"
    case dateCreated = "dateCreated"

    var displayName: String {
        switch self {
        case .random: "Random / Shuffle"
        case .name: "By Name"
        case .dateCreated: "By Date Created"
        }
    }
}

enum CacheLimit: Int, CaseIterable, Codable {
    case fifty = 50
    case hundred = 100
    case twoHundred = 200
    case fiveHundred = 500

    var displayName: String {
        "\(rawValue) images"
    }
}

// MARK: - App Settings (persisted via UserDefaults)

@MainActor
final class AppSettings: ObservableObject {
    static let shared = AppSettings()

    private let defaults = UserDefaults.standard
    private let collectionsKey = "wallp_collections"
    private let cachedImagesKey = "wallp_cached_images"

    @AppStorage("wallp_api_key") var apiKey: String = ""
    @AppStorage("wallp_username") var wallhavenUsername: String = ""

    @Published var syncInterval: SyncInterval {
        didSet { defaults.set(syncInterval.rawValue, forKey: "wallp_sync_interval") }
    }
    @Published var rotationInterval: RotationInterval {
        didSet { defaults.set(rotationInterval.rawValue, forKey: "wallp_rotation_interval") }
    }
    @Published var displayOrder: DisplayOrder {
        didSet { defaults.set(displayOrder.rawValue, forKey: "wallp_display_order") }
    }
    @Published var cacheLimit: CacheLimit {
        didSet { defaults.set(cacheLimit.rawValue, forKey: "wallp_cache_limit") }
    }

    @Published var defaultCollectionID: UUID? {
        didSet { defaults.set(defaultCollectionID?.uuidString, forKey: "wallp_default_collection") }
    }

    @Published var pauseOnSleep: Bool {
        didSet { defaults.set(pauseOnSleep, forKey: "wallp_pause_sleep") }
    }
    @Published var pauseOnLock: Bool {
        didSet { defaults.set(pauseOnLock, forKey: "wallp_pause_lock") }
    }
    @Published var pauseOnScreenOff: Bool {
        didSet { defaults.set(pauseOnScreenOff, forKey: "wallp_pause_screen_off") }
    }
    @Published var optimizeImages: Bool {
        didSet { defaults.set(optimizeImages, forKey: "wallp_optimize_images") }
    }

    @Published var isPaused: Bool = false

    @Published var collections: [WallPCollection] {
        didSet { saveCollections() }
    }

    @Published var cachedImages: [CachedImage] {
        didSet { saveCachedImages() }
    }

    private init() {
        self.syncInterval = SyncInterval(rawValue: defaults.integer(forKey: "wallp_sync_interval")) ?? .fourHours
        self.rotationInterval = RotationInterval(rawValue: defaults.integer(forKey: "wallp_rotation_interval")) ?? .thirtyMinutes
        self.displayOrder = DisplayOrder(rawValue: defaults.string(forKey: "wallp_display_order") ?? "") ?? .random
        self.cacheLimit = CacheLimit(rawValue: defaults.integer(forKey: "wallp_cache_limit")) ?? .hundred
        self.pauseOnSleep = defaults.object(forKey: "wallp_pause_sleep") as? Bool ?? true
        self.pauseOnLock = defaults.object(forKey: "wallp_pause_lock") as? Bool ?? true
        self.pauseOnScreenOff = defaults.object(forKey: "wallp_pause_screen_off") as? Bool ?? true
        self.optimizeImages = defaults.object(forKey: "wallp_optimize_images") as? Bool ?? true

        if let idStr = defaults.string(forKey: "wallp_default_collection") {
            self.defaultCollectionID = UUID(uuidString: idStr)
        } else {
            self.defaultCollectionID = nil
        }

        self.collections = []
        self.cachedImages = []
        self.collections = loadCollections()
        self.cachedImages = loadCachedImages()
    }

    // MARK: - Persistence

    private func saveCollections() {
        if let data = try? JSONEncoder().encode(collections) {
            defaults.set(data, forKey: collectionsKey)
            // Mirror to shared app group so the App Intents extension can read it
            UserDefaults(suiteName: "group.com.wallp.app")?.set(data, forKey: collectionsKey)
        }
    }

    private func loadCollections() -> [WallPCollection] {
        guard let data = defaults.data(forKey: collectionsKey),
              let collections = try? JSONDecoder().decode([WallPCollection].self, from: data)
        else { return [] }
        return collections
    }

    private func saveCachedImages() {
        if let data = try? JSONEncoder().encode(cachedImages) {
            defaults.set(data, forKey: cachedImagesKey)
        }
    }

    private func loadCachedImages() -> [CachedImage] {
        guard let data = defaults.data(forKey: cachedImagesKey),
              let images = try? JSONDecoder().decode([CachedImage].self, from: data)
        else { return [] }
        return images
    }

    // MARK: - Helpers

    func imagesForCollection(_ collectionID: UUID) -> [CachedImage] {
        cachedImages.filter { $0.collectionID == collectionID }
    }

    var activeCollection: WallPCollection? {
        if let id = defaultCollectionID {
            return collections.first { $0.id == id }
        }
        return collections.first
    }
}
