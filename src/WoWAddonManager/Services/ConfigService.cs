using System.Text.Json;
using System.Text.Json.Serialization;

namespace WoWAddonManager.Services;

public class AppConfig
{
    public string WowRoot { get; set; } = "";
    public string Flavor { get; set; } = "_retail_";
    public string CfApiKey { get; set; } = "";
}

public static class ConfigService
{
    private static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WoWAddonManager");

    private static readonly string FilePath = Path.Combine(Dir, "config.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static AppConfig Current { get; private set; } = Load();

    public static event Action? ConfigChanged;

    public static void Save(AppConfig config)
    {
        Current = config;
        Directory.CreateDirectory(Dir);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(config, JsonOptions));
        ConfigChanged?.Invoke();
    }

    public static void Reload() => Current = Load();

    private static AppConfig Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(FilePath), JsonOptions) ?? new AppConfig();
        }
        catch
        {
            // 配置损坏时回退默认
        }
        return new AppConfig();
    }
}
