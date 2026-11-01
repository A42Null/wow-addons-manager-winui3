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

## 云构建

- GitHub Actions（windows-latest + VS MSBuild）：`.github/workflows/build.yml`
  - push main / tag `v*` 自动构建，产物上传 artifact，tag 构建自动发 Release。
- CNB（cnb.cool）：`.cnb.yml` 镜像推送流水线（需在 CNB 仓库配置 `GITHUB_TOKEN` 密钥）。
  - 注：CNB Runner 为 Linux，WinUI3 XAML 编译必须 Windows，故实际打包在 GitHub Actions。

## 本地开发

```bash
dotnet build src/WoWAddonManager/WoWAddonManager.csproj -c Release -p:Platform=x64
# 或用 Visual Studio（需要 .NET 桌面开发 + WinUI 工作负载）打开 WoWAddonManager.slnx
```

注意：`dotnet build` 的 XAML 编译链偶发崩溃（XamlCompiler exit 1），失败时改用 VS MSBuild。
