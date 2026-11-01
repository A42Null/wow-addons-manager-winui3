using System.Text.Json;
using System.Text.Json.Serialization;

namespace WoWAddonManager.Models;

public class CfEnvelope<T>
{
    [JsonPropertyName("data")]
    public T[] Data { get; set; } = [];

    [JsonPropertyName("pagination")]
    public CfPagination? Pagination { get; set; }
}

public class CfPagination
{
    public int Index { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
}

public class CfMod
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string Summary { get; set; } = "";
    public long DownloadCount { get; set; }
    public DateTimeOffset DateModified { get; set; }
    public CfLogo? Logo { get; set; }
    public List<CfAuthor> Authors { get; set; } = [];
    public List<CfFile> LatestFiles { get; set; } = [];
    public List<CfCategory> Categories { get; set; } = [];
}

public class CfLogo
{
    public string ThumbnailUrl { get; set; } = "";
}

public class CfAuthor
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public class CfCategory
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public int ClassId { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
}

public class CfFile
{
    public int Id { get; set; }
    public string DisplayName { get; set; } = "";
    public string FileName { get; set; } = "";
    public DateTimeOffset FileDate { get; set; }
    public string? DownloadUrl { get; set; }
    public int ReleaseType { get; set; } // 1=正式 2=Beta 3=Alpha
    public int FileStatus { get; set; }
    public List<string> GameVersions { get; set; } = [];
}

public class InstalledAddon
{
    public string FolderName { get; set; } = "";
    public string Title { get; set; } = "";
    public string Version { get; set; } = "";
    public int CurseProjectId { get; set; }
}
