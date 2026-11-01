using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using WoWAddonManager.Models;
using WoWAddonManager.Services.Sources;

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
    public SourceEntry Entry { get; init; } = new();

    public ImageSource? Logo =>
        string.IsNullOrEmpty(Entry.LogoUrl) ? null : new BitmapImage(new Uri(Entry.LogoUrl));

    public Visibility LogoVisibility => Logo == null ? Visibility.Collapsed : Visibility.Visible;

    public string Name => Entry.Name;
    public string Summary => Entry.Summary;
    public string AuthorText => Entry.Author;
    public string DownloadText => Entry.Extra;
}

public class CategoryOption
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
}
