using Wpf.Ui.Abstractions;

namespace WallP.Helpers;

/// <summary>
/// Bridges WPF-UI's NavigationView page resolution to our DI container so pages
/// with constructor-injected services can be navigated to via TargetPageType.
/// </summary>
public sealed class NavigationViewPageProvider(IServiceProvider serviceProvider) : INavigationViewPageProvider
{
    public object? GetPage(Type pageType) => serviceProvider.GetService(pageType);
}
