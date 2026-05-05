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

        // Land on General the first time the window opens.
        Nav.Navigate(typeof(GeneralPage));
    }
}
