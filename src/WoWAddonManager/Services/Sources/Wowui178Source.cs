using System.Net.Http;
using System.Text.Json;

namespace WoWAddonManager.Services.Sources;

/// <summary>
/// 大脚插件站 (wowui.178.com) 源。
/// 接入方式（无需鉴权）：
///   列表/搜索：GET /wow/0_37499/d/{偏移}?k={关键词}（每页 10 条，SSR HTML）
///   详情：GET /ui/{id} —— 页面内嵌 JS 数组 var resp = [{file_id, file_name, version, update_time, main_game_version}, ...]
///         首条为最新文件；末条时间为首次发布日期
///   下载：GET /down/{ui_id}/{file_id}/{file_name}（application/zip 直链）
///
/// 站点改版说明：列表条目结构为
///   &lt;div class="item"&gt;&lt;div class="box"&gt;
///     &lt;span class="pic"&gt;&lt;img src="/data/wow/{id}/ui_logo.jpg"&gt;&lt;/span&gt;
///     &lt;h2 class="h2"&gt;&lt;a href="/ui/{id}"&gt;名称&lt;/a&gt;&lt;/h2&gt;
///     &lt;div class="other"&gt;&lt;span class="down"&gt;下载量&lt;/span&gt;&lt;span class="date"&gt;2026/11/03&lt;/span&gt;...&lt;/div&gt;
///     &lt;div class="dig"&gt;描述&lt;/div&gt;
///     &lt;a ... onclick="downloadui(id,fileId,'xxx.zip')"&gt;下载&lt;/a&gt;
///   &lt;/div&gt;&lt;/div&gt;
/// 解析按 item 块切分，避免依赖标签顺序。
/// </summary>
public class Wowui178Source : IAddonSource
{
    private static readonly HttpClient Http = CreateHttp();

    private const string Base = "https://wowui.178.com";
    private const int PageSize = 10;
    /// <summary>全部插件分类（站点导航中的“全部插件”）。</summary>
    private const string AllCategory = "0_37499";

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
        h.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "zh-CN,zh;q=0.9");
        return h;
    }

    public string Id => "wowui178";
    public string DisplayName => "大脚插件站 (178)";
    public bool SupportsCategoryAndSort => false;

    /// <summary>版本 id 与客户端版本对应关系（与设置页 flavor 语义一致）。</summary>
    public IReadOnlyDictionary<int, string> VersionOptions { get; } = new Dictionary<int, string>
    {
        [0] = "全部版本",
        [1] = "正式服",
        [2] = "永久60 (怀旧服)",
        [3] = "经典旧世",
        [4] = "熊猫人之谜",
        [5] = "燃烧的远征 (周年)",
        [6] = "泰坦重铸",
    };

    /// <summary>flavor 目录 → 178 版本 id（与 VersionOptions 的 key 一致）。</summary>
    public static readonly IReadOnlyDictionary<string, int> FlavorToVersion = new Dictionary<string, int>
    {
        ["_retail_"] = 1,
        ["_retail_ptr_"] = 1,
        ["_beta_"] = 1,
        ["_classic_era_"] = 2,
        ["_classic_era_ptr_"] = 2,
        ["_classic_"] = 4,
        ["_anniversary_"] = 5,
        ["_classic_titan_"] = 6,
    };

    private static readonly System.Text.RegularExpressions.Regex RespRx = new(
        @"var\s+resp\s*=\s*(\[\{.*?\}\]);",
        System.Text.RegularExpressions.RegexOptions.Singleline);

    private async Task<string> GetPageAsync(string url)
    {
        Exception? last = null;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                using var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                if (resp.IsSuccessStatusCode)
                {
                    var html = await resp.Content.ReadAsStringAsync();
                    if (html.Length > 0) return html;
                }
                else
                {
                    last = new HttpRequestException($"HTTP {(int)resp.StatusCode}");
                }
            }
            catch (Exception ex) { last = ex; }
            await Task.Delay(500 * (attempt + 1));
        }
        throw new InvalidOperationException($"大脚插件站访问失败（{last?.Message ?? "网络异常"}），请稍后重试", last);
    }

    /// <summary>解析列表页条目。</summary>
    private static List<SourceEntry> ParseItems(string html)
    {
        var result = new List<SourceEntry>();
        var blocks = html.Split(new[] { "<div class=\"item\">" }, StringSplitOptions.None);
        for (int i = 1; i < blocks.Length; i++)
        {
            var b = blocks[i];
            var idM = System.Text.RegularExpressions.Regex.Match(b, @"href=""/ui/(?<id>\d+)""");
            if (!idM.Success) continue;
            var id = idM.Groups["id"].Value;

            var nameM = System.Text.RegularExpressions.Regex.Match(b, @"<h2 class=""h2"">\s*<a[^>]*>(?<n>.*?)</a>", System.Text.RegularExpressions.RegexOptions.Singleline);
            var name = System.Net.WebUtility.HtmlDecode(nameM.Groups["n"].Value).Trim();

            var logoM = System.Text.RegularExpressions.Regex.Match(b, @"<img src=""(?<l>/data/wow/\d+/[^""]+)""");
            var downM = System.Text.RegularExpressions.Regex.Match(b, @"<span class=""down"">(?<v>[\d,]+)</span>");
            var dateM = System.Text.RegularExpressions.Regex.Match(b, @"<span class=""date"">(?<v>[\d/]+)</span>");
            var digM = System.Text.RegularExpressions.Regex.Match(b, @"<div class=""dig"">(?<v>.*?)</div>", System.Text.RegularExpressions.RegexOptions.Singleline);

            result.Add(new SourceEntry
            {
                Id = id,
                Name = name,
                Summary = System.Net.WebUtility.HtmlDecode(digM.Groups["v"].Value).Trim(),
                Author = "178 大脚",
                Extra = downM.Success ? $"下载 {downM.Groups["v"].Value}" : "",
                Updated = dateM.Success ? dateM.Groups["v"].Value.Replace('/', '-') : "",
                LogoUrl = logoM.Success ? Base + logoM.Groups["l"].Value : null,
            });
        }
        return result;
    }

    /// <summary>解析详情页内嵌文件数组。</summary>
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

    private async Task<SourceEntry> EnrichAsync(SourceEntry entry)
    {
        try
        {
            var html = await GetPageAsync($"{Base}/ui/{entry.Id}");
            var files = ParseDetailFiles(html);
            if (files is { Count: > 0 })
            {
                var latest = files[0];
                entry.Version = string.IsNullOrWhiteSpace(latest.version) ? latest.file_name : latest.version;
                entry.Updated = FormatDate(latest.update_time);
                entry.GameVersions = latest.main_game_version;
                entry.FirstReleased = FormatDate(files[^1].update_time); // 最早一条 = 首次发布
            }
        }
        catch { /* 详情失败不阻塞列表 */ }
        return entry;
    }

    private static string FormatDate(string s) =>
        string.IsNullOrWhiteSpace(s) ? "" : s.Split(' ')[0].Replace('/', '-');

    /// <summary>
    /// 依据插件名前缀与适配游戏版本推断所属客户端版本。
    /// 返回 VersionOptions 中的 id；0 = 无法判定（视为通用，始终保留）。
    /// </summary>
    public static int GuessClientVersion(string name, string mainGameVersion)
    {
        var n = name.TrimStart();
        if (n.StartsWith("["))
        {
            var end = n.IndexOf(']');
            if (end > 0)
            {
                var tag = n[1..end];
                if (tag.Contains("怀旧服")) return 2;      // 永久60
                if (tag.Contains("经典旧世")) return 3;
                if (tag.Contains("熊猫人")) return 4;
                if (tag.Contains("燃烧") || tag.Contains("周年") || tag.Contains("TBC")) return 5;
                if (tag.Contains("泰坦") || tag.Contains("时光服")) return 6;
            }
        }

        // 无前缀：依据适配的游戏版本号主版本判断
        var mgv = mainGameVersion.Trim();
        if (mgv.Length > 0)
        {
            var major = mgv.Split('.')[0];
            if (int.TryParse(major, out var m))
            {
                return m switch
                {
                    1 => 2,      // 1.13~1.15 永久60
                    2 => 5,      // 2.x TBC
                    3 => 6,      // 3.8x 泰坦
                    4 or 5 => 4, // 4.x/5.x 熊猫人
                    >= 10 => 1,  // 10.x/11.x/12.x 正式服
                    _ => 0,
                };
            }
        }
        return 0;
    }

    public async Task<SourceResult> ListAsync(int page, string? keyword, int? categoryId, int sortField, int? versionId)
    {
        var offset = page * PageSize;
        var url = $"{Base}/wow/{AllCategory}/d/{offset}";
        if (!string.IsNullOrWhiteSpace(keyword))
            url += $"?k={Uri.EscapeDataString(keyword.Trim())}";

        var html = await GetPageAsync(url);

        var raw = ParseItems(html);
        var hasMore = raw.Count >= PageSize;

        // 补齐版本号 / 首次发布 / 适配游戏版本（每页 10 条，并行）
        if (raw.Count > 0)
            await Task.WhenAll(raw.Select(EnrichAsync).ToArray());

        var items = raw.Where(i => !SourceFilter.IsBlocked(i.Name)).ToList();

        // 版本筛选（宽松：无法判定的条目保留）
        string? notice = null;
        if (versionId is > 0)
        {
            var filtered = items.Where(i =>
            {
                var v = GuessClientVersion(i.Name, i.GameVersions);
                return v == 0 || v == versionId; // 0 = 无法判定，宽松保留
            }).ToList();

            if (filtered.Count == 0 && items.Count > 0)
            {
                notice = "该版本下没有匹配的插件，已显示全部版本的插件。";
                filtered = items;
            }
            items = filtered;
        }

        return new SourceResult { Page = page, HasMore = hasMore, Items = items, Notice = notice };
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
