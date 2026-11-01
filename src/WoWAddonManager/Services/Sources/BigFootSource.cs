using System.Net.Http;
using System.Text.Json;

namespace WoWAddonManager.Services.Sources;

/// <summary>
/// 大脚整合包源：大脚官方在 Gitee (gitee.com/bigfoot-wow) 按客户端版本分仓库发布 Releases，
/// 走 Gitee 开放 API v5，无需鉴权。
/// 接入方式：
///   仓库列表 = org 下固定仓库（retail/classic/classic_titan/anniversary/classic_era/forever/collections）
///   最新版   = GET https://gitee.com/api/v5/repos/bigfoot-wow/{repo}/releases?per_page=1
///   下载     = release assets 中 full-*.zip（browser_download_url，整包 zip，解压到 AddOns 即可）
/// </summary>
public class BigFootSource : IAddonSource
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    private const string Org = "bigfoot-wow";
    private const string Api = "https://gitee.com/api/v5";

    private static readonly (string repo, string name, string flavor)[] Repos =
    [
        ("retail",        "大脚整合包 · 正式服",          "_retail_"),
        ("classic",       "大脚整合包 · 熊猫人之谜",      "_classic_"),
        ("classic_titan", "大脚整合包 · 泰坦重铸 (WLK)",  "_classic_titan_"),
        ("anniversary",   "大脚整合包 · 燃烧的远征周年",  "_anniversary_"),
        ("classic_era",   "大脚整合包 · 永久60",          "_classic_era_"),
        ("forever",       "大脚整合包 · Forever",         "_forever_"),
        ("collections",   "大脚 · 合集",                  ""),
    ];

    public string Id => "bigfoot";
    public string DisplayName => "大脚整合包";
    public bool SupportsCategoryAndSort => false;
    public IReadOnlyDictionary<int, string> VersionOptions => new Dictionary<int, string>();

    public Task<SourceResult> ListAsync(int page, string? keyword, int? categoryId, int sortField, int? versionId)
    {
        if (page > 0) return Task.FromResult(new SourceResult { Page = page, HasMore = false });

        var flavor = ConfigService.Current.Flavor;
        var items = Repos.Select(r => new SourceEntry
        {
            Id = r.repo,
            Name = r.name,
            Summary = $"Gitee 仓库 bigfoot-wow/{r.repo} 的最新 Release 整包",
            Author = "大脚 (BigFoot)",
            Extra = r.flavor.Length > 0 && r.flavor == flavor ? "★ 匹配当前客户端" : r.flavor,
        }).ToList();

        if (!string.IsNullOrWhiteSpace(keyword))
            items = items.Where(i =>
                i.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                i.Id.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList();

        return Task.FromResult(new SourceResult { Page = 0, HasMore = false, Items = items });
    }

    public async Task<SourceDownload> ResolveDownloadAsync(SourceEntry entry)
    {
        var url = $"{Api}/repos/{Org}/{entry.Id}/releases?per_page=1";
        using var resp = await Http.GetAsync(url);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
            throw new InvalidOperationException($"仓库 {entry.Id} 暂无 Release");

        var release = doc.RootElement[0];
        var tag = release.TryGetProperty("tag_name", out var t) ? t.GetString() : "";
        foreach (var asset in release.GetProperty("assets").EnumerateArray())
        {
            var an = asset.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            var ad = asset.TryGetProperty("browser_download_url", out var d) ? d.GetString() ?? "" : "";
            if (an.StartsWith("full-") && an.EndsWith(".zip") && ad.Length > 0)
                return new SourceDownload { FileName = $"{entry.Name} {tag} ({an})", Url = ad };
        }
        throw new InvalidOperationException($"Release {tag} 中没有 full-*.zip 整包");
    }
}
