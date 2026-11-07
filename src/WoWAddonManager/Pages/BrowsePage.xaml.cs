using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using WoWAddonManager.Services;
using WoWAddonManager.Services.Sources;
using WoWAddonManager.ViewModels;

namespace WoWAddonManager.Pages;

public sealed partial class BrowsePage : Page
{
    private static readonly List<IAddonSource> Sources =
    [
        new CurseForgeSource(),
        new WclboxSource(),
        new Wowui178Source(),
        new BigFootSource(),
        new NetEaseDDSource(),
    ];

    private readonly List<CategoryOption> _categories = [];
    private IAddonSource _source = null!;
    private int _page;
    private int _totalPages = 1;

    public BrowsePage()
    {
        InitializeComponent();
        SourceCombo.ItemsSource = Sources.Select(s => s.DisplayName).ToList();
        SourceCombo.SelectedIndex = 0;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        ApplySourceVisibility();
        _ = LoadCategoriesAndSearchAsync();
    }

    private IAddonSource Source => _source ??= Sources[0];

    private void ApplySourceVisibility()
    {
        CategoryCombo.Visibility = Source.SupportsCategoryAndSort ? Visibility.Visible : Visibility.Collapsed;
        SortCombo.Visibility = Source.SupportsCategoryAndSort ? Visibility.Visible : Visibility.Collapsed;

        if (Source.VersionOptions.Count > 0)
        {
            VersionCombo.ItemsSource = Source.VersionOptions.Select(kv => new ComboBoxItem
            {
                Content = kv.Value,
                Tag = kv.Key,
            }).ToList();
            var current = VersionCombo.SelectedItem as ComboBoxItem;
            var keep = current?.Tag;
            VersionCombo.SelectedIndex = 0;
            if (keep != null)
                foreach (ComboBoxItem i in VersionCombo.Items)
                    if (Equals(i.Tag, keep)) VersionCombo.SelectedItem = i;
            VersionCombo.Visibility = Visibility.Visible;
        }
        else
        {
            VersionCombo.Visibility = Visibility.Collapsed;
        }
    }

    private int? SelectedVersionId =>
        VersionCombo.Visibility == Visibility.Visible &&
        VersionCombo.SelectedItem is ComboBoxItem cb &&
        cb.Tag is int v && v > 0 ? v : null;

    private int SelectedSort
    {
        get
        {
            if (SortCombo.SelectedItem is ComboBoxItem si && int.TryParse(si.Tag?.ToString(), out var s)) return s;
            return 6;
        }
    }

    private int? SelectedCategory => (CategoryCombo.SelectedItem as CategoryOption)?.Id is > 0 ? (CategoryCombo.SelectedItem as CategoryOption)!.Id : null;

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
            if (Source.SupportsCategoryAndSort && _categories.Count == 0)
            {
                var cf = (CurseForgeSource)Sources.First(s => s.Id == "curseforge");
                var cats = await CurseForgeService.Instance.GetAddonCategoriesAsync();
                _categories.Add(new CategoryOption { Id = 0, Name = "全部分类" });
                _categories.AddRange(cats.OrderBy(c => c.Name)
                    .Select(c => new CategoryOption { Id = c.Id, Name = c.Name }));
                CategoryCombo.ItemsSource = _categories;
                CategoryCombo.SelectedIndex = 0;
            }
            await DoSearchAsync(0);
        }
        catch (Exception ex)
        {
            ShowInfo($"初始化失败：{ex.Message}", InfoBarSeverity.Error);
        }
    }

    private async void OnSourceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SourceCombo.SelectedIndex < 0 || ListResults == null) return;
        _source = Sources[SourceCombo.SelectedIndex];
        _categories.Clear();
        CategoryCombo.ItemsSource = null;
        ApplySourceVisibility();

        // 新手盒子：按当前客户端版本自动选中筛选
        if (_source is WclboxSource && VersionCombo.Items.Count > 0)
        {
            var flavor = ConfigService.Current.Flavor;
            if (WclboxSource.FlavorToVersion.TryGetValue(flavor, out var vid))
            {
                foreach (ComboBoxItem i in VersionCombo.Items)
                    if (Equals(i.Tag, vid)) { VersionCombo.SelectedItem = i; break; }
            }
        }
        await LoadCategoriesAndSearchAsync();
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

    private async void OnVersionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (VersionCombo.IsLoaded && ListResults != null)
            await DoSearchAsync(0);
    }

    private async void OnSortChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SortCombo.IsLoaded && ListResults != null && Source.SupportsCategoryAndSort)
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
            var result = await Source.ListAsync(page, SearchBox.Text, SelectedCategory, SelectedSort, SelectedVersionId);

            _page = result.Page;
            _totalPages = result.HasMore ? page + 2 : page + 1;

            ListResults.ItemsSource = result.Items.Select(i => new BrowseRow { Entry = i }).ToList();
            PageText.Text = $"第 {page + 1} 页";
            PrevPageButton.IsEnabled = page > 0;
            NextPageButton.IsEnabled = result.HasMore;

            if (!string.IsNullOrEmpty(result.Notice))
                ShowInfo(result.Notice, InfoBarSeverity.Informational, "插件源说明");
            else if (result.Items.Count == 0)
                ShowInfo("没有找到匹配的插件。", InfoBarSeverity.Informational);
        }
        catch (Exception ex)
        {
            ShowInfo($"搜索失败：{ex.Message}", InfoBarSeverity.Error);
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
            ShowBusy($"正在安装 {row.Entry.Name}…");

            var download = await Source.ResolveDownloadAsync(row.Entry);
            var folders = await WowInstallService.DownloadAndInstallAsync(download.Url, AddonsDir);

            btn.Content = "已安装";
            ShowInfo($"已安装 {row.Entry.Name}（{download.FileName}）到 {AddonsDir}。" +
                     (folders.Length > 0 ? $" 包含插件: {string.Join(", ", folders)}（旧版本已自动改名为 *.bak-* 保留）" : ""),
                InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            btn.IsEnabled = true;
            btn.Content = originalContent;
            ShowInfo($"安装 {row.Entry.Name} 失败：{ex.Message}", InfoBarSeverity.Error);
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
