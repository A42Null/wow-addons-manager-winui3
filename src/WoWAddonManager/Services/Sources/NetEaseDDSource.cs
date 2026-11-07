using System.Net.Http;
using System.Text.Json;

namespace WoWAddonManager.Services.Sources;

/// <summary>
/// 网易DD (dd.163.com) 插件源 —— 接入现状（受限）：
/// 已从客户端 NetEaseDD/ 目录与官方配置接口解析到：
///   - 游戏版本配置：GET https://uiapi.w.163.com/game_type/list
///     （game_type：10001=正式服, 2=泰坦重铸(3.8.0), 7=无限服, 3=经典怀旧服, 6=熊猫人之谜, 4=燃烧的远征）
///   - 插件市场 Web 端：https://dd.163.com/dd-wow-tools/（umi SPA，数据经由客户端桥接/登录态接口）
///   - 客户端数据 API 主机：https://ds-api.w.163.com（端点未公开、需登录态与签名）
/// 插件市场列表/搜索/下载接口均需 DD 客户端登录态，未公开文档；本源暂时只提供说明，
/// 待抓包确认端点后按 IAddonSource 补全即可接入。
/// </summary>
public class NetEaseDDSource : IAddonSource
{
    public const string Notice =
        "网易DD 插件市场接口需 DD 客户端登录态（未公开）。" +
        "已从客户端解析出版本映射：10001=正式服, 2=泰坦重铸, 7=无限服, 3=经典怀旧服, 6=熊猫人之谜, 4=燃烧的远征；" +
        "待官方开放接口或提供抓包数据后即可补全此源。";

    public string Id => "netease-dd";
    public string DisplayName => "网易DD (待接入)";
    public bool SupportsCategoryAndSort => false;
    public IReadOnlyDictionary<int, string> VersionOptions => new Dictionary<int, string>();

    /// <summary>DD 客户端 game_type ↔ 本应用 flavor 目录映射。</summary>
    public static readonly IReadOnlyDictionary<string, int> FlavorToGameType = new Dictionary<string, int>
    {
        ["_retail_"] = 10001,
        ["_classic_titan_"] = 2,
        ["_forever_"] = 7,
        ["_classic_era_"] = 3,
        ["_classic_"] = 6,
        ["_anniversary_"] = 4,
    };

    public Task<SourceResult> ListAsync(int page, string? keyword, int? categoryId, int sortField, int? versionId)
    {
        return Task.FromResult(new SourceResult { Page = 0, HasMore = false, Notice = Notice });
    }

    public Task<SourceDownload> ResolveDownloadAsync(SourceEntry entry)
    {
        throw new NotSupportedException("网易DD 源尚未接入：插件市场接口需 DD 客户端登录态。");
    }
}
