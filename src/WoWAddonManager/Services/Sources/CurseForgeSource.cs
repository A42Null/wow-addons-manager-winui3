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
            Items = mods
                .Where(m => !SourceFilter.IsBlocked(m.Name)) // 过滤违规插件
                .Select(m =>
                {
                    var latest = m.LatestFiles
                        .Where(f => f.ReleaseType != 3)
                        .OrderByDescending(f => f.FileDate)
                        .FirstOrDefault();
                    return new SourceEntry
                    {
                        Id = m.Id.ToString(),
                        Name = m.Name,
                        Summary = m.Summary,
                        Author = m.Authors.Count > 0 ? string.Join(", ", m.Authors.Select(a => a.Name)) : "",
                        Extra = $"下载 {m.DownloadCount:N0}",
                        Version = latest is null ? "" :
                            string.IsNullOrWhiteSpace(latest.DisplayName) ? latest.FileName : latest.DisplayName,
                        Updated = m.DateModified.LocalDateTime.ToString("yyyy-MM-dd HH:mm"),
                        GameVersions = latest is null ? "" : string.Join(", ", latest.GameVersions.Take(3)),
                        LogoUrl = m.Logo?.ThumbnailUrl,
                    };
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
