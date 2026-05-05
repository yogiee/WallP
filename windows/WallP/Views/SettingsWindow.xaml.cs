using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Controls;
using WallP.Views.Settings;

namespace WallP.Views;

public partial class SettingsWindow : FluentWindow
{
    public SettingsWindow(INavigationViewPageProvider pageProvider)
    {
        InitializeComponent();

        // Hand the NavigationView our DI-backed page provider so it can resolve pages
        // with constructor-injected services. With this in place, NavigationView's
        // built-in selection (TargetPageType-driven) handles both content and the
        // side-pane highlight automatically — no more Click handlers needed.
        Nav.SetPageProviderService(pageProvider);

        // NavigationView's content host swallows PreviewMouseWheel before it reaches
        // the page's ScrollViewer. Catch it at the window level and route manually.
        PreviewMouseWheel += OnPreviewMouseWheel;

        // Initial navigation must wait until the NavigationView has been loaded —
        // calling Navigate from the ctor throws NullReferenceException inside
        // NavigationView.UpdateContent because the content host isn't wired yet.
        Loaded += (_, _) => Nav.Navigate(typeof(GeneralPage));
    }

    private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled) return;
        if (e.OriginalSource is not DependencyObject d) return;

        // Walk up from the hit element looking for a ScrollViewer that has somewhere
        // to scroll. First match wins so we drive the inner page-content scroller
        // rather than any outer container.
        while (d is not null)
        {
            if (d is ScrollViewer sv && sv.ScrollableHeight > 0)
            {
                sv.ScrollToVerticalOffset(sv.VerticalOffset - e.Delta);
                e.Handled = true;
                return;
            }
            d = VisualTreeHelper.GetParent(d) ?? LogicalTreeHelper.GetParent(d);
        }
    }
}
