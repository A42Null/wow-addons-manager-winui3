using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using WoWAddonManager.Services;
using WoWAddonManager.ViewModels;

namespace WoWAddonManager.Pages;

public sealed partial class InstalledPage : Page
{
    private readonly List<AddonRow> _rows = [];
    private string _addonsDir = "";

    public InstalledPage()
    {
        InitializeComponent();
        ConfigService.ConfigChanged += () => { ReloadSafe(); };
    }

    protected override void OnNavigatedTo(NavigationEventArgs e) => ReloadSafe();

    private void ReloadSafe()
    {
        if (DispatcherQueue.HasThreadAccess)
            _ = ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        var cfg = ConfigService.Current;
        _addonsDir = WowInstallService.GetAddOnsDir(cfg.WowRoot, cfg.Flavor);

        ScopeText.Text = Directory.Exists(_addonsDir)
            ? $"{cfg.Flavor}  ·  {_addonsDir}"
            : "未找到插件目录，请先在「设置」中配置魔兽世界目录";

        ListAddons.ItemsSource = null;
        _rows.Clear();

        if (Directory.Exists(_addonsDir))
        {
            foreach (var addon in WowInstallService.ListInstalled(_addonsDir))
                _rows.Add(new AddonRow { Addon = addon });
            ListAddons.ItemsSource = _rows;
        }
        await Task.CompletedTask;
    }

    private void ShowBusy(string text)
    {
        BusyText.Text = text;
        BusyOverlay.Visibility = Visibility.Visible;
    }

    private void HideBusy() => BusyOverlay.Visibility = Visibility.Collapsed;

    private void ShowInfo(string message, InfoBarSeverity severity = InfoBarSeverity.Informational, string title = "")
    {
        InfoState.Message = message;
        InfoState.Severity = severity;
        InfoState.Title = title;
        InfoState.IsOpen = true;
    }

    private async void OnRefreshClick(object sender, RoutedEventArgs e) => await ReloadAsync();

    private async void OnOpenFolderClick(object sender, RoutedEventArgs e)
    {
        if (!Directory.Exists(_addonsDir))
        {
            ShowInfo("插件目录不存在，请先在「设置」中配置。", InfoBarSeverity.Warning);
            return;
        }
        try
        {
            System.Diagnostics.Process.Start("explorer.exe", $"\"{_addonsDir}\"");
        }
        catch (Exception ex)
        {
            ShowInfo($"打开目录失败: {ex.Message}", InfoBarSeverity.Error);
        }
        await Task.CompletedTask;
    }

    private async void OnCheckUpdatesClick(object sender, RoutedEventArgs e)
    {
        if (_rows.Count == 0) { ShowInfo("没有已安装的插件。", InfoBarSeverity.Informational); return; }

        try
        {
            ShowBusy("正在检查更新…");
            var cf = CurseForgeService.Instance;
            var flavor = ConfigService.Current.Flavor;
            int checkedCount = 0, updateCount = 0;

            foreach (var row in _rows)
            {
                row.StatusText = "";
                if (row.Addon.CurseProjectId <= 0)
                {
                    row.RemoteVersion = "";
                    row.HasUpdate = false;
                    continue;
                }
                try
                {
                    var mod = await cf.GetModAsync(row.Addon.CurseProjectId);
                    var files = mod.LatestFiles.Count > 0
                        ? mod.LatestFiles
                        : await cf.GetModFilesAsync(mod.Id);
                    var file = CurseForgeService.PickBestFile(files, flavor);
                    checkedCount++;
                    if (file == null) continue;

                    var remote = string.IsNullOrWhiteSpace(file.DisplayName) ? file.FileName : file.DisplayName;
                    row.RemoteVersion = $"可更新 → {remote}";
                    row.HasUpdate = !string.Equals(remote, row.Version, StringComparison.OrdinalIgnoreCase);
                    if (row.HasUpdate) updateCount++;
                }
                catch (Exception ex)
                {
                    row.RemoteVersion = "";
                    row.HasUpdate = false;
                    row.StatusText = $"检查失败: {ex.Message}";
                }
            }

            ShowInfo(updateCount > 0
                ? $"检查完成：{checkedCount} 个插件已关联，其中 {updateCount} 个可更新。"
                : "检查完成：所有插件均为最新。", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowInfo($"检查更新失败：{ex.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            HideBusy();
        }
    }

    private async void OnUpdateAllClick(object sender, RoutedEventArgs e)
    {
        var targets = _rows.Where(r => r.HasUpdate).ToList();
        if (targets.Count == 0) { ShowInfo("没有可更新的插件，请先「检查更新」。", InfoBarSeverity.Informational); return; }

        try
        {
            ShowBusy("正在更新全部…");
            var cf = CurseForgeService.Instance;
            var flavor = ConfigService.Current.Flavor;
            var ok = 0; var fail = 0;

            foreach (var row in targets)
            {
                try
                {
                    await InstallLatestForAsync(cf, row.Addon.CurseProjectId, flavor);
                    row.StatusText = "已更新";
                    ok++;
                }
                catch (Exception ex)
                {
                    row.StatusText = $"更新失败: {ex.Message}";
                    fail++;
                }
            }

            ShowInfo($"批量更新完成：成功 {ok}，失败 {fail}。",
                fail > 0 ? InfoBarSeverity.Warning : InfoBarSeverity.Success);
            await ReloadAsync();
        }
        finally
        {
            HideBusy();
        }
    }

    private async void OnUpdateClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not AddonRow row) return;

        try
        {
            ShowBusy($"正在更新 {row.Title}…");
            var file = await InstallLatestForAsync(CurseForgeService.Instance, row.Addon.CurseProjectId, ConfigService.Current.Flavor);
            var remote = string.IsNullOrWhiteSpace(file.DisplayName) ? file.FileName : file.DisplayName;
            row.RemoteVersion = "";
            row.HasUpdate = false;
            row.StatusText = $"已更新 → {remote}";
            ShowInfo($"{row.Title} 已更新到 {remote}。", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            row.StatusText = $"更新失败: {ex.Message}";
            ShowInfo($"更新 {row.Title} 失败：{ex.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            HideBusy();
        }
    }

    private async Task<CfFile> InstallLatestForAsync(CurseForgeService cf, int projectId, string flavor)
    {
        if (projectId <= 0) throw new InvalidOperationException("该插件未关联 CurseForge 项目");
        var files = await cf.GetModFilesAsync(projectId);
        var file = CurseForgeService.PickBestFile(files, flavor)
                   ?? throw new InvalidOperationException("没有可用文件");
        var url = await cf.ResolveDownloadUrlAsync(projectId, file);
        await WowInstallService.DownloadAndInstallAsync(url, _addonsDir);
        return file;
    }

    private async void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not AddonRow row) return;

        var dialog = new ContentDialog
        {
            Title = "删除插件",
            Content = $"确定删除「{row.Title}」（{row.FolderName}）吗？\n此操作会直接删除插件文件夹，不可恢复。",
            PrimaryButtonText = "删除",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };
        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        try
        {
            ShowBusy($"正在删除 {row.Title}…");
            WowInstallService.DeleteAddon(_addonsDir, row.FolderName);
            ShowInfo($"已删除 {row.Title}。", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowInfo($"删除失败：{ex.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            HideBusy();
            await ReloadAsync();
        }
    }
}
