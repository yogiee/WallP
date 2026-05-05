using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

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

            // When sync finishes, kick the rotator if it's idle and we have new images.
            // Marshals to the UI thread because rotator's INotifyPropertyChanged is bound
            // to UI in the Settings window.
            sync.SyncCompleted += (_, args) =>
            {
                if (args.NewImageCount <= 0) return;
                Dispatcher.BeginInvoke(() =>
                {
                    if (!rotator.IsRunning && !settings.IsPaused)
                    {
                        rotator.Start();
                        _ = rotator.NextWallpaperAsync();
                    }
                });
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

        services.AddSingleton<TrayIconHost>();
        services.AddTransient<SettingsWindow>();
        services.AddTransient<GeneralPage>();
        services.AddTransient<CollectionsPage>();
        services.AddTransient<TimingPage>();
        services.AddTransient<CachePage>();
        services.AddTransient<AboutPage>();
    }
}
