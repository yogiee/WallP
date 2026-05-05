using System.Diagnostics;
using System.IO;
using NetSparkleUpdater;
using NetSparkleUpdater.Enums;
using NetSparkleUpdater.Interfaces;
using NetSparkleUpdater.SignatureVerifiers;
using NetSparkleUpdater.UI.WPF;
using WallP.Models;

namespace WallP.Services;

public sealed class UpdaterService
{
    public const string AppcastUrl =
        "https://raw.githubusercontent.com/yogiee/WallP/main/appcast-windows.xml";

    /// <summary>
    /// Ed25519 public key used to verify NetSparkle update signatures.
    /// Distinct from the macOS Sparkle key — signing happens with the Windows-only
    /// private key stored at %LOCALAPPDATA%\netsparkle\NetSparkle_Ed25519.priv.
    /// </summary>
    public const string Ed25519PublicKey =
        "FKrg6FZMt458GyIIBREIvgA+q3iRzWDLh0Ncw19lYf8=";

    private readonly AppSettings _settings;
    private SparkleUpdater? _updater;

    public UpdaterService(AppSettings settings)
    {
        _settings = settings;
    }

    private SparkleUpdater Updater
    {
        get
        {
            if (_updater is null)
            {
                _updater = new SparkleUpdater(
                    AppcastUrl,
                    new Ed25519Checker(SecurityMode.Strict, Ed25519PublicKey))
                {
                    UIFactory = new UIFactory(),
                    LogWriter = new FileLogWriter(),
                };
                ApplyMode();
            }
            return _updater;
        }
    }

    /// <summary>
    /// Starts the periodic background check loop unless updates are disabled.
    /// Safe to call once at startup; no-op if already started.
    /// </summary>
    public void StartIfEnabled()
    {
        if (_settings.UpdateMode == UpdateMode.Disabled) return;
        try
        {
            Updater.StartLoop(true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WallP][Updater] StartLoop failed: {ex.Message}");
        }
    }

    public void ApplyMode()
    {
        if (_updater is null) return;
        _updater.UserInteractionMode = _settings.UpdateMode switch
        {
            UpdateMode.Auto => UserInteractionMode.DownloadAndInstall,
            UpdateMode.Ask => UserInteractionMode.DownloadNoInstall,
            _ => UserInteractionMode.NotSilent,
        };
    }

    /// <summary>
    /// Triggers a user-initiated update check. Surfaces NetSparkle's UI when
    /// an update is available; shows a "no update" dialog otherwise.
    /// </summary>
    public async Task CheckForUpdatesAtUserRequestAsync()
    {
        try
        {
            await Updater.CheckForUpdatesAtUserRequest();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WallP][Updater] Check failed: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Writes NetSparkle's diagnostics to %LOCALAPPDATA%\WallP\netsparkle.log so we can
    /// see what's happening when the published WinExe build fails — there's no console
    /// to log to in that mode.
    /// </summary>
    private sealed class FileLogWriter : ILogger
    {
        private static readonly string LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WallP",
            "netsparkle.log");

        private static readonly object Sync = new();

        public void PrintMessage(string message, params object[]? arguments)
        {
            try
            {
                var formatted = arguments is { Length: > 0 } ? string.Format(message, arguments) : message;
                var line = $"{DateTime.Now:HH:mm:ss.fff} {formatted}\n";
                lock (Sync)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                    File.AppendAllText(LogPath, line);
                }
            }
            catch { /* swallow */ }
        }
    }

    public void OnUpdateModeChanged()
    {
        // Mode changed — apply to existing updater if any, and start/stop the loop.
        ApplyMode();
        if (_settings.UpdateMode == UpdateMode.Disabled)
        {
            try { _updater?.StopLoop(); } catch { /* best-effort */ }
        }
        else if (_updater is null)
        {
            // Lazy-construct on first non-disabled use.
            StartIfEnabled();
        }
        else
        {
            try { Updater.StartLoop(false); } catch { /* may already be running */ }
        }
    }
}
