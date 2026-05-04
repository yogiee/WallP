using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using WallP.Helpers;
using WallP.Models;
using WallP.Services;

namespace WallP.Views.Settings;

public partial class TimingPage : UserControl
{
    private readonly AppSettings _settings;
    private readonly SyncScheduler _sync;
    private readonly WallpaperRotator _rotator;
    private bool _initializing = true;

    public TimingPage(AppSettings settings, SyncScheduler sync, WallpaperRotator rotator)
    {
        _settings = settings;
        _sync = sync;
        _rotator = rotator;

        InitializeComponent();

        RotationIntervalPicker.ItemsSource = EnumPickerHelper.ItemsFor<RotationInterval>();
        DisplayOrderPicker.ItemsSource = EnumPickerHelper.ItemsFor<DisplayOrder>();
        SyncIntervalPicker.ItemsSource = EnumPickerHelper.ItemsFor<SyncInterval>();

        RotationIntervalPicker.SelectedValue = _settings.RotationInterval;
        DisplayOrderPicker.SelectedValue = _settings.DisplayOrder;
        SyncIntervalPicker.SelectedValue = _settings.SyncInterval;

        DefaultCollectionPicker.ItemsSource = BuildCollectionItems();
        DefaultCollectionPicker.SelectedValue = _settings.DefaultCollectionId;

        _sync.PropertyChanged += Sync_PropertyChanged;
        Unloaded += (_, _) => _sync.PropertyChanged -= Sync_PropertyChanged;
        UpdateSyncUi();

        _initializing = false;
    }

    private IList<CollectionPickerItem> BuildCollectionItems()
    {
        var list = new List<CollectionPickerItem> { new(null, "None") };
        list.AddRange(_settings.Collections.Select(c => new CollectionPickerItem(c.Id, c.Name)));
        return list;
    }

    private void RotationIntervalPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing) return;
        if (RotationIntervalPicker.SelectedValue is RotationInterval value && value != _settings.RotationInterval)
        {
            _settings.RotationInterval = value;
            // Restart so the new interval takes effect immediately rather than waiting
            // out the previous timer.
            _rotator.Restart();
        }
    }

    private void DisplayOrderPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing) return;
        if (DisplayOrderPicker.SelectedValue is DisplayOrder value && value != _settings.DisplayOrder)
        {
            _settings.DisplayOrder = value;
            _rotator.RefreshImageList();
        }
    }

    private void SyncIntervalPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing) return;
        if (SyncIntervalPicker.SelectedValue is SyncInterval value && value != _settings.SyncInterval)
        {
            _settings.SyncInterval = value;
            _sync.Restart();
        }
    }

    private void DefaultCollectionPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing) return;
        if (DefaultCollectionPicker.SelectedValue is Guid id)
        {
            _rotator.SwitchToCollection(id);
        }
        else
        {
            _settings.DefaultCollectionId = null;
        }
    }

    private async void SyncNow_Click(object sender, RoutedEventArgs e)
    {
        try { await _sync.SyncNowAsync(); }
        catch (Exception ex)
        {
            SyncStatusLabel.Text = ex.Message;
        }
    }

    private void Sync_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Sync property changes can fire from a thread-pool thread (timer callback).
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(UpdateSyncUi);
            return;
        }
        UpdateSyncUi();
    }

    private void UpdateSyncUi()
    {
        SyncNowButton.IsEnabled = !_sync.IsSyncing;
        SyncSpinner.Visibility = _sync.IsSyncing ? Visibility.Visible : Visibility.Collapsed;

        if (!string.IsNullOrEmpty(_sync.LastSyncError))
            SyncStatusLabel.Text = _sync.LastSyncError;
        else
            SyncStatusLabel.Text = _sync.SyncProgress;
    }

    private sealed record CollectionPickerItem(Guid? Value, string Display);
}
