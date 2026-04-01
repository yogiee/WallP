import ServiceManagement
import SwiftUI

struct SettingsView: View {
    @ObservedObject private var settings = AppSettings.shared
    @ObservedObject private var syncScheduler = SyncScheduler.shared

    @State private var isValidatingKey = false
    @State private var keyValidationResult: Bool?
    @State private var showAddCollection = false
    @State private var availableCollections: [WallhavenCollection] = []
    @State private var isFetchingCollections = false
    @State private var fetchError: String?
    @State private var cacheSize: String = "Calculating..."
    @State private var selectedWallhavenCollection: Int?

    var body: some View {
        TabView {
            generalTab
                .tabItem { Label("General", systemImage: "gear") }

            collectionsTab
                .tabItem { Label("Collections", systemImage: "folder") }

            timingTab
                .tabItem { Label("Timing", systemImage: "clock") }

            cacheTab
                .tabItem { Label("Cache", systemImage: "internaldrive") }

            updatesTab
                .tabItem { Label("Updates", systemImage: "arrow.down.circle") }
        }
        .frame(width: 500, height: 420)
        .onAppear {
            refreshCacheSize()
        }
    }

    // MARK: - General Tab

    private var generalTab: some View {
        Form {
            Section("Wallhaven Account") {
                SecureField("API Key", text: $settings.apiKey)
                    .textFieldStyle(.roundedBorder)

                TextField("Username", text: $settings.wallhavenUsername)
                    .textFieldStyle(.roundedBorder)

                HStack {
                    Button("Validate API Key") {
                        validateAPIKey()
                    }
                    .disabled(settings.apiKey.isEmpty || isValidatingKey)

                    if isValidatingKey {
                        ProgressView()
                            .controlSize(.small)
                    }

                    if let result = keyValidationResult {
                        Image(systemName: result ? "checkmark.circle.fill" : "xmark.circle.fill")
                            .foregroundStyle(result ? .green : .red)
                    }
                }
            }

            Section("Startup") {
                Toggle("Launch at login", isOn: launchAtLoginBinding)
            }

            Section("Pause Wallpaper Changes") {
                Toggle("When system is sleeping", isOn: $settings.pauseOnSleep)
                Toggle("When screen is locked", isOn: $settings.pauseOnLock)
                Toggle("When display is off", isOn: $settings.pauseOnScreenOff)
            }

            Section("Focus Mode") {
                Text("To use different collections per Focus mode, go to:")
                    .font(.caption)
                    .foregroundStyle(.secondary)
                Text("System Settings \u{2192} Focus \u{2192} [Mode] \u{2192} Add Filter \u{2192} WallP")
                    .font(.caption)
                    .foregroundStyle(.secondary)
                    .textSelection(.enabled)
            }
        }
        .formStyle(.grouped)
        .padding()
    }

    // MARK: - Collections Tab

    private var collectionsTab: some View {
        VStack(spacing: 0) {
            List {
                ForEach(settings.collections) { collection in
                    CollectionRow(collection: collection) {
                        deleteCollection(collection)
                    }
                }
            }

            Divider()

            HStack {
                Button(action: { fetchAvailableCollections() }) {
                    Label("Add from Wallhaven", systemImage: "plus")
                }
                .disabled(settings.apiKey.isEmpty || settings.wallhavenUsername.isEmpty || isFetchingCollections)

                if isFetchingCollections {
                    ProgressView()
                        .controlSize(.small)
                }

                Spacer()

                if let defaultID = settings.defaultCollectionID,
                   let name = settings.collections.first(where: { $0.id == defaultID })?.name {
                    Text("Default: \(name)")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }
            }
            .padding(12)

            if !availableCollections.isEmpty {
                Divider()
                availableCollectionsView
            }

            if let error = fetchError {
                Text(error)
                    .font(.caption)
                    .foregroundStyle(.red)
                    .padding(.horizontal)
            }
        }
    }

    private var availableCollectionsView: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text("Available Wallhaven Collections:")
                .font(.caption)
                .fontWeight(.medium)
                .padding(.horizontal, 12)

            HStack {
                Picker("Collection", selection: $selectedWallhavenCollection) {
                    Text("Select a collection…").tag(Int?.none)
                    ForEach(availableCollections) { whCollection in
                        let alreadyAdded = settings.collections.contains {
                            $0.wallhavenCollectionID == whCollection.id
                        }
                        Text("\(whCollection.label) (\(whCollection.count) wallpapers)\(alreadyAdded ? " ✓" : "")")
                            .tag(Int?.some(whCollection.id))
                    }
                }
                .labelsHidden()

                Button("Add") {
                    if let selectedID = selectedWallhavenCollection,
                       let collection = availableCollections.first(where: { $0.id == selectedID }) {
                        addCollection(collection)
                        selectedWallhavenCollection = nil
                    }
                }
                .disabled(selectedWallhavenCollection == nil || isSelectedCollectionAlreadyAdded)
            }
            .padding(.horizontal, 12)
        }
        .padding(.bottom, 8)
    }

    private var isSelectedCollectionAlreadyAdded: Bool {
        guard let selectedID = selectedWallhavenCollection else { return false }
        return settings.collections.contains { $0.wallhavenCollectionID == selectedID }
    }

    // MARK: - Timing Tab

    private var timingTab: some View {
        Form {
            Section("Wallpaper Rotation") {
                Picker("Change wallpaper", selection: $settings.rotationInterval) {
                    ForEach(RotationInterval.allCases, id: \.self) { interval in
                        Text(interval.displayName).tag(interval)
                    }
                }

                Picker("Display order", selection: $settings.displayOrder) {
                    ForEach(DisplayOrder.allCases, id: \.self) { order in
                        Text(order.displayName).tag(order)
                    }
                }
            }

            Section("Wallhaven Sync") {
                Picker("Check for new wallpapers", selection: $settings.syncInterval) {
                    ForEach(SyncInterval.allCases, id: \.self) { interval in
                        Text(interval.displayName).tag(interval)
                    }
                }

                HStack {
                    Button("Sync Now") {
                        Task { await syncScheduler.syncNow() }
                    }
                    .disabled(syncScheduler.isSyncing)

                    if syncScheduler.isSyncing {
                        ProgressView()
                            .controlSize(.small)
                        Text("Syncing...")
                            .font(.caption)
                            .foregroundStyle(.secondary)
                    }
                }
            }

            Section("Default Collection") {
                Picker("Default collection", selection: defaultCollectionBinding) {
                    Text("None").tag(UUID?.none)
                    ForEach(settings.collections) { collection in
                        Text(collection.name).tag(UUID?.some(collection.id))
                    }
                }
            }
        }
        .formStyle(.grouped)
        .padding()
    }

    // MARK: - Cache Tab

    private var cacheTab: some View {
        Form {
            Section("Image Optimization") {
                Toggle("Optimize cached images (HEIC + downscale)", isOn: $settings.optimizeImages)
                Text("Converts images to HEIC format and downscales to screen resolution. Typically saves 85-94% disk space.")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }

            Section("Cache Limits") {
                Picker("Max images per collection", selection: $settings.cacheLimit) {
                    ForEach(CacheLimit.allCases, id: \.self) { limit in
                        Text(limit.displayName).tag(limit)
                    }
                }
            }

            Section("Storage") {
                HStack {
                    Text("Cache size:")
                    Spacer()
                    Text(cacheSize)
                        .foregroundStyle(.secondary)
                }

                HStack {
                    Text("Total cached images:")
                    Spacer()
                    Text("\(settings.cachedImages.count)")
                        .foregroundStyle(.secondary)
                }

                Button("Clear All Cache", role: .destructive) {
                    clearCache()
                }
            }
        }
        .formStyle(.grouped)
        .padding()
    }

    // MARK: - Updates Tab

    private var updatesTab: some View {
        Form {
            Section("Automatic Updates") {
                Picker("Update behavior", selection: Binding(
                    get: { UpdaterService.shared.updateMode },
                    set: { UpdaterService.shared.updateMode = $0 }
                )) {
                    Text("Auto-update (recommended)").tag(0)
                    Text("Download updates, ask before installing").tag(1)
                    Text("Disabled").tag(2)
                }
                .pickerStyle(.radioGroup)
            }

            Section {
                Button("Check for Updates…") {
                    UpdaterService.shared.checkForUpdates()
                }
            }
        }
        .formStyle(.grouped)
        .padding()
    }

    // MARK: - Actions

    private func validateAPIKey() {
        isValidatingKey = true
        keyValidationResult = nil
        Task {
            do {
                let valid = try await WallhavenAPIService.shared.validateAPIKey()
                await MainActor.run {
                    keyValidationResult = valid
                    isValidatingKey = false
                }
            } catch {
                await MainActor.run {
                    keyValidationResult = false
                    isValidatingKey = false
                }
            }
        }
    }

    private func fetchAvailableCollections() {
        isFetchingCollections = true
        fetchError = nil
        Task {
            do {
                let collections = try await WallhavenAPIService.shared.fetchCollections(
                    username: settings.wallhavenUsername
                )
                await MainActor.run {
                    availableCollections = collections
                    isFetchingCollections = false
                }
            } catch {
                await MainActor.run {
                    fetchError = error.localizedDescription
                    isFetchingCollections = false
                }
            }
        }
    }

    private func addCollection(_ whCollection: WallhavenCollection) {
        let newCollection = WallPCollection(
            name: whCollection.label,
            wallhavenCollectionID: whCollection.id,
            wallhavenUsername: settings.wallhavenUsername
        )
        settings.collections.append(newCollection)

        if settings.defaultCollectionID == nil {
            settings.defaultCollectionID = newCollection.id
        }

        // Auto-sync the newly added collection
        Task {
            await syncScheduler.syncCollection(newCollection.id)
        }
    }

    private func deleteCollection(_ collection: WallPCollection) {
        settings.cachedImages.removeAll { $0.collectionID == collection.id }
        Task {
            await ImageCache.shared.clearCache(for: collection.id)
        }
        settings.collections.removeAll { $0.id == collection.id }

        // If we deleted the default collection, pick the first remaining one
        if settings.defaultCollectionID == collection.id {
            settings.defaultCollectionID = settings.collections.first?.id
        }
    }

    private func clearCache() {
        Task {
            await ImageCache.shared.clearAllCache()
        }
        settings.cachedImages.removeAll()
        for i in settings.collections.indices {
            settings.collections[i].cachedImageIDs.removeAll()
        }
        refreshCacheSize()
    }

    private func refreshCacheSize() {
        Task {
            let size = await ImageCache.shared.formattedCacheSize()
            await MainActor.run { cacheSize = size }
        }
    }

    private var launchAtLoginBinding: Binding<Bool> {
        Binding(
            get: { SMAppService.mainApp.status == .enabled },
            set: { newValue in
                do {
                    if newValue {
                        try SMAppService.mainApp.register()
                    } else {
                        try SMAppService.mainApp.unregister()
                    }
                } catch {
                    print("[WallP] Launch at login error: \(error)")
                }
            }
        )
    }

    private var defaultCollectionBinding: Binding<UUID?> {
        Binding(
            get: { settings.defaultCollectionID },
            set: { newValue in
                if let id = newValue {
                    WallpaperRotator.shared.switchToCollection(id)
                } else {
                    settings.defaultCollectionID = nil
                }
            }
        )
    }
}

// MARK: - Collection Row

struct CollectionRow: View {
    let collection: WallPCollection
    let onDelete: () -> Void
    @ObservedObject private var settings = AppSettings.shared
    @State private var showDeleteConfirmation = false

    var body: some View {
        HStack {
            VStack(alignment: .leading, spacing: 2) {
                HStack {
                    Text(collection.name)
                        .fontWeight(.medium)
                    if collection.id == settings.defaultCollectionID {
                        Text("DEFAULT")
                            .font(.caption2)
                            .fontWeight(.bold)
                            .padding(.horizontal, 6)
                            .padding(.vertical, 2)
                            .glassEffect(.regular.tint(.blue), in: .capsule)
                    }
                }

                HStack(spacing: 12) {
                    Text("Wallhaven #\(collection.wallhavenCollectionID)")
                        .font(.caption)
                        .foregroundStyle(.secondary)

                    Text("\(settings.imagesForCollection(collection.id).count) cached")
                        .font(.caption)
                        .foregroundStyle(.secondary)

                    if let lastSynced = collection.lastSynced {
                        Text("Synced \(lastSynced.formatted(.relative(presentation: .named)))")
                            .font(.caption)
                            .foregroundStyle(.secondary)
                    }
                }
            }

            Spacer()

            if collection.id != settings.defaultCollectionID {
                Button("Set Default") {
                    WallpaperRotator.shared.switchToCollection(collection.id)
                }
                .buttonStyle(.glass)
                .font(.caption)
            }

            Button(role: .destructive) {
                showDeleteConfirmation = true
            } label: {
                Image(systemName: "trash")
            }
            .buttonStyle(.borderless)
            .font(.caption)
            .help("Remove collection")
            .confirmationDialog(
                "Remove \"\(collection.name)\"?",
                isPresented: $showDeleteConfirmation
            ) {
                Button("Remove", role: .destructive) {
                    onDelete()
                }
            } message: {
                Text("This will remove the collection and delete all cached images for it.")
            }
        }
        .padding(.vertical, 4)
    }
}
