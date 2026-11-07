using System.Net.Http;
using System.Text.Json;

namespace WoWAddonManager.Services.Sources;

/// <summary>
/// 新手盒子 (wclbox.com) 源。
/// 接入方式：插件列表由 https://www.wclbox.com/games/1?aN=plugin&version={v}&pn={页} 服务端渲染，
/// 页面内嵌 JSON-LD（application/ld+json）ItemList 含插件名与详情 URL；
/// 下载走同源接口 /api/download/PluginItem/{id}，返回 JSON {name, size, url(CDN 直链)}。
/// 搜索为客户端过滤：拉取前若干页后在本地按关键词筛选。
/// </summary>
public class WclboxSource : IAddonSource
{
    private static readonly HttpClient Http = CreateHttp();

    private const string Base = "https://www.wclbox.com";
    private const int PageSize = 30;
    private const int SearchScanPages = 8;

    public string Id => "wclbox";
    public string DisplayName => "新手盒子";
    public bool SupportsCategoryAndSort => false;

    public IReadOnlyDictionary<int, string> VersionOptions { get; } = new Dictionary<int, string>
    {
        [0] = "全部版本",
        [2] = "正式服",
        [4] = "泰坦重铸",
        [6] = "经典旧世 (永久60)",
        [7] = "熊猫人之谜",
        [8] = "燃烧的远征 (周年)",
        [9] = "无限",
    };

    /// <summary>flavor 目录 → wclbox version id（探索赛季不提供）。</summary>
    public static readonly IReadOnlyDictionary<string, int> FlavorToVersion = new Dictionary<string, int>
    {
        ["_retail_"] = 2,
        ["_retail_ptr_"] = 2,
        ["_beta_"] = 2,
        ["_classic_titan_"] = 4,
        ["_classic_era_"] = 6,
        ["_classic_"] = 7,
        ["_anniversary_"] = 8,
        ["_forever_"] = 9,
    };

    private static HttpClient CreateHttp()
    {
        var h = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        h.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36");
        h.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        h.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "zh-CN,zh;q=0.9");
        h.DefaultRequestHeaders.TryAddWithoutValidation("Referer", "https://www.wclbox.com/games/1");
        return h;
    }

    /// <summary>从 SSR 页面 JSON-LD 中解析插件条目。</summary>
    private static List<SourceEntry> ParseItemList(string html)
    {
        var result = new List<SourceEntry>();
        var rx = new System.Text.RegularExpressions.Regex(
            @"\{""@type"":""ListItem"",""position"":\d+,""name"":""(?<name>(?:[^""\\]|\\.)*)"",""url"":""(?<url>[^""]*?/PluginItem/(?<id>\d+))""\}");
        foreach (System.Text.RegularExpressions.Match m in rx.Matches(html))
        {
            var name = System.Text.RegularExpressions.Regex.Unescape(m.Groups["name"].Value);
            result.Add(new SourceEntry
            {
                Id = m.Groups["id"].Value,
                Name = name,
            });
        }
        return result;
    }

    private static async Task<string> GetPageAsync(string url)
    {
        // 站点偶发 302/限流，多重试几次
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                using var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                if (resp.IsSuccessStatusCode)
                {
                    var html = await resp.Content.ReadAsStringAsync();
                    if (html.Contains("PluginItem") || html.Contains("ListItem"))
                        return html;
                }
            }
            catch { }
            await Task.Delay(600 * (attempt + 1));
        }
        return "";
    }

    public async Task<SourceResult> ListAsync(int page, string? keyword, int? categoryId, int sortField, int? versionId)
    {
        var items = new List<SourceEntry>();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            // 搜索：扫描前 N 页后本地过滤
            var seen = new HashSet<string>();
            for (int pn = 1; pn <= SearchScanPages; pn++)
            {
                var html = await GetPageAsync(BuildUrl(versionId, pn));
                foreach (var it in ParseItemList(html))
                {
                    if (SourceFilter.IsBlocked(it.Name)) continue;
                    if (seen.Add(it.Id) &&
                        it.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                        items.Add(it);
                }
                if (html.Length == 0) break;
            }
            return new SourceResult { Page = 0, HasMore = false, Items = items };
        }

        var html1 = await GetPageAsync(BuildUrl(versionId, page + 1));
        items = ParseItemList(html1).Where(i => !SourceFilter.IsBlocked(i.Name)).ToList();
        return new SourceResult
        {
            Page = page,
            HasMore = items.Count >= PageSize,
            Items = items,
        };
    }

    private static string BuildUrl(int? versionId, int pn)
    {
        var url = $"{Base}/games/1?aN=plugin";
        if (versionId is > 0) url += $"&version={versionId}";
        if (pn > 1) url += $"&pn={pn}";
        return url;
    }

    public async Task<SourceDownload> ResolveDownloadAsync(SourceEntry entry)
    {
        var json = await Http.GetStringAsync($"{Base}/api/download/PluginItem/{entry.Id}");
        using var doc = JsonDocument.Parse(json);
        var url = doc.RootElement.GetProperty("url").GetString()
                  ?? throw new InvalidOperationException("下载接口未返回 URL");
        var name = doc.RootElement.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
        return new SourceDownload { FileName = name, Url = url };
    }
}
