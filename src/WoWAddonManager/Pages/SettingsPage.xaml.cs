using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Windows.Storage.Pickers;
using WinRT.Interop;
using WoWAddonManager.Services;

namespace WoWAddonManager.Pages;

public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        var cfg = ConfigService.Current;
        WowRootBox.Text = cfg.WowRoot;
        ApiKeyBox.Password = cfg.CfApiKey;
        RefreshFlavors(cfg.Flavor);

        // 外观
        SelectByTag(ThemeButtons, cfg.Theme);
        SelectByTag(BackdropButtons, cfg.Backdrop);
        SelectByTag(NavPosCombo, cfg.NavPosition);
    }

    private static void SelectByTag(Selector selector, string? tag)
    {
        foreach (var item in selector.Items)
        {
            if ((item as FrameworkElement)?.Tag?.ToString() == tag)
            {
                selector.SelectedItem = item;
                return;
            }
        }
    }

    private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThemeButtons.SelectedItem is not RadioButton rb) return;
        SaveAppearance("Theme", rb.Tag?.ToString() ?? "system");
    }

    private void OnBackdropChanged(object sender, SelectionChangedEventArgs e)
    {
        if (BackdropButtons.SelectedItem is not RadioButton rb) return;
        SaveAppearance("Backdrop", rb.Tag?.ToString() ?? "micaalt");
    }

    private void OnNavPosChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NavPosCombo.SelectedItem is not ComboBoxItem cb) return;
        SaveAppearance("NavPosition", cb.Tag?.ToString() ?? "left");
    }

    private void SaveAppearance(string key, string value)
    {
        var cfg = ConfigService.Current;
        var current = key switch
        {
            "Theme" => cfg.Theme,
            "Backdrop" => cfg.Backdrop,
            "NavPosition" => cfg.NavPosition,
            _ => "",
        };
        if (current == value) return; // 初始化期间避免回写

        var prop = typeof(AppConfig).GetProperty(key)!;
        prop.SetValue(cfg, value);
        ConfigService.Save(cfg);
    }

    private void RefreshFlavors(string preferredFlavor)
    {
        var root = WowRootBox.Text.Trim();
        var flavors = Directory.Exists(root)
            ? WowInstallService.GetFlavors(root)
            : [.. WowInstallService.KnownFlavors];

        FlavorCombo.ItemsSource = flavors;
        if (flavors.Any(f => f.Folder == preferredFlavor))
            FlavorCombo.SelectedValue = preferredFlavor;
        else if (flavors.Count > 0)
            FlavorCombo.SelectedIndex = 0;

        UpdateAddOnsPathText();
    }

    private void UpdateAddOnsPathText()
    {
        var root = WowRootBox.Text.Trim();
        var flavor = FlavorCombo.SelectedValue?.ToString() ?? "_retail_";
        AddOnsPathText.Text = string.IsNullOrWhiteSpace(root)
            ? "插件将安装到：—"
            : "插件将安装到：" + WowInstallService.GetAddOnsDir(root, flavor);
    }

    private void OnFlavorChanged(object sender, SelectionChangedEventArgs e) => UpdateAddOnsPathText();

    private void OnDetectClick(object sender, RoutedEventArgs e)
    {
        var detected = WowInstallService.DetectWowRoot();
        if (detected == null)
        {
            ShowInfo("未自动检测到魔兽世界目录，请手动选择游戏安装根目录（含 _retail_ / _classic_ 等子目录）。", InfoBarSeverity.Warning);
            return;
        }
        WowRootBox.Text = detected;
        RefreshFlavors("_retail_");
        ShowInfo($"已检测到：{detected}", InfoBarSeverity.Success);
    }

    private async void OnPickFolderClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FolderPicker();
            if (App.MainWindow != null)
                InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));
            picker.FileTypeFilter.Add("*");
            var folder = await picker.PickSingleFolderAsync();
            if (folder == null) return;

            WowRootBox.Text = folder.Path;
            RefreshFlavors("_retail_");
            if (WowInstallService.GetFlavors(folder.Path).Count == 0)
                ShowInfo("所选目录下没有发现 _retail_ / _classic_ 等客户端版本目录，请确认选择的是魔兽世界安装根目录。", InfoBarSeverity.Warning);
        }
        catch (Exception ex)
        {
            ShowInfo($"选择文件夹失败：{ex.Message}", InfoBarSeverity.Error);
        }
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var root = WowRootBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(root))
        {
            ShowInfo("请先填写或检测魔兽世界目录。", InfoBarSeverity.Warning);
            return;
        }
        if (!Directory.Exists(root))
        {
            ShowInfo("目录不存在，请检查路径。", InfoBarSeverity.Error);
            return;
        }

        ConfigService.Save(new AppConfig
        {
            WowRoot = root,
            Flavor = FlavorCombo.SelectedValue?.ToString() ?? "_retail_",
            CfApiKey = ApiKeyBox.Password.Trim(),
        });

        ShowInfo("设置已保存。", InfoBarSeverity.Success);
    }

    private void OnOpenAddOnsClick(object sender, RoutedEventArgs e)
    {
        var root = WowRootBox.Text.Trim();
        var flavor = FlavorCombo.SelectedValue?.ToString() ?? "_retail_";
        if (string.IsNullOrWhiteSpace(root)) return;
        var dir = WowInstallService.GetAddOnsDir(root, flavor);
        System.IO.Directory.CreateDirectory(dir);
        System.Diagnostics.Process.Start("explorer.exe", $"\"{dir}\"");
    }

    private void ShowInfo(string message, InfoBarSeverity severity = InfoBarSeverity.Informational, string title = "")
    {
        SaveInfo.Message = message;
        SaveInfo.Severity = severity;
        SaveInfo.Title = title;
        SaveInfo.IsOpen = true;
    }
}
