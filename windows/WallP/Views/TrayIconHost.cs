using System.Windows;
using System.Windows.Controls;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using Microsoft.Extensions.DependencyInjection;
using WallP.Models;
using WallP.Services;

namespace WallP.Views;

public sealed class TrayIconHost : IDisposable
{
    private readonly AppSettings _settings;
    private readonly WallpaperRotator _rotator;
    private readonly SyncScheduler _sync;
    private TaskbarIcon? _icon;

    public TrayIconHost(AppSettings settings, WallpaperRotator rotator, SyncScheduler sync)
    {
        _settings = settings;
        _rotator = rotator;
        _sync = sync;
    }

    public void Show()
    {
        _icon = new TaskbarIcon
        {
            ToolTipText = "WallP",
            Visibility = Visibility.Visible,
            ContextMenu = BuildMenu(),
        };

        _icon.LeftClickCommand = new RelayCommand(_ => OpenSettings());
    }

    public void Dispose()
    {
        _icon?.Dispose();
        _icon = null;
    }

    private ContextMenu BuildMenu()
    {
        var menu = new ContextMenu();

        menu.Items.Add(MenuItem("Pause", _ => { /* TODO: rotator.Stop() / .Start() */ }));
        menu.Items.Add(MenuItem("Shuffle", async _ => await _rotator.ShuffleAsync()));
        menu.Items.Add(MenuItem("Sync now", async _ => await _sync.SyncNowAsync()));
        menu.Items.Add(new Separator());
        menu.Items.Add(MenuItem("Settings...", _ => OpenSettings()));
        menu.Items.Add(new Separator());
        menu.Items.Add(MenuItem("Quit WallP", _ => Application.Current.Shutdown()));

        return menu;
    }

    private static MenuItem MenuItem(string header, Action<object?> action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => action(null);
        return item;
    }

    private static void OpenSettings()
    {
        var existing = Application.Current.Windows.OfType<SettingsWindow>().FirstOrDefault();
        if (existing is not null)
        {
            existing.Activate();
            return;
        }

        var window = App.Services.GetRequiredService<SettingsWindow>();
        window.Show();
        window.Activate();
    }
}

internal sealed class RelayCommand : System.Windows.Input.ICommand
{
    private readonly Action<object?> _execute;
    public RelayCommand(Action<object?> execute) => _execute = execute;
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _execute(parameter);
    public event EventHandler? CanExecuteChanged;
}
