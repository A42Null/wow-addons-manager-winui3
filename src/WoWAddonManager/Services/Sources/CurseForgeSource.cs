using WoWAddonManager.Models;

namespace WoWAddonManager.Services.Sources;

public class CurseForgeSource : IAddonSource
{
    private readonly CurseForgeService _cf = CurseForgeService.Instance;

    public string Id => "curseforge";
    public string DisplayName => "CurseForge";
    public bool SupportsCategoryAndSort => true;
    public IReadOnlyDictionary<int, string> VersionOptions => new Dictionary<int, string>();

    public async Task<SourceResult> ListAsync(int page, string? keyword, int? categoryId, int sortField, int? versionId)
    {
        var mods = await _cf.SearchAsync(keyword ?? "", categoryId, sortField, page);
        return new SourceResult
        {
            Page = page,
            HasMore = mods.Count >= 40,
            Items = mods.Select(m => new SourceEntry
            {
                Id = m.Id.ToString(),
                Name = m.Name,
                Summary = m.Summary,
                Author = m.Authors.Count > 0 ? string.Join(", ", m.Authors.Select(a => a.Name)) : "",
                Extra = $"下载 {m.DownloadCount:N0} · 更新于 {m.DateModified.LocalDateTime:yyyy-MM-dd}",
                LogoUrl = m.Logo?.ThumbnailUrl,
            }).ToList(),
        };
    }

    public async Task<SourceDownload> ResolveDownloadAsync(SourceEntry entry)
    {
        var modId = int.Parse(entry.Id);
        var files = await _cf.GetModFilesAsync(modId);
        var file = CurseForgeService.PickBestFile(files, ConfigService.Current.Flavor)
                   ?? throw new InvalidOperationException("没有适配当前客户端的可用文件");
        var url = await _cf.ResolveDownloadUrlAsync(modId, file);
        return new SourceDownload
        {
            FileName = string.IsNullOrWhiteSpace(file.DisplayName) ? file.FileName : file.DisplayName,
            Url = url,
        };
    }
}
