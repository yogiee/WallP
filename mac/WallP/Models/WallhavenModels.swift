import Foundation

// MARK: - Wallhaven API Response Types

struct WallhavenSearchResponse: Codable {
    let data: [WallhavenWallpaper]
    let meta: WallhavenMeta
}

struct WallhavenWallpaper: Codable, Identifiable {
    let id: String
    let url: String
    let shortURL: String?
    let views: Int
    let favorites: Int
    let source: String
    let purity: String
    let category: String
    let dimensionX: Int
    let dimensionY: Int
    let resolution: String
    let ratio: String
    let fileSize: Int
    let fileType: String
    let createdAt: String
    let colors: [String]
    let path: String
    let thumbs: WallhavenThumbs

    enum CodingKeys: String, CodingKey {
        case id, url, views, favorites, source, purity, category
        case shortURL = "short_url"
        case dimensionX = "dimension_x"
        case dimensionY = "dimension_y"
        case resolution, ratio
        case fileSize = "file_size"
        case fileType = "file_type"
        case createdAt = "created_at"
        case colors, path, thumbs
    }
}

struct WallhavenThumbs: Codable {
    let large: String
    let original: String
    let small: String
}

struct WallhavenMeta: Codable {
    let currentPage: Int
    let lastPage: Int
    let perPage: Int
    let total: Int
    let query: String?
    let seed: String?

    enum CodingKeys: String, CodingKey {
        case currentPage = "current_page"
        case lastPage = "last_page"
        case perPage = "per_page"
        case total, query, seed
    }
}

// MARK: - Collections

struct WallhavenCollectionsResponse: Codable {
    let data: [WallhavenCollection]
}

struct WallhavenCollection: Codable, Identifiable {
    let id: Int
    let label: String
    let views: Int
    let `public`: Int
    let count: Int
}

// MARK: - User Settings

struct WallhavenUserSettings: Codable {
    let thumbSize: String?
    let perPage: String?
    let purity: [String]?
    let categories: [String]?
    let resolutions: [String]?
    let aspectRatios: [String]?
    let toplistRange: String?
    let tagBlacklist: [String]?
    let userBlacklist: [String]?

    enum CodingKeys: String, CodingKey {
        case thumbSize = "thumb_size"
        case perPage = "per_page"
        case purity, categories, resolutions
        case aspectRatios = "aspect_ratios"
        case toplistRange = "toplist_range"
        case tagBlacklist = "tag_blacklist"
        case userBlacklist = "user_blacklist"
    }
}
