using System.Text.Json;
using System.Text.Json.Serialization;

namespace PiggyOcr.App.Services;

public enum MouseAction
{
    Edit,
    CopyClose,
    FullScreen,
    Cancel,
}

public sealed class AppConfig
{
    public string Hotkey { get; set; } = "Win+Shift+O";
    public MouseAction Left { get; set; } = MouseAction.Edit;
    public MouseAction Middle { get; set; } = MouseAction.CopyClose;
    public MouseAction Right { get; set; } = MouseAction.Cancel;
    public MouseAction DoubleClick { get; set; } = MouseAction.FullScreen;
    public string Tier { get; set; } = "Auto";
    public bool AutoCopy { get; set; } = true;
    public int HistoryLimit { get; set; } = 20;
    public bool AutoStart { get; set; }
    public bool CheckUpdate { get; set; } = true;
    public bool WriteLog { get; set; } = true;
    public DateTimeOffset LastUpdateCheck { get; set; }
}

public static class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private static string BasePath => Path.Combine(AppContext.BaseDirectory, "PiggyOcr.json");
    private static string FallbackPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PiggyOcr", "PiggyOcr.json");

    public static AppConfig Load()
    {
        foreach (string path in new[] { BasePath, FallbackPath })
        {
            try
            {
                if (!File.Exists(path)) continue;
                var config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(path), JsonOptions);
                if (config is not null) return config;
            }
            catch
            {
                // a broken config must not block startup
            }
        }
        return new AppConfig();
    }

    public static void Save(AppConfig config)
    {
        string json = JsonSerializer.Serialize(config, JsonOptions);
        try
        {
            File.WriteAllText(BasePath, json);
        }
        catch
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FallbackPath)!);
                File.WriteAllText(FallbackPath, json);
            }
            catch
            {
                // read-only locations are tolerated; settings just won't persist
            }
        }
    }
}
