using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WallP.Models;
using WallP.Services;
using WallP.Views;

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

        services.AddSingleton<TrayIconHost>();
        services.AddTransient<SettingsWindow>();
    }
}
