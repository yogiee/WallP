using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using WallP.Models;
using WallP.Services;

namespace WallP.Views.Settings;

public partial class CollectionsPage : Page
{
    private readonly AppSettings _settings;
    private readonly WallhavenApiService _api;
    private readonly SyncScheduler _sync;
    private readonly WallpaperRotator _rotator;

    private readonly ObservableCollection<CollectionRow> _rows = [];
    private readonly ObservableCollection<AvailableEntry> _available = [];

    public CollectionsPage(
        AppSettings settings,
        WallhavenApiService api,
        SyncScheduler sync,
        WallpaperRotator rotator)
    {
        _settings = settings;
        _api = api;
        _sync = sync;
        _rotator = rotator;

        InitializeComponent();
        CollectionsList.ItemsSource = _rows;
        AvailablePicker.ItemsSource = _available;

        RefreshRows();
        RefreshCredentialsHint();
    }

    private void RefreshRows()
    {
        _rows.Clear();
        foreach (var c in _settings.Collections)
        {
            _rows.Add(new CollectionRow(c, _settings));
        }
        EmptyHint.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RefreshCredentialsHint()
    {
        var missing = string.IsNullOrWhiteSpace(_settings.ApiKey)
                      || string.IsNullOrWhiteSpace(_settings.WallhavenUsername);
        MissingCredentialsPanel.Visibility = missing ? Visibility.Visible : Visibility.Collapsed;
        FetchButton.IsEnabled = !missing;
    }

    private void SetDefault_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is Guid id)
        {
            _rotator.SwitchToCollection(id);
            RefreshRows();
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.Tag is not Guid id) return;

        var collection = _settings.Collections.FirstOrDefault(c => c.Id == id);
        if (collection is null) return;

        var confirm = System.Windows.MessageBox.Show(
            $"Remove \"{collection.Name}\"?\n\nThis will delete all cached images for this collection.",
            "WallP",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);
        if (confirm != System.Windows.MessageBoxResult.Yes) return;

        _settings.CachedImages.RemoveAll(i => i.CollectionId == id);
        _settings.Collections.Remove(collection);

        if (_settings.DefaultCollectionId == id)
        {
            _settings.DefaultCollectionId = _settings.Collections.FirstOrDefault()?.Id;
        }
        _settings.Save();

        // Best-effort cache cleanup; we don't await this so the UI stays responsive.
        _ = App.Services.GetService(typeof(ImageCache)) is ImageCache cache
            ? cache.ClearCollectionAsync(id)
            : Task.CompletedTask;

        RefreshRows();
        _rotator.RefreshImageList();
    }

    private async void Fetch_Click(object sender, RoutedEventArgs e)
    {
        ErrorLabel.Visibility = Visibility.Collapsed;
        FetchButton.IsEnabled = false;
        FetchSpinner.Visibility = Visibility.Visible;
        FetchStatus.Text = "Fetching…";
        _available.Clear();
        PickerRow.Visibility = Visibility.Collapsed;
        AddButton.IsEnabled = false;

        try
        {
            var collections = await _api.FetchCollectionsAsync(_settings.WallhavenUsername);
            FetchStatus.Text = $"{collections.Count} collection(s) found.";

            var existingIds = _settings.Collections.Select(c => c.WallhavenCollectionId).ToHashSet();
            foreach (var c in collections)
            {
                var alreadyAdded = existingIds.Contains(c.Id);
                _available.Add(new AvailableEntry(c, alreadyAdded));
            }

            PickerRow.Visibility = _available.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            if (_available.Count == 0)
            {
                FetchStatus.Text = "Your account has no collections.";
            }
        }
        catch (Exception ex)
        {
            FetchStatus.Text = "";
            ShowError($"Couldn't fetch collections: {ex.Message}");
        }
        finally
        {
            FetchSpinner.Visibility = Visibility.Collapsed;
            FetchButton.IsEnabled = true;
        }
    }

    private void AvailablePicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AvailablePicker.SelectedItem is AvailableEntry entry)
        {
            AddButton.IsEnabled = !entry.AlreadyAdded;
        }
        else
        {
            AddButton.IsEnabled = false;
        }
    }

    private async void Add_Click(object sender, RoutedEventArgs e)
    {
        if (AvailablePicker.SelectedItem is not AvailableEntry entry || entry.AlreadyAdded) return;

        var collection = new WallPCollection
        {
            Name = entry.Source.Label,
            WallhavenCollectionId = entry.Source.Id,
            WallhavenUsername = _settings.WallhavenUsername,
        };
        _settings.Collections.Add(collection);
        _settings.DefaultCollectionId ??= collection.Id;
        _settings.Save();

        RefreshRows();
        // Mark as added so the user can't double-add.
        entry.AlreadyAdded = true;
        AddButton.IsEnabled = false;
        AvailablePicker.Items.Refresh();

        // Kick off a sync of the new collection. Settings tab stays usable while it runs.
        try { await _sync.SyncCollectionAsync(collection.Id); }
        catch (Exception ex) { ShowError($"Sync failed: {ex.Message}"); }
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.Visibility = Visibility.Visible;
    }

    private sealed class CollectionRow
    {
        public Guid Id { get; }
        public string Name { get; }
        public string MetaLine { get; }
        public Visibility DefaultBadgeVisibility { get; }
        public Visibility SetDefaultVisibility { get; }

        public CollectionRow(WallPCollection c, AppSettings settings)
        {
            Id = c.Id;
            Name = c.Name;

            var imageCount = settings.ImagesForCollection(c.Id).Count();
            var lastSynced = c.LastSynced is { } ts ? FormatRelative(ts) : "never";
            MetaLine = $"Wallhaven #{c.WallhavenCollectionId}  ·  {imageCount} cached  ·  Synced {lastSynced}";

            var isDefault = c.Id == settings.DefaultCollectionId;
            DefaultBadgeVisibility = isDefault ? Visibility.Visible : Visibility.Collapsed;
            SetDefaultVisibility = isDefault ? Visibility.Collapsed : Visibility.Visible;
        }

        private static string FormatRelative(DateTime ts)
        {
            var delta = DateTime.UtcNow - ts.ToUniversalTime();
            if (delta.TotalSeconds < 60) return "just now";
            if (delta.TotalMinutes < 60) return $"{(int)delta.TotalMinutes}m ago";
            if (delta.TotalHours < 24) return $"{(int)delta.TotalHours}h ago";
            if (delta.TotalDays < 7) return $"{(int)delta.TotalDays}d ago";
            return ts.ToLocalTime().ToString("yyyy-MM-dd");
        }
    }

    private sealed class AvailableEntry
    {
        public WallhavenCollection Source { get; }
        public bool AlreadyAdded { get; set; }
        public string Display => AlreadyAdded
            ? $"{Source.Label} ({Source.Count} wallpapers) ✓ already added"
            : $"{Source.Label} ({Source.Count} wallpapers)";

        public AvailableEntry(WallhavenCollection source, bool alreadyAdded)
        {
            Source = source;
            AlreadyAdded = alreadyAdded;
        }
    }
}
