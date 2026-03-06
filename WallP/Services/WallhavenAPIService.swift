import Foundation

actor WallhavenAPIService {
    static let shared = WallhavenAPIService()

    private let baseURL = "https://wallhaven.cc/api/v1"
    private let session: URLSession

    private init() {
        let config = URLSessionConfiguration.default
        config.httpAdditionalHeaders = ["User-Agent": "WallP/1.0"]
        self.session = URLSession(configuration: config)
    }

    // MARK: - API Key Header

    private func authorizedRequest(for url: URL, apiKey: String) -> URLRequest {
        var request = URLRequest(url: url)
        if !apiKey.isEmpty {
            request.setValue(apiKey, forHTTPHeaderField: "X-API-Key")
        }
        return request
    }

    private func currentAPIKey() async -> String {
        await MainActor.run { AppSettings.shared.apiKey }
    }

    // MARK: - Fetch User Collections

    func fetchCollections(username: String) async throws -> [WallhavenCollection] {
        var urlString = "\(baseURL)/collections"
        if !username.isEmpty {
            urlString += "/\(username)"
        }
        guard let url = URL(string: urlString) else {
            throw WallhavenError.invalidURL
        }

        let apiKey = await currentAPIKey()
        let request = authorizedRequest(for: url, apiKey: apiKey)
        let (data, response) = try await session.data(for: request)
        try validateResponse(response)

        let decoded = try JSONDecoder().decode(WallhavenCollectionsResponse.self, from: data)
        return decoded.data
    }

    // MARK: - Fetch Collection Wallpapers

    func fetchCollectionWallpapers(
        username: String,
        collectionID: Int,
        page: Int = 1
    ) async throws -> WallhavenSearchResponse {
        guard let url = URL(string: "\(baseURL)/collections/\(username)/\(collectionID)?page=\(page)") else {
            throw WallhavenError.invalidURL
        }

        let apiKey = await currentAPIKey()
        let request = authorizedRequest(for: url, apiKey: apiKey)
        let (data, response) = try await session.data(for: request)
        try validateResponse(response)

        return try JSONDecoder().decode(WallhavenSearchResponse.self, from: data)
    }

    // MARK: - Fetch All Wallpapers from Collection (paginated)

    func fetchAllCollectionWallpapers(
        username: String,
        collectionID: Int,
        maxPages: Int = 10
    ) async throws -> [WallhavenWallpaper] {
        var allWallpapers: [WallhavenWallpaper] = []
        var page = 1

        while page <= maxPages {
            let response = try await fetchCollectionWallpapers(
                username: username,
                collectionID: collectionID,
                page: page
            )
            allWallpapers.append(contentsOf: response.data)

            if page >= response.meta.lastPage {
                break
            }
            page += 1

            // Respect rate limits: 45 req/min
            try await Task.sleep(for: .milliseconds(200))
        }

        return allWallpapers
    }

    // MARK: - Download Image Data

    func downloadImage(from urlString: String) async throws -> Data {
        guard let url = URL(string: urlString) else {
            throw WallhavenError.invalidURL
        }

        let apiKey = await currentAPIKey()
        let request = authorizedRequest(for: url, apiKey: apiKey)
        let (data, response) = try await session.data(for: request)
        try validateResponse(response)
        return data
    }

    // MARK: - Validate API Key

    func validateAPIKey() async throws -> Bool {
        guard let url = URL(string: "\(baseURL)/settings") else {
            throw WallhavenError.invalidURL
        }

        let apiKey = await currentAPIKey()
        let request = authorizedRequest(for: url, apiKey: apiKey)
        let (_, response) = try await session.data(for: request)

        if let httpResponse = response as? HTTPURLResponse {
            return httpResponse.statusCode == 200
        }
        return false
    }

    // MARK: - Response Validation

    private func validateResponse(_ response: URLResponse) throws {
        guard let httpResponse = response as? HTTPURLResponse else {
            throw WallhavenError.invalidResponse
        }

        switch httpResponse.statusCode {
        case 200...299:
            return
        case 401:
            throw WallhavenError.unauthorized
        case 429:
            throw WallhavenError.rateLimited
        case 404:
            throw WallhavenError.notFound
        default:
            throw WallhavenError.httpError(httpResponse.statusCode)
        }
    }
}

// MARK: - Errors

enum WallhavenError: LocalizedError {
    case invalidURL
    case invalidResponse
    case unauthorized
    case rateLimited
    case notFound
    case httpError(Int)

    var errorDescription: String? {
        switch self {
        case .invalidURL: "Invalid URL"
        case .invalidResponse: "Invalid server response"
        case .unauthorized: "Invalid API key. Check your key in Settings."
        case .rateLimited: "Rate limited by Wallhaven. Try again in a minute."
        case .notFound: "Resource not found on Wallhaven."
        case .httpError(let code): "HTTP error \(code)"
        }
    }
}
