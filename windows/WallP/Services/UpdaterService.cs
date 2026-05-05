using System.Diagnostics;
using System.IO;
using System.Windows.Media;
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
                    UIFactory = BuildUIFactory(),
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

    private static UIFactory BuildUIFactory()
    {
        // Dark Mica-ish background and a dark-themed release-notes HTML so the
        // update dialog stops clashing with the rest of the app.
        var darkPanel = new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x20));
        darkPanel.Freeze();

        return new UIFactory
        {
            UseStaticUpdateWindowBackgroundColor = true,
            UpdateWindowGridBackgroundBrush = darkPanel,
            ReleaseNotesHTMLTemplate = DarkReleaseNotesTemplate,
            ProcessWindowAfterInit = ApplyDarkTheme,
        };
    }

    /// <summary>
    /// Themed HTML used as the per-item release-notes block. NetSparkle calls
    /// <c>string.Format(template, title, date, body, accentColor)</c>, so the
    /// placeholders are {0}=title, {1}=date, {2}=body, {3}=accent color and
    /// every other brace would break parsing — hence inline styles only.
    /// </summary>
    private const string DarkReleaseNotesTemplate =
        "<html><head><meta charset=\"utf-8\"></head>" +
        "<body style=\"font-family:'Segoe UI',sans-serif;background:#1F1F1F;color:#E6E6E6;margin:0;padding:12px;\">" +
        "<div style=\"background:#2B2B2B;border-left:4px solid {3};padding:8px 12px;margin-bottom:8px;border-radius:4px;display:flex;justify-content:space-between;align-items:center;\">" +
        "<span style=\"font-weight:600;font-size:14px;\">{0}</span>" +
        "<span style=\"color:#9A9A9A;font-size:12px;\">{1}</span>" +
        "</div>" +
        "<div style=\"padding:4px 12px 12px;line-height:1.5;\">{2}</div>" +
        "</body></html>";

    /// <summary>
    /// Applies a dark background + foreground to NetSparkle-created Windows so they
    /// don't render with the system-light defaults that clash with our app theme.
    /// </summary>
    private static void ApplyDarkTheme(System.Windows.Window window, UIFactory factory)
    {
        var bg = new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x20));
        var fg = new SolidColorBrush(Color.FromRgb(0xE6, 0xE6, 0xE6));
        bg.Freeze();
        fg.Freeze();
        window.Background = bg;
        window.Foreground = fg;
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
