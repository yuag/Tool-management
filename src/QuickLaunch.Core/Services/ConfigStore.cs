using System.Text.Json;
using System.Text.Json.Serialization;
using QuickLaunch.Core.Models;

namespace QuickLaunch.Core.Services;

public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public string ConfigPath { get; }

    public ConfigStore(string? configPath = null)
    {
        ConfigPath = configPath ?? DefaultConfigPath();
    }

    public AppConfig Load()
    {
        EnsureDirectory();
        if (!File.Exists(ConfigPath))
        {
            var created = CreateDefault();
            Save(created);
            return created;
        }

        var json = File.ReadAllText(ConfigPath);
        var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? CreateDefault();
        Normalize(config);
        return config;
    }

    public void Save(AppConfig config)
    {
        Normalize(config);
        EnsureDirectory();
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(config, JsonOptions));
    }

    public static void Normalize(AppConfig config)
    {
        if (config.Scale is < 0.8 or > 1.6)
        {
            config.Scale = 1.0;
        }

        if (config.TileWidth < 100)
        {
            config.TileWidth = 150;
        }

        if (config.TileHeight < 36)
        {
            config.TileHeight = 44;
        }

        if (config.SidebarWidth < 140)
        {
            config.SidebarWidth = 180;
        }

        if (config.Groups.Count == 0)
        {
            config.Groups.Add(new LaunchGroup { Name = "分类-1" });
        }

        config.Tasks ??= [];
        config.Colors ??= new ThemeColors();
        config.ExpandedGroupIds ??= [];

        foreach (var g in config.Groups)
        {
            g.ParentId ??= string.Empty;
            var order = 0;
            foreach (var item in g.Items.OrderBy(i => i.SortOrder).ThenBy(i => i.Name))
            {
                item.SortOrder = order++;
            }

            g.Items = g.Items.OrderBy(i => i.SortOrder).ThenBy(i => i.Name).ToList();
        }

        config.Groups = config.Groups.OrderBy(g => g.SortOrder).ThenBy(g => g.Name).ToList();
    }

    private void EnsureDirectory()
    {
        var dir = Path.GetDirectoryName(ConfigPath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    private static AppConfig CreateDefault() => new()
    {
        Hotkey = "Alt+Space",
        Theme = AppTheme.Light,
        Scale = 1.0,
        TileWidth = 150,
        TileHeight = 44,
        SidebarWidth = 180,
        Groups =
        [
            new LaunchGroup { Name = "分类-1", SortOrder = 0 },
            new LaunchGroup { Name = "分类-2", SortOrder = 1 },
            new LaunchGroup { Name = "分类-3", SortOrder = 2 }
        ]
    };

    public static string DefaultConfigPath()
    {
        string root;
        if (OperatingSystem.IsWindows())
        {
            root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        }
        else if (OperatingSystem.IsMacOS())
        {
            root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library", "Application Support");
        }
        else
        {
            var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            root = string.IsNullOrWhiteSpace(xdg)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config")
                : xdg;
        }

        return Path.Combine(root, "QuickLaunch", "config.json");
    }
}
