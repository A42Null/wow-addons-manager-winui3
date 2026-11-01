using System.Net.Http;
using System.Text.Json;
using WoWAddonManager.Models;

namespace WoWAddonManager.Services;

/// <summary>
/// CurseForge API 客户端。
/// 优先使用官方 API（api.curseforge.com，需 x-api-key）；
/// 未配置 Key 时回退到社区匿名端点（www.curseforge.com/api/v1，可能不稳定）。
/// </summary>
public sealed class CurseForgeService
{
    public static CurseForgeService Instance { get; } = new();

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };

    private const string ApiBase = "https://api.curseforge.com/v1";
    private const string KeylessBase = "https://www.curseforge.com/api/v1";
    private const string DownloadHost = "https://www.curseforge.com";

    private const int WowGameId = 1;
    private const int AddonsClassId = 1;

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private string Base =>
        string.IsNullOrWhiteSpace(ConfigService.Current.CfApiKey) ? KeylessBase : ApiBase;

    public bool UseOfficialApi => Base == ApiBase;

    private async Task<string> GetAsync(string url)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.UserAgent.ParseAdd("WoWAddonManager/1.0 (Windows NT 10.0; Win64; x64)");
        req.Headers.Accept.ParseAdd("application/json");
        var key = ConfigService.Current.CfApiKey;
        if (!string.IsNullOrWhiteSpace(key))
            req.Headers.TryAddWithoutValidation("x-api-key", key);

        using var resp = await Http.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsStringAsync();
    }

    public async Task<List<CfCategory>> GetAddonCategoriesAsync()
    {
        var json = await GetAsync($"{Base}/categories?gameId={WowGameId}");
        var env = JsonSerializer.Deserialize<CfEnvelope<CfCategory>>(json, Json) ?? new();
        var list = env.Data.Where(c => c.ClassId == AddonsClassId).ToList();
        return list.Count > 0 ? list : env.Data.ToList();
    }

    public async Task<List<CfMod>> SearchAsync(string searchFilter, int? categoryId, int sortField, int page, int pageSize = 40)
    {
        var query = $"gameId={WowGameId}&classId={AddonsClassId}" +
                    $"&sortField={sortField}&sortOrder=desc&pageSize={pageSize}&pageNumber={page}";
        if (!string.IsNullOrWhiteSpace(searchFilter))
            query += "&searchFilter=" + Uri.EscapeDataString(searchFilter.Trim());
        if (categoryId is > 0)
            query += $"&categoryId={categoryId}";

        var json = await GetAsync($"{Base}/mods/search?{query}");
        var env = JsonSerializer.Deserialize<CfEnvelope<CfMod>>(json, Json) ?? new();
        return env.Data.ToList();
    }

    public async Task<CfMod> GetModAsync(int modId)
    {
        var json = await GetAsync($"{Base}/mods/{modId}");
        var env = JsonSerializer.Deserialize<CfEnvelope<CfMod>>(json, Json) ?? new();
        return env.Data.FirstOrDefault() ?? throw new InvalidOperationException($"未找到插件 {modId}");
    }

    public async Task<List<CfFile>> GetModFilesAsync(int modId)
    {
        var json = await GetAsync($"{Base}/mods/{modId}/files?pageSize=1000");
        var env = JsonSerializer.Deserialize<CfEnvelope<CfFile>>(json, Json) ?? new();
        return env.Data
            .Where(f => f.FileStatus is 4 or 2) // 4=Approved 2=Submitted
            .OrderByDescending(f => f.FileDate)
            .ToList();
    }

    private async Task<CfFile> GetFileAsync(int modId, int fileId)
    {
        var json = await GetAsync($"{Base}/mods/{modId}/file/{fileId}");
        var env = JsonSerializer.Deserialize<CfEnvelope<CfFile>>(json, Json) ?? new();
        return env.Data.FirstOrDefault() ?? throw new InvalidOperationException($"未找到文件 {fileId}");
    }

    /// <summary>为指定客户端版本挑选最合适的文件：优先正式版、按发布时间倒序、匹配游戏版本模式。</summary>
    public static CfFile? PickBestFile(IEnumerable<CfFile> files, string flavorFolder)
    {
        var candidates = files.Where(f => f.ReleaseType != 3)
                              .OrderByDescending(f => f.FileDate)
                              .ToList();
        if (candidates.Count == 0) return null;

        var patterns = VersionPatternsFor(flavorFolder);
        if (patterns.Length > 0)
        {
            var matched = candidates.FirstOrDefault(f =>
                f.GameVersions.Any(gv => patterns.Any(p => gv.StartsWith(p, StringComparison.OrdinalIgnoreCase))));
            if (matched != null) return matched;
        }
        return candidates[0];
    }

    private static string[] VersionPatternsFor(string flavor) => flavor switch
    {
        "_retail_" or "_retail_ptr_" or "_beta_" => ["11.", "12."],          // 正式服
        "_classic_" or "_classic_ptr_"          => ["5."],                    // 熊猫人之谜怀旧服
        "_classic_titan_"                        => ["3.80"],                  // 泰坦重铸时光服（WLK 3.80.x 国服）
        "_anniversary_"                          => ["2."],                    // 周年纪念服（TBC）
        "_classic_era_" or "_classic_era_ptr_"   => ["1.14", "1.15"],          // 永久60 / 旧世经典
        _ => []
    };

    /// <summary>解析文件真实下载地址（相对路径需补域名；缺失时补查一次文件详情）。</summary>
    public async Task<string> ResolveDownloadUrlAsync(int modId, CfFile file)
    {
        var url = file.DownloadUrl;
        if (string.IsNullOrEmpty(url))
        {
            var full = await GetFileAsync(modId, file.Id);
            url = full.DownloadUrl;
        }
        if (string.IsNullOrEmpty(url))
            throw new InvalidOperationException("该文件没有可用的下载地址（可能为付费通道文件）");

        return url.StartsWith('/') ? DownloadHost + url : url;
    }
}
