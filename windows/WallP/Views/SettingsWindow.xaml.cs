using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui.Controls;
using WallP.Views.Settings;

namespace WallP.Views;

public partial class SettingsWindow : FluentWindow
{
    private readonly IServiceProvider _services;
    private readonly Dictionary<NavigationViewItem, Func<UIElement>> _pageFactories;

    public SettingsWindow(IServiceProvider services)
    {
        _services = services;
        InitializeComponent();

        _pageFactories = new Dictionary<NavigationViewItem, Func<UIElement>>
        {
            [GeneralItem] = () => _services.GetRequiredService<GeneralPage>(),
            [CollectionsItem] = () => _services.GetRequiredService<CollectionsPage>(),
            [TimingItem] = () => _services.GetRequiredService<TimingPage>(),
            [CacheItem] = () => _services.GetRequiredService<CachePage>(),
            [AboutItem] = () => _services.GetRequiredService<AboutPage>(),
        };

        // Default to General on first open. WPF-UI's NavigationView doesn't expose a
        // public programmatic-selection API in 4.3 without the TargetPageType pattern,
        // so we just load the General page directly into the content area; the side
        // pane catches up the first time the user picks anything else.
        ContentArea.Content = _pageFactories[GeneralItem]();
    }

    private void NavItem_Click(object sender, RoutedEventArgs e)
    {
        // NavigationView.SelectionChanged in WPF-UI 4.3 only fires when items have a
        // TargetPageType — without that we have to wire each item's Click event ourselves.
        if (sender is NavigationViewItem item && _pageFactories.TryGetValue(item, out var factory))
        {
            ContentArea.Content = factory();
        }
    }

    private static UIElement PlaceholderPage(string text) =>
        new System.Windows.Controls.TextBlock
        {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 14,
            Opacity = 0.6,
        };
}
