using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Wpf.Ui.Controls;
using WallP.Models;
using WallP.Services;

namespace WallP.Views.Settings;

public partial class GeneralPage : UserControl
{
    private readonly AppSettings _settings;
    private readonly WallhavenApiService _api;
    private readonly StartupRegistrationService _startup;
    private bool _initializing = true;

    public GeneralPage(AppSettings settings, WallhavenApiService api, StartupRegistrationService startup)
    {
        _settings = settings;
        _api = api;
        _startup = startup;

        InitializeComponent();
        DataContext = settings;

        // PasswordBox can't be data-bound (security), so initialize it manually.
        ApiKeyBox.Password = settings.ApiKey;
        LaunchAtLoginToggle.IsChecked = startup.IsEnabled;

        _initializing = false;
    }

    private void ApiKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        _settings.ApiKey = ApiKeyBox.Password;
        ResetValidationStatus();
    }

    private async void ValidateButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            SetValidationStatus(false, "Enter an API key first.");
            return;
        }

        ValidateButton.IsEnabled = false;
        ValidationStatusPanel.Visibility = Visibility.Visible;
        ValidationLabel.Text = "Checking…";
        ValidationIcon.Symbol = SymbolRegular.ArrowSync24;
        ValidationIcon.Foreground = (Brush)FindResource("TextFillColorSecondaryBrush");

        try
        {
            var ok = await _api.ValidateApiKeyAsync();
            SetValidationStatus(ok, ok ? "API key is valid." : "API key is invalid.");
        }
        catch (Exception ex)
        {
            SetValidationStatus(false, $"Couldn't reach Wallhaven: {ex.Message}");
        }
        finally
        {
            ValidateButton.IsEnabled = true;
        }
    }

    private void LaunchAtLoginToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        try
        {
            _startup.Apply(LaunchAtLoginToggle.IsChecked == true);
            _settings.LaunchAtLogin = LaunchAtLoginToggle.IsChecked == true;
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Couldn't update launch-at-login:\n\n{ex.Message}",
                "WallP",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            // Revert toggle to actual registry state.
            _initializing = true;
            LaunchAtLoginToggle.IsChecked = _startup.IsEnabled;
            _initializing = false;
        }
    }

    private void SetValidationStatus(bool ok, string message)
    {
        ValidationStatusPanel.Visibility = Visibility.Visible;
        ValidationLabel.Text = message;
        ValidationIcon.Symbol = ok ? SymbolRegular.CheckmarkCircle24 : SymbolRegular.ErrorCircle24;
        ValidationIcon.Foreground = ok
            ? new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50))
            : new SolidColorBrush(Color.FromRgb(0xE8, 0x1C, 0x3A));
    }

    private void ResetValidationStatus()
    {
        ValidationStatusPanel.Visibility = Visibility.Collapsed;
    }
}
