using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using WallP.Helpers;
using WallP.Models;
using WallP.Services;

namespace WallP.Views.Settings;

public partial class AboutPage : Page
{
    private const string GitHubUrl = "https://github.com/yogiee/WallP";

    private readonly AppSettings _settings;
    private readonly UpdaterService _updater;
    private bool _initializing = true;

    public AboutPage(AppSettings settings, UpdaterService updater)
    {
        _settings = settings;
        _updater = updater;

        InitializeComponent();

        VersionLabel.Text = $"Version {GetAppVersion()}";

        UpdateModePicker.ItemsSource = EnumPickerHelper.ItemsFor<UpdateMode>();
        UpdateModePicker.SelectedValue = _settings.UpdateMode;

        _initializing = false;
    }

    private static string GetAppVersion()
    {
        // Prefer InformationalVersion (matches <Version> in csproj). Falls back to assembly version.
        var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrEmpty(info))
        {
            // Strip git-hash suffix that .NET appends to InformationalVersion (e.g. "0.1.0+abc1234").
            var plus = info.IndexOf('+');
            return plus > 0 ? info[..plus] : info;
        }
        return asm.GetName().Version?.ToString(3) ?? "0.0.0";
    }

    private void OpenGitHub_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(GitHubUrl) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WallP][About] Open GitHub failed: {ex.Message}");
        }
    }

    private void UpdateModePicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing) return;
        if (UpdateModePicker.SelectedValue is UpdateMode value && value != _settings.UpdateMode)
        {
            _settings.UpdateMode = value;
            _updater.OnUpdateModeChanged();
        }
    }

    private async void Check_Click(object sender, RoutedEventArgs e)
    {
        CheckButton.IsEnabled = false;
        CheckSpinner.Visibility = Visibility.Visible;
        try
        {
            await _updater.CheckForUpdatesAtUserRequestAsync();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Couldn't check for updates:\n\n{ex.Message}",
                "WallP",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }
        finally
        {
            CheckButton.IsEnabled = true;
            CheckSpinner.Visibility = Visibility.Collapsed;
        }
    }
}
