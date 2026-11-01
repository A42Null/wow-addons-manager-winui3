using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using WoWAddonManager.Models;

namespace WoWAddonManager.ViewModels;

public class AddonRow : INotifyPropertyChanged
{
    public InstalledAddon Addon { get; init; } = new();

    public string Title => string.IsNullOrWhiteSpace(Addon.Title) ? Addon.FolderName : Addon.Title;
    public string FolderName => Addon.FolderName;
    public string Version => string.IsNullOrWhiteSpace(Addon.Version) ? "未知版本" : Addon.Version;
    public string ProjectText => Addon.CurseProjectId > 0 ? $"CurseForge #{Addon.CurseProjectId}" : "未关联 CurseForge";

    private string _remoteVersion = "";
    public string RemoteVersion
    {
        get => _remoteVersion;
        set { _remoteVersion = value; OnPropertyChanged(); }
    }

    private bool _hasUpdate;
    public bool HasUpdate
    {
        get => _hasUpdate;
        set
        {
            _hasUpdate = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasUpdateVisibility));
        }
    }

    public Visibility HasUpdateVisibility => HasUpdate ? Visibility.Visible : Visibility.Collapsed;

    private string _status = "";
    public string StatusText
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name ?? ""));
}

public class BrowseRow
{
    public CfMod Mod { get; init; } = new();

    public ImageSource? Logo =>
        string.IsNullOrEmpty(Mod.Logo?.ThumbnailUrl) ? null : new BitmapImage(new Uri(Mod.Logo.ThumbnailUrl));

    public Visibility LogoVisibility => Logo == null ? Visibility.Collapsed : Visibility.Visible;

    public string Name => Mod.Name;
    public string Summary => Mod.Summary;

    public string AuthorText => Mod.Authors.Count > 0
        ? "作者: " + string.Join(", ", Mod.Authors.Select(a => a.Name))
        : "";

    public string DownloadText => $"下载 {Mod.DownloadCount:N0}";

    public string UpdatedText => $"更新于 {Mod.DateModified.LocalDateTime:yyyy-MM-dd}";
}

public class CategoryOption
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
}
