import SwiftUI

struct MenuBarPopover: View {
    @ObservedObject private var settings = AppSettings.shared
    @ObservedObject private var rotator = WallpaperRotator.shared
    @ObservedObject private var syncScheduler = SyncScheduler.shared
    @ObservedObject private var systemMonitor = SystemStateMonitor.shared
    @Environment(\.openSettings) private var openSettings

    var body: some View {
        VStack(spacing: 12) {
            // Header
            HStack {
                Text("WallP")
                    .font(.headline)
                Spacer()
                statusIndicator
            }

            Divider()

            // Active collection picker
            if settings.collections.count > 1 {
                HStack {
                    Image(systemName: "folder")
                        .foregroundStyle(.secondary)
                    Picker("Collection", selection: collectionBinding) {
                        ForEach(settings.collections) { collection in
                            Text(collection.name).tag(collection.id)
                        }
                    }
                    .labelsHidden()
                    .pickerStyle(.menu)
                    .font(.caption)
                    Spacer()
                    if let id = settings.defaultCollectionID {
                        Text("\(settings.imagesForCollection(id).count) images")
                            .font(.caption2)
                            .foregroundStyle(.secondary)
                    }
                }
            } else if let collection = settings.activeCollection {
                HStack {
                    Image(systemName: "folder")
                        .foregroundStyle(.secondary)
                    Text(collection.name)
                        .font(.caption)
                    Spacer()
                    Text("\(settings.imagesForCollection(collection.id).count) images")
                        .font(.caption2)
                        .foregroundStyle(.secondary)
                }
            } else {
                HStack {
                    Image(systemName: "folder.badge.questionmark")
                        .foregroundStyle(.orange)
                    Text("No collection configured")
                        .font(.caption)
                        .foregroundStyle(.orange)
                    Spacer()
                }
            }

            Divider()

            // Controls with Liquid Glass
            GlassEffectContainer {
                HStack {
                    Button(action: { rotator.nextWallpaper() }) {
                        Image(systemName: "shuffle")
                            .frame(width: 28, height: 28)
                    }
                    .buttonStyle(.glass)
                    .buttonBorderShape(.circle)
                    .help("Shuffle wallpaper")
                    .disabled(settings.cachedImages.isEmpty)

                    Spacer()

                    Button(action: {
                        if settings.isPaused {
                            rotator.resume()
                        } else {
                            rotator.pause()
                        }
                    }) {
                        Image(systemName: settings.isPaused ? "play.fill" : "pause.fill")
                            .frame(width: 28, height: 28)
                    }
                    .buttonStyle(.glassProminent)
                    .buttonBorderShape(.circle)
                    .help(settings.isPaused ? "Resume" : "Pause")
                    .disabled(settings.cachedImages.isEmpty)

                    Spacer()

                    Button(action: {
                        Task { await syncScheduler.syncNow() }
                    }) {
                        Group {
                            if syncScheduler.isSyncing {
                                ProgressView()
                                    .controlSize(.small)
                            } else {
                                Image(systemName: "arrow.clockwise")
                            }
                        }
                        .frame(width: 28, height: 28)
                    }
                    .buttonStyle(.glass)
                    .buttonBorderShape(.circle)
                    .disabled(syncScheduler.isSyncing || settings.collections.isEmpty)
                    .help("Sync from Wallhaven")
                }
            }

            // Sync progress
            if syncScheduler.isSyncing, !syncScheduler.syncProgress.isEmpty {
                HStack {
                    ProgressView()
                        .controlSize(.small)
                    Text(syncScheduler.syncProgress)
                        .font(.caption2)
                        .foregroundStyle(.secondary)
                        .lineLimit(1)
                }
            } else if !syncScheduler.syncProgress.isEmpty {
                Text(syncScheduler.syncProgress)
                    .font(.caption2)
                    .foregroundStyle(.secondary)
                    .lineLimit(1)
            }

            // System state warnings
            if systemMonitor.shouldPause {
                HStack {
                    Image(systemName: "pause.circle")
                        .foregroundStyle(.orange)
                    Text("Paused: \(pauseReason)")
                        .font(.caption2)
                        .foregroundStyle(.secondary)
                }
            }

            if let error = syncScheduler.lastSyncError {
                HStack {
                    Image(systemName: "exclamationmark.triangle")
                        .foregroundStyle(.red)
                    Text(error)
                        .font(.caption2)
                        .foregroundStyle(.red)
                        .lineLimit(2)
                }
            }

            // Setup prompt if needed
            if settings.apiKey.isEmpty || settings.collections.isEmpty {
                Divider()
                HStack {
                    Image(systemName: "info.circle")
                        .foregroundStyle(.blue)
                    if settings.apiKey.isEmpty {
                        Text("Enter your Wallhaven API key in Settings to get started.")
                            .font(.caption2)
                    } else {
                        Text("Add a Wallhaven collection in Settings to get started.")
                            .font(.caption2)
                    }
                }
            }

            Divider()

            // Bottom actions
            HStack {
                Button("Settings...") {
                    openSettings()
                    NSApp.activate()
                }
                .buttonStyle(.glass)
                .font(.caption)

                Spacer()

                Button("Quit") {
                    NSApplication.shared.terminate(nil)
                }
                .buttonStyle(.glass)
                .font(.caption)
            }
        }
        .padding(16)
        .frame(width: 280)
    }

    // MARK: - Helpers

    private var collectionBinding: Binding<UUID> {
        Binding(
            get: { settings.defaultCollectionID ?? (settings.collections.first?.id ?? UUID()) },
            set: { rotator.switchToCollection($0) }
        )
    }

    private var statusIndicator: some View {
        HStack(spacing: 4) {
            Circle()
                .fill(statusColor)
                .frame(width: 8, height: 8)
            Text(statusText)
                .font(.caption2)
                .foregroundStyle(.secondary)
        }
        .padding(.horizontal, 8)
        .padding(.vertical, 4)
        .glassEffect(.regular, in: .capsule)
    }

    private var statusColor: Color {
        if syncScheduler.isSyncing { return .blue }
        if systemMonitor.shouldPause { return .orange }
        if settings.isPaused { return .orange }
        if rotator.isRunning { return .green }
        return .gray
    }

    private var statusText: String {
        if syncScheduler.isSyncing { return "Syncing..." }
        if systemMonitor.shouldPause { return "Auto-paused" }
        if settings.isPaused { return "Paused" }
        if rotator.isRunning { return "Running" }
        if settings.collections.isEmpty { return "No collections" }
        return "Stopped"
    }

    private var pauseReason: String {
        if systemMonitor.isSystemSleeping { return "System sleeping" }
        if systemMonitor.isScreenLocked { return "Screen locked" }
        if systemMonitor.isScreenOff { return "Display off" }
        return ""
    }
}
