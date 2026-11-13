# 魔兽世界插件管理器 (WoW Addon Manager)

WinUI 3 桌面应用：浏览 / 安装 / 更新 / 卸载《魔兽世界》CurseForge 插件。

## 下载

- Release：https://github.com/A42Null/wow-addons-manager-winui3/releases （`WoWAddonManager-win-x64.zip`）
- 解压即用，无需安装运行时（自包含 Windows App SDK + .NET 8）。
- 要求：Windows 10 1809+ / Windows 11，x64。

## 功能

| 页面 | 功能 |
|---|---|
| 已安装插件 | 扫描 AddOns 目录、读 .toc（Title/Version/X-Curse-Project-ID）、检查更新、单个/批量更新、删除、打开目录 |
| 浏览 CurseForge | 搜索、分类过滤、按下载量/热门/更新时间排序、翻页、一键安装最新兼容版 |
| 设置 | WoW 目录自动检测（注册表+常见路径）/手动选择、客户端版本切换、CurseForge API Key |

## 客户端版本识别

| 目录 | 含义 | 兼容文件匹配 |
|---|---|---|
| `_retail_` | 正式服 | 游戏版本 11.x / 12.x |
| `_classic_` | 熊猫人之谜怀旧服 | 5.x |
| `_classic_titan_` | 泰坦重铸时光服（WLK 3.80.x 国服） | 3.80.x |
| `_anniversary_` | 周年纪念服（TBC） | 2.x |
| `_classic_era_` | 永久60（旧世经典/专家模式） | 1.14 / 1.15 |

## CurseForge API

- 推荐在「设置」中填写免费 API Key：https://console.curseforge.com/#/api-keys/registration
- 未填写时回退社区匿名端点 `www.curseforge.com/api/v1`（可能被限流）。
- 更新检查依赖 .toc 中的 `X-Curse-Project-ID`；未关联的插件无法自动更新。

## 插件源

浏览页左上角可切换源（搜索/安装均走所选源）：

| 源 | 接入方式 | 说明 |
|---|---|---|
| CurseForge | 官方 API（需免费 Key）+ 匿名端点回退 | 搜索/分类/排序/分页，支持更新检查 |
| 新手盒子 (wclbox.com) | 无需 Key。列表页 `https://www.wclbox.com/games/1?aN=plugin&version={v}&pn={页}` 服务端渲染，页面内嵌 JSON-LD ItemList（插件名+ID）；下载走同源接口 `GET /api/download/PluginItem/{id}` 返回 `{name,size,url}` CDN 直链 | 版本筛选（可在浏览页直接切换，按「设置」的客户端版本自动选中）：2=正式服 4=泰坦重铸 6=经典旧世 7=熊猫人之谜 8=燃烧的远征 9=无限；探索赛季(3)不提供。**注意**：非默认版本的条目 URL 带 `?version=N` 后缀，解析需兼容；关键词搜索为拉取前 20 页（600 条）后本地过滤，若筛选版本无命中会自动回退全部版本并提示 |
| 大脚插件站 (wowui.178.com) | 无需 Key。列表/搜索 `GET /wow/0_37499/d/{偏移}?k={关键词}`（10条/页 SSR）；详情页 `/ui/{id}` 内嵌 `var resp=[{file_id,file_name,version,update_time,main_game_version}]`（首条最新，末条时间=首次发布）；下载直链 `GET /down/{ui_id}/{file_id}/{file_name}` | 列表展示版本号/首发/更新时间/适配游戏版本；支持版本筛选（按名称前缀 + `main_game_version` 判定，无法判定的条目宽松保留） |
| 大脚整合包 (Gitee) | Gitee 开放 API v5（无需鉴权）：`GET https://gitee.com/api/v5/repos/bigfoot-wow/{repo}/releases?per_page=1`，下载 assets 中 `full-*.zip` | 仓库：retail/classic/classic_titan/anniversary/classic_era/forever/collections |
| 网易DD (dd.163.com) | **受限**：插件市场接口需 DD 客户端登录态（未公开）。已解析：`GET https://uiapi.w.163.com/game_type/list`（10001=正式服 2=泰坦重铸 7=无限服 3=经典怀旧服 6=熊猫人之谜 4=燃烧的远征）、市场 Web 端 `dd.163.com/dd-wow-tools/`（umi SPA，数据经客户端桥接）、数据 API 主机 `ds-api.w.163.com`（端点未公开） | 应用内显示接入说明；抓包确认端点后按 `IAddonSource` 补全 `NetEaseDDSource` 即可 |

### 违规插件过滤

CurseForge / 大脚插件站 / 大脚整合包源的列表会自动隐藏名称包含以下关键字的插件：
`BiaoGe`、`BiaoGeX`、`TuanJian`、`ZongLan`、`HanHua`。

### 版本对应规则（ Forever = 无限 ）

`_forever_` ↔ 无限服：新手盒子 version=9，大脚 Gitee 仓库 `forever`，网易DD game_type=7。

### 网易DD（dd.163.com）接入说明

网易DD 的插件库数据仅通过其**桌面客户端私有协议**获取（无公开 API、无开发者平台）。已逆向到的信息：

1. 客户端配置接口 `https://uiapi.w.163.com/game_type/list`：含全部魔兽版本映射与官方 163UI_Info.zip；
2. 插件市场 Web 端为 `https://dd.163.com/dd-wow-tools/`（umi SPA），运行于 DD 客户端 WebView 内，列表/安装数据经客户端 IPC 桥接；
3. 客户端数据 API 主机 `https://ds-api.w.163.com`，端点未公开、需登录态与签名。

下一步：登录 DD 客户端抓包（Fiddler/mitmproxy）确认市场端点后，在 `Services/Sources/NetEaseDDSource.cs` 补全 `ListAsync`/`ResolveDownloadAsync` 并在 `BrowsePage.Sources` 中保留注册即可。

### 安装备份

安装/更新插件时，AddOns 下已存在的同名插件目录会自动改名为 `目录名.bak-时间戳` 保留（不删除），新版解压为全新目录。

## 云构建

- GitHub Actions（windows-latest + VS MSBuild）：`.github/workflows/build.yml`
  - push main 自动构建并更新 `dev-build` 滚动 Release；push tag `v*` 自动发正式 Release。
  - MSI 由 **Advanced Installer**（读取仓库根目录 `WoW WinUI3 Addons Manager.aip`）构建；本机无 AI 时回退 WiX v5（`installer/build-msi.ps1`）。
  - `publish-cnb` job：tag 构建时（或手动触发 workflow_dispatch）用仓库密钥 `CNB_TOKEN` 把 zip + MSI 发布为 CNB Release。
- CNB（cnb.cool）：`.cnb.yml` 流水线等待 GitHub Actions 完成后，把产物收集到 CNB 仓库 `dist` 分支（`build/WoWAddonManager-win-x64.zip`、`build/WoWAddonManager-<版本>-x64.msi`），从 CNB 即可下载。
  - CNB Runner 为 Linux，WinUI3 XAML 编译必须 Windows，故编译在 GitHub Actions，CNB 负责产物分发。

## 许可证

[MIT](LICENSE) — Copyright (c) 2026 A42Null

## 本地开发

```bash
dotnet build src/WoWAddonManager/WoWAddonManager.csproj -c Release -p:Platform=x64
# 或用 Visual Studio（需要 .NET 桌面开发 + WinUI 工作负载）打开 WoWAddonManager.slnx
```

注意：`dotnet build` 的 XAML 编译链偶发崩溃（XamlCompiler exit 1），失败时改用 VS MSBuild。
