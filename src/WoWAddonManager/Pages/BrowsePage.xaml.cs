using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using WoWAddonManager.Services;
using WoWAddonManager.ViewModels;

namespace WoWAddonManager.Pages;

public sealed partial class BrowsePage : Page
{
    private const int PageSize = 40;

    private readonly List<CategoryOption> _categories = [];
    private int _page;
    private int _totalPages = 1;

    public BrowsePage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        _ = LoadCategoriesAndSearchAsync();
    }

    private string AddonsDir =>
        WowInstallService.GetAddOnsDir(ConfigService.Current.WowRoot, ConfigService.Current.Flavor);

    private bool ValidateWowPath()
    {
        if (Directory.Exists(AddonsDir)) return true;
        ShowInfo("尚未配置有效的魔兽世界目录，请先到「设置」页完成配置。", InfoBarSeverity.Warning);
        return false;
    }

    private async Task LoadCategoriesAndSearchAsync()
    {
        try
        {
            var cats = await CurseForgeService.Instance.GetAddonCategoriesAsync();
            _categories.Clear();
            _categories.Add(new CategoryOption { Id = 0, Name = "全部分类" });
            _categories.AddRange(cats.OrderBy(c => c.Name)
                .Select(c => new CategoryOption { Id = c.Id, Name = c.Name }));
            CategoryCombo.ItemsSource = _categories;
            CategoryCombo.SelectedIndex = 0;
            await DoSearchAsync(0);
        }
        catch (Exception ex)
        {
            ShowInfo($"加载分类失败：{ex.Message}。可尝试在「设置」中填写 CurseForge API Key。", InfoBarSeverity.Error);
        }
    }

    private async void OnSearchClick(object sender, RoutedEventArgs e) => await DoSearchAsync(0);

    private async void OnSearchBoxKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
            await DoSearchAsync(0);
    }

    private async void OnCategoryChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CategoryCombo.ItemsSource != null && ListResults != null)
            await DoSearchAsync(0);
    }

    private async void OnSortChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SortCombo.IsLoaded && ListResults != null && CategoryCombo.ItemsSource != null)
            await DoSearchAsync(0);
    }

    private async void OnPrevPageClick(object sender, RoutedEventArgs e)
    {
        if (_page > 0) await DoSearchAsync(_page - 1);
    }

    private async void OnNextPageClick(object sender, RoutedEventArgs e)
    {
        if (_page + 1 < _totalPages) await DoSearchAsync(_page + 1);
    }

    private async Task DoSearchAsync(int page)
    {
        try
        {
            ShowBusy("正在搜索…");
            var sort = 6;
            if (SortCombo.SelectedItem is ComboBoxItem si && int.TryParse(si.Tag?.ToString(), out var s)) sort = s;

            var catId = (CategoryCombo.SelectedItem as CategoryOption)?.Id;
            var mods = await CurseForgeService.Instance.SearchAsync(
                SearchBox.Text, catId, sort, page, PageSize);

            _page = page;
            // 匿名端点可能没有 pagination，用有无返回判断
            _totalPages = mods.Count == PageSize ? page + 2 : page + 1;

            ListResults.ItemsSource = mods.Select(m => new BrowseRow { Mod = m }).ToList();
            PageText.Text = $"第 {page + 1} 页";
            PrevPageButton.IsEnabled = page > 0;
            NextPageButton.IsEnabled = page + 1 < _totalPages;

            if (mods.Count == 0)
                ShowInfo("没有找到匹配的插件。", InfoBarSeverity.Informational);
        }
        catch (Exception ex)
        {
            ShowInfo($"搜索失败：{ex.Message}。匿名接口可能被限流，建议在「设置」中申请并填写免费的 CurseForge API Key。",
                InfoBarSeverity.Error);
        }
        finally
        {
            HideBusy();
        }
    }

    private async void OnInstallClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not BrowseRow row) return;
        if (!ValidateWowPath()) return;

        var originalContent = btn.Content;
        try
        {
            btn.IsEnabled = false;
            btn.Content = "安装中…";
            ShowBusy($"正在安装 {row.Mod.Name}…");

            var cf = CurseForgeService.Instance;
            var flavor = ConfigService.Current.Flavor;
            var files = await cf.GetModFilesAsync(row.Mod.Id);
            var file = CurseForgeService.PickBestFile(files, flavor)
                       ?? throw new InvalidOperationException($"没有适配 {flavor} 的可用文件");

            var url = await cf.ResolveDownloadUrlAsync(row.Mod.Id, file);
            var folders = await WowInstallService.DownloadAndInstallAsync(url, AddonsDir);

            btn.Content = "已安装";
            var name = string.IsNullOrWhiteSpace(file.DisplayName) ? file.FileName : file.DisplayName;
            ShowInfo($"已安装 {row.Mod.Name}（{name}）到 {AddonsDir}。" +
                     (folders.Length > 0 ? $" 包含插件: {string.Join(", ", folders)}" : ""),
                InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            btn.IsEnabled = true;
            btn.Content = originalContent;
            ShowInfo($"安装 {row.Mod.Name} 失败：{ex.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            HideBusy();
        }
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
}
