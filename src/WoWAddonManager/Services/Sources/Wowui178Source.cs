using System.Net.Http;
using System.Text.Json;

namespace WoWAddonManager.Services.Sources;

/// <summary>
/// 大脚插件站 (wowui.178.com) 源。
/// 接入方式（无需鉴权）：
///   列表/搜索：GET /wow/0_34676/d/{偏移}?k={关键词}（每页 10 条，SSR HTML）
///   详情：GET /ui/{id} —— 页面内嵌 JS 数组 var resp = [{file_id, file_name, version, update_time, main_game_version}, ...]
///         首条为最新文件；末条时间为首次发布日期
///   下载：GET /down/{ui_id}/{file_id}/{file_name}（application/zip 直链）
/// </summary>
public class Wowui178Source : IAddonSource
{
    private static readonly HttpClient Http = CreateHttp();

    private const string Base = "https://wowui.178.com";
    private const int PageSize = 10;

    private class FileEntry
    {
        public string file_id { get; set; } = "";
        public string file_name { get; set; } = "";
        public string version { get; set; } = "";
        public string update_time { get; set; } = "";
        public string main_game_version { get; set; } = "";
    }

    private static HttpClient CreateHttp()
    {
        var h = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        h.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36");
        return h;
    }

    public string Id => "wowui178";
    public string DisplayName => "大脚插件站 (178)";
    public bool SupportsCategoryAndSort => false;
    public IReadOnlyDictionary<int, string> VersionOptions => new Dictionary<int, string>();

    private static readonly System.Text.RegularExpressions.Regex ItemRx = new(
        @"<a\s+href=""/ui/(?<id>\d+)""[^>]*>.*?<span class=""num"">\d+</span>" +
        @"<span class=""tit"">(?<name>.*?)</span><span class=""dig"">(?<desc>.*?)</span>" +
        @"<span class=""size"">(?<dl>[\d,]+)</span>",
        System.Text.RegularExpressions.RegexOptions.Singleline);

    private static readonly System.Text.RegularExpressions.Regex RespRx = new(
        @"var\s+resp\s*=\s*(\[\{.*?\}\]);",
        System.Text.RegularExpressions.RegexOptions.Singleline);

    private async Task<string> GetPageAsync(string url)
    {
        for (int attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                using var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                if (resp.IsSuccessStatusCode)
                    return await resp.Content.ReadAsStringAsync();
            }
            catch { }
            await Task.Delay(500);
        }
        return "";
    }

    /// <summary>解析详情页内嵌文件数组。返回 (文件列表)。</summary>
    private static List<FileEntry>? ParseDetailFiles(string html)
    {
        var m = RespRx.Match(html);
        if (!m.Success) return null;
        try
        {
            return JsonSerializer.Deserialize<List<FileEntry>>(m.Groups[1].Value);
        }
        catch
        {
            return null;
        }
    }

    private async Task<SourceEntry?> EnrichAsync(SourceEntry entry)
    {
        try
        {
            var html = await GetPageAsync($"{Base}/ui/{entry.Id}");
            var files = ParseDetailFiles(html);
            if (files is { Count: > 0 })
            {
                var latest = files[0];
                entry.Version = string.IsNullOrWhiteSpace(latest.version) ? latest.file_name : latest.version;
                entry.Updated = latest.update_time;
                entry.GameVersions = latest.main_game_version;
                entry.FirstReleased = files[^1].update_time; // 最早一条 = 首次发布
            }
        }
        catch { /* 详情失败不阻塞列表 */ }
        return entry;
    }

    public async Task<SourceResult> ListAsync(int page, string? keyword, int? categoryId, int sortField, int? versionId)
    {
        var offset = page * PageSize;
        var url = $"{Base}/wow/0_34676/d/{offset}";
        if (!string.IsNullOrWhiteSpace(keyword))
            url += $"?k={Uri.EscapeDataString(keyword.Trim())}";

        var html = await GetPageAsync(url);
        if (html.Length == 0)
            throw new InvalidOperationException("大脚插件站暂时无法访问（可能被限流），请稍后重试");

        var items = new List<SourceEntry>();
        foreach (System.Text.RegularExpressions.Match m in ItemRx.Matches(html))
        {
            var name = System.Net.WebUtility.HtmlDecode(m.Groups["name"].Value).Trim();
            if (SourceFilter.IsBlocked(name)) continue; // 过滤违规插件
            items.Add(new SourceEntry
            {
                Id = m.Groups["id"].Value,
                Name = name,
                Summary = System.Net.WebUtility.HtmlDecode(m.Groups["desc"].Value).Trim(),
                Author = "178 大脚",
                Extra = $"下载 {m.Groups["dl"].Value}",
            });
        }

        var hasMore = items.Count >= PageSize;
        if (items.Count > 0)
        {
            // 并行补齐版本号 / 更新时间 / 首次发布 / 适配游戏版本（每页 10 条）
            var tasks = items.Select(EnrichAsync).ToArray();
            await Task.WhenAll(tasks);
        }

        return new SourceResult { Page = page, HasMore = hasMore, Items = items };
    }

    public async Task<SourceDownload> ResolveDownloadAsync(SourceEntry entry)
    {
        var html = await GetPageAsync($"{Base}/ui/{entry.Id}");
        var files = ParseDetailFiles(html)
                    ?? throw new InvalidOperationException("未获取到文件列表");
        var latest = files[0];
        return new SourceDownload
        {
            FileName = $"{entry.Name} {latest.version} ({latest.file_name})",
            Url = $"{Base}/down/{entry.Id}/{latest.file_id}/{latest.file_name}",
        };
    }
}
