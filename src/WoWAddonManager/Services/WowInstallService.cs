using System.IO.Compression;
using Microsoft.Win32;

namespace WoWAddonManager.Services;

public class WowFlavor
{
    public string Folder { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public override string ToString() => DisplayName;
}

public class WowInstallService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(5) };

    /// <summary>客户端版本目录 → 显示名（国服 2026 现状）</summary>
    public static readonly WowFlavor[] KnownFlavors =
    [
        new() { Folder = "_retail_",          DisplayName = "正式服 (Retail)" },
        new() { Folder = "_retail_ptr_",      DisplayName = "正式服 PTR" },
        new() { Folder = "_beta_",            DisplayName = "Beta 测试服" },
        new() { Folder = "_classic_",         DisplayName = "熊猫人之谜怀旧服 (Mists Classic)" },
        new() { Folder = "_classic_ptr_",     DisplayName = "熊猫人之谜怀旧服 PTR" },
        new() { Folder = "_classic_titan_",   DisplayName = "泰坦重铸时光服 (WLK 3.8x 国服)" },
        new() { Folder = "_anniversary_",     DisplayName = "周年纪念服 (TBC Anniversary)" },
        new() { Folder = "_classic_era_",     DisplayName = "永久60 (Classic Era)" },
        new() { Folder = "_classic_era_ptr_", DisplayName = "永久60 PTR" },
    ];

    /// <summary>自动检测魔兽世界安装目录：注册表 → 常见路径。</summary>
    public static string? DetectWowRoot()
    {
        var candidates = new List<string>();

        try
        {
            var reg = Registry.GetValue(
                @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Blizzard Entertainment\World of Warcraft",
                "InstallPath", null) as string;
            if (!string.IsNullOrWhiteSpace(reg))
                candidates.Add(reg.TrimEnd('\\', '/'));
        }
        catch { /* 无权限或不存在 */ }

        candidates.AddRange(
        [
            @"C:\Program Files (x86)\World of Warcraft",
            @"C:\Program Files\World of Warcraft",
            @"D:\World of Warcraft",
            @"D:\Games\World of Warcraft",
            @"E:\World of Warcraft",
        ]);

        return candidates.FirstOrDefault(c => Directory.Exists(c) && HasAnyFlavor(c));
    }

    private static bool HasAnyFlavor(string root) =>
        KnownFlavors.Any(f => Directory.Exists(Path.Combine(root, f.Folder)));

    /// <summary>列出某安装目录下所有可用的客户端版本。</summary>
    public static List<WowFlavor> GetFlavors(string wowRoot)
    {
        var result = KnownFlavors.Where(f => Directory.Exists(Path.Combine(wowRoot, f.Folder))).ToList();
        if (result.Count == 0)
        {
            // 未知名录兜底：_ 开头且包含 Interface 的目录
            foreach (var dir in Directory.GetDirectories(wowRoot, "_*"))
            {
                if (Directory.Exists(Path.Combine(dir, "Interface")))
                    result.Add(new WowFlavor { Folder = Path.GetFileName(dir), DisplayName = Path.GetFileName(dir) });
            }
        }
        return result;
    }

    public static string GetAddOnsDir(string wowRoot, string flavor) =>
        Path.Combine(wowRoot, flavor, "Interface", "AddOns");

    /// <summary>扫描已安装插件：读每个目录的 .toc（Title / Version / X-Curse-Project-ID）。</summary>
    public static List<InstalledAddon> ListInstalled(string addonsDir)
    {
        var result = new List<InstalledAddon>();
        if (!Directory.Exists(addonsDir)) return result;

        foreach (var dir in Directory.GetDirectories(addonsDir))
        {
            var toc = Directory.GetFiles(dir, "*.toc", SearchOption.TopDirectoryOnly).FirstOrDefault();
            if (toc == null) continue;

            var addon = new InstalledAddon
            {
                FolderName = Path.GetFileName(dir),
                Title = Path.GetFileName(dir),
            };

            try
            {
                foreach (var line in File.ReadLines(toc))
                {
                    if (!line.StartsWith("## ")) continue;
                    var body = line[3..].Trim();
                    int colon = body.IndexOf(':');
                    if (colon <= 0) continue;
                    var key = body[..colon].Trim();
                    var val = body[(colon + 1)..].Trim();
                    switch (key.ToLowerInvariant())
                    {
                        case "title":
                            if (!string.IsNullOrEmpty(val)) addon.Title = val;
                            break;
                        case "version":
                            addon.Version = val;
                            break;
                        case "x-curse-project-id":
                            int.TryParse(val, out var pid);
                            addon.CurseProjectId = pid;
                            break;
                    }
                }
            }
            catch { /* toc 解析失败按默认处理 */ }

            result.Add(addon);
        }

        return result.OrderBy(a => a.Title, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>下载 zip 并解压到 AddOns 目录，返回解压出的顶层目录（插件）名。</summary>
    public static async Task<string[]> DownloadAndInstallAsync(string downloadUrl, string addonsDir)
    {
        var tmpZip = Path.Combine(Path.GetTempPath(), "wam_" + Guid.NewGuid().ToString("N") + ".zip");
        try
        {
            using (var resp = await Http.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                resp.EnsureSuccessStatusCode();
                await using var src = await resp.Content.ReadAsStreamAsync();
                await using var dst = File.Create(tmpZip);
                await src.CopyToAsync(dst);
            }

            Directory.CreateDirectory(addonsDir);
            using (var archive = ZipFile.OpenRead(tmpZip))
            {
                var roots = archive.Entries
                    .Where(e => !string.IsNullOrEmpty(e.FullName))
                    .Select(e => e.FullName.Split('/')[0])
                    .Where(s => s.Length > 0)
                    .Distinct()
                    .ToArray();

                archive.ExtractToDirectory(addonsDir, overwriteFiles: true);
                return roots;
            }
        }
        finally
        {
            try { File.Delete(tmpZip); } catch { }
        }
    }

    public static void DeleteAddon(string addonsDir, string folderName)
    {
        var path = Path.Combine(addonsDir, folderName);
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);
    }
}
