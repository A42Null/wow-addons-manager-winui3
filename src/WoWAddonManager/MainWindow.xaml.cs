using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WoWAddonManager.Pages;

namespace WoWAddonManager;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Title = "魔兽世界插件管理器";
        SystemBackdrop = new MicaBackdrop { Kind = MicaKind.BaseAlt };

        ContentFrame.Navigate(typeof(InstalledPage));
        NavView.SelectedItem = NavView.MenuItems[0];
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item) return;
        var page = item.Tag?.ToString() switch
        {
            "browse" => typeof(BrowsePage),
            "settings" => typeof(SettingsPage),
            _ => typeof(InstalledPage)
        };
        if (ContentFrame.CurrentSourcePageType != page)
            ContentFrame.Navigate(page);
    }
}
