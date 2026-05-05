using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Appearance;
using WallP.Helpers;
using WallP.Models;
using WallP.Services;
using WallP.Views;
using WallP.Views.Settings;

namespace WallP;

public partial class App : Application
{
    private IHost? _host;

    public static IServiceProvider Services =>
        ((App)Current)._host?.Services
            ?? throw new InvalidOperationException("Host not initialized.");

    private static readonly string CrashLogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WallP",
        "crash.log");

    protected override async void OnStartup(StartupEventArgs e)
    {
        // Catch everything so silent crashes leave a trace on disk and show a dialog
        // before the process dies. Without these the published .exe just vanishes.
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex) LogCrash(ex, "AppDomain");
        };
        DispatcherUnhandledException += (_, args) =>
        {
            LogCrash(args.Exception, "Dispatcher");
            ShowCrashDialog(args.Exception);
            args.Handled = true; // keep the app alive after a UI-thread error
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            LogCrash(args.Exception, "Task");
            args.SetObserved();
        };

        base.OnStartup(e);

        // Setting <ui:ThemesDictionary Theme="Dark"/> in XAML only seeds the resource
        // dictionary; it doesn't push the theme through Frame-hosted content like
        // NavigationView's pages. The manager API does, and also tracks the system
        // accent color for the title bar.
        ApplicationThemeManager.Apply(ApplicationTheme.Dark);

        try
        {
            _host = Host.CreateDefaultBuilder()
                .ConfigureServices(ConfigureServices)
                .Build();

            await _host.StartAsync();

            var trayHost = Services.GetRequiredService<TrayIconHost>();
            trayHost.Show();

            var settings = Services.GetRequiredService<AppSettings>();
            var sync = Services.GetRequiredService<SyncScheduler>();
            var rotator = Services.GetRequiredService<WallpaperRotator>();

            // As soon as sync caches its first image, start the rotator and apply it.
            // Without this, a fresh setup would wait the entire sync before showing
            // any wallpaper. SyncScheduler.ImageCached fires from the sync's
            // thread-pool task, so we marshal to the dispatcher.
            sync.ImageCached += (_, _) =>
            {
                Dispatcher.BeginInvoke(() =>
                {
                    if (!rotator.IsRunning && !settings.IsPaused)
                    {
                        rotator.Start();
                        _ = rotator.NextWallpaperAsync();
                    }
                });
            };

            // Sync-complete toast — only when the user has it on and at least one
            // new image was synced. Errors get a separate toast so they're not silent.
            var notifications = Services.GetRequiredService<NotificationService>();
            sync.SyncCompleted += (_, args) =>
            {
                if (!settings.ShowSyncCompleteToast) return;
                if (args.Error is { Length: > 0 } err)
                {
                    notifications.ShowSyncFailed(err);
                }
                else
                {
                    notifications.ShowSyncComplete(args.NewImageCount);
                }
            };

            sync.Start();

            // System-driven pauses: stop the rotator when Windows locks the session or
            // suspends, restart when it returns. Doesn't touch settings.IsPaused — that's
            // the user-facing toggle, this is system state. SystemEvents callbacks fire
            // on a private thread, so we marshal to the UI dispatcher before touching the
            // rotator's INotifyPropertyChanged-driven state.
            var systemMonitor = Services.GetRequiredService<SystemStateMonitor>();
            void Suspend() => Dispatcher.BeginInvoke(rotator.Stop);
            void Resume() => Dispatcher.BeginInvoke(rotator.Start);
            systemMonitor.SessionLocked += (_, _) => Suspend();
            systemMonitor.Suspending += (_, _) => Suspend();
            systemMonitor.SessionUnlocked += (_, _) => Resume();
            systemMonitor.Resumed += (_, _) => Resume();
            systemMonitor.Start();

            // Pause-condition enforcement: fullscreen-app or on-battery (per the user
            // toggles in General). Stops the rotator while any reason applies, starts it
            // when all clear (rotator.Start respects settings.IsPaused).
            var pauseMonitor = Services.GetRequiredService<PauseConditionMonitor>();
            pauseMonitor.PauseReasonsChanged += (_, reasons) =>
            {
                Dispatcher.BeginInvoke(() =>
                {
                    if (reasons != PauseReason.None) rotator.Stop();
                    else rotator.Start();
                });
            };
            pauseMonitor.Start();

            // Start the auto-update background loop unless disabled in settings.
            Services.GetRequiredService<UpdaterService>().StartIfEnabled();

            // Start the rotator immediately if the cache is already populated from a
            // previous run, and apply a fresh wallpaper right away — otherwise the
            // user would wait a full RotationInterval before seeing anything change.
            if (settings.CachedImages.Count > 0 && !settings.IsPaused)
            {
                rotator.Start();
                _ = rotator.NextWallpaperAsync();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Startup failed:\n\n{ex}",
                "WallP — startup error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(2));
            _host.Dispose();
        }
        base.OnExit(e);
    }

    private static void LogCrash(Exception ex, string source)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CrashLogPath)!);
            var line = $"\n=== {DateTime.Now:yyyy-MM-dd HH:mm:ss} [{source}] ===\n{ex}\n";
            File.AppendAllText(CrashLogPath, line);
        }
        catch { /* swallow — best-effort logging */ }
    }

    private static void ShowCrashDialog(Exception ex)
    {
        try
        {
            MessageBox.Show(
                $"WallP encountered an error and may not work correctly:\n\n{ex.Message}\n\nFull stack trace logged to:\n{CrashLogPath}",
                "WallP — error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        catch { /* if even MessageBox fails, give up */ }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<AppSettings>(_ => AppSettings.Load());

        services.AddSingleton<WallhavenApiService>();
        services.AddSingleton<ImageCache>();
        services.AddSingleton<ImageOptimizer>();
        services.AddSingleton<DesktopWallpaperService>();
        services.AddSingleton<WallpaperRotator>();
        services.AddSingleton<SyncScheduler>();
        services.AddSingleton<SystemStateMonitor>();
        services.AddSingleton<PauseConditionMonitor>();
        services.AddSingleton<UpdaterService>();
        services.AddSingleton<StartupRegistrationService>();
        services.AddSingleton<NotificationService>();

        services.AddSingleton<TrayIconHost>();
        services.AddSingleton<INavigationViewPageProvider, NavigationViewPageProvider>();
        services.AddTransient<SettingsWindow>();
        services.AddTransient<TrayPopover>();
        services.AddTransient<GeneralPage>();
        services.AddTransient<CollectionsPage>();
        services.AddTransient<TimingPage>();
        services.AddTransient<CachePage>();
        services.AddTransient<AboutPage>();
    }
}
