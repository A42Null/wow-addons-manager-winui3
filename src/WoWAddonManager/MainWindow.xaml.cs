using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WoWAddonManager.Pages;
using WoWAddonManager.Services;

namespace WoWAddonManager;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        Title = "魔兽世界插件管理器";

        ApplyConfig();

        ContentFrame.Navigate(typeof(InstalledPage));
        NavView.SelectedItem = NavView.MenuItems[0];

        ConfigService.ConfigChanged += ApplyConfig;
    }

    private void ApplyConfig()
    {
        var cfg = ConfigService.Current;

        // 主题
        RootGrid.RequestedTheme = cfg.Theme switch
        {
            "light" => ElementTheme.Light,
            "dark" => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };

        // 背景材质
        SystemBackdrop = cfg.Backdrop switch
        {
            "mica" => new MicaBackdrop { Kind = MicaKind.Base },
            "acrylic" => new DesktopAcrylicBackdrop(),
            _ => new MicaBackdrop { Kind = MicaKind.BaseAlt },
        };

        // 导航栏位置
        NavView.PaneDisplayMode = cfg.NavPosition == "top"
            ? NavigationViewPaneDisplayMode.Top
            : NavigationViewPaneDisplayMode.Left;
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item) return;
        var page = item.Tag?.ToString() switch
        {
            "browse" => typeof(BrowsePage),
            "settings" => typeof(SettingsPage),
            _ => typeof(InstalledPage),
        };
        if (ContentFrame.CurrentSourcePageType != page)
            ContentFrame.Navigate(page);
    }

    private void NavView_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
    {
        if (ContentFrame.CanGoBack)
            ContentFrame.GoBack();
    }
}
