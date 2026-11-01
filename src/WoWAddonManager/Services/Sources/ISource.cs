namespace WoWAddonManager.Services.Sources;

/// <summary>插件源中的一个可安装条目。</summary>
public class SourceEntry
{
    /// <summary>源内唯一 ID（CurseForge modId / 新手盒子 PluginItem id / 大脚仓库名）。</summary>
    public string Id { get; init; } = "";

    public string Name { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Author { get; set; } = "";
    /// <summary>附加展示信息（下载量 / 版本 / 适用客户端等）。</summary>
    public string Extra { get; set; } = "";
    /// <summary>缩略图（可空）。</summary>
    public string? LogoUrl { get; set; }
}

public class SourceResult
{
    public List<SourceEntry> Items { get; init; } = [];
    public int Page { get; init; }
    public bool HasMore { get; init; }
}

/// <summary>插件下载信息。</summary>
public class SourceDownload
{
    public string FileName { get; init; } = "";
    public string Url { get; init; } = "";
}

public interface IAddonSource
{
    string Id { get; }
    string DisplayName { get; }
    /// <summary>是否支持分类/排序筛选（仅 CurseForge）。</summary>
    bool SupportsCategoryAndSort { get; }
    /// <summary>版本筛选下拉框选项（id → 名称），空字典表示不支持。</summary>
    IReadOnlyDictionary<int, string> VersionOptions { get; }

    Task<SourceResult> ListAsync(int page, string? keyword, int? categoryId, int sortField, int? versionId);

    Task<SourceDownload> ResolveDownloadAsync(SourceEntry entry);
}
