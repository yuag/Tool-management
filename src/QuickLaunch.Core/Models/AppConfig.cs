namespace QuickLaunch.Core.Models;

public enum LaunchItemType
{
    App,
    Folder,
    Url,
    Script,
    File
}

public enum AppTheme
{
    Light,
    Dark
}

public enum ScheduleKind
{
    Interval,
    Daily,
    Once
}

public sealed class LaunchItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public LaunchItemType Type { get; set; } = LaunchItemType.App;
    public string Path { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public string Hotkey { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public sealed class LaunchGroup
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "分类-1";
    /// <summary>Empty = root. Enables virtual nested categories.</summary>
    public string ParentId { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public List<LaunchItem> Items { get; set; } = [];
}

public sealed class ThemeColors
{
    public string SidebarBg { get; set; } = "#FFF0F0F0";
    public string SidebarSelected { get; set; } = "#FFE4E4E4";
    public string ContentBg { get; set; } = "#FFFFFFFF";
    public string TitleBg { get; set; } = "#FFF7F7F7";
    public string PanelBg { get; set; } = "#FFFBFBFB";
    public string BorderLine { get; set; } = "#FFD8D8D8";
    public string TextPrimary { get; set; } = "#FF222222";
    public string TextMuted { get; set; } = "#FF888888";
    public string Accent { get; set; } = "#FF4FC3F7";
    public string MenuBg { get; set; } = "#FFFFFFFF";

    public static ThemeColors ForTheme(AppTheme theme) =>
        theme == AppTheme.Dark ? Dark() : Light();

    public static ThemeColors Light() => new()
    {
        SidebarBg = "#FFF0F0F0",
        SidebarSelected = "#FFE4E4E4",
        ContentBg = "#FFFFFFFF",
        TitleBg = "#FFF7F7F7",
        PanelBg = "#FFFBFBFB",
        BorderLine = "#FFD8D8D8",
        TextPrimary = "#FF222222",
        TextMuted = "#FF888888",
        Accent = "#FF4FC3F7",
        MenuBg = "#FFFFFFFF"
    };

    public static ThemeColors Dark() => new()
    {
        SidebarBg = "#FF2B2B2B",
        SidebarSelected = "#FF3A3A3A",
        ContentBg = "#FF1E1E1E",
        TitleBg = "#FF252525",
        PanelBg = "#FF2A2A2A",
        BorderLine = "#FF444444",
        TextPrimary = "#FFE8E8E8",
        TextMuted = "#FFAAAAAA",
        Accent = "#FF4FC3F7",
        MenuBg = "#FF2F2F2F"
    };

    public void CopyFrom(ThemeColors other)
    {
        SidebarBg = other.SidebarBg;
        SidebarSelected = other.SidebarSelected;
        ContentBg = other.ContentBg;
        TitleBg = other.TitleBg;
        PanelBg = other.PanelBg;
        BorderLine = other.BorderLine;
        TextPrimary = other.TextPrimary;
        TextMuted = other.TextMuted;
        Accent = other.Accent;
        MenuBg = other.MenuBg;
    }
}

public sealed class ScheduledTask
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public bool Silent { get; set; } = true;
    public ScheduleKind Kind { get; set; } = ScheduleKind.Interval;
    /// <summary>For Interval: minutes. For Daily: ignored.</summary>
    public int IntervalMinutes { get; set; } = 60;
    /// <summary>For Daily/Once: HH:mm</summary>
    public string TimeOfDay { get; set; } = "09:00";
    /// <summary>Launch item id to run.</summary>
    public string LaunchItemId { get; set; } = string.Empty;
    /// <summary>Optional next task ids (chain).</summary>
    public List<string> NextTaskIds { get; set; } = [];
    public DateTime? LastRunUtc { get; set; }
    public DateTime? NextRunUtc { get; set; }
    public string LastResult { get; set; } = string.Empty;
}

public sealed class AppConfig
{
    public string Hotkey { get; set; } = "Alt+Space";
    public AppTheme Theme { get; set; } = AppTheme.Light;
    public ThemeColors Colors { get; set; } = new();
    public double Scale { get; set; } = 1.0;
    public double TileWidth { get; set; } = 150;
    public double TileHeight { get; set; } = 44;
    public double SidebarWidth { get; set; } = 120;
    public bool StartWithWindows { get; set; }
    public bool HideAfterLaunch { get; set; } = true;
    /// <summary>When window hidden, slow down scheduler tick (power mode).</summary>
    public bool PowerSaveWhenHidden { get; set; } = true;
    /// <summary>Hide window when it loses focus.</summary>
    public bool AutoHideOnDeactivate { get; set; }
    /// <summary>Collapse other category trees when selecting a different root.</summary>
    public bool AutoCollapseGroups { get; set; } = true;
    /// <summary>Expanded parent category ids (virtual directory).</summary>
    public List<string> ExpandedGroupIds { get; set; } = [];
    public List<LaunchGroup> Groups { get; set; } = [];
    public List<ScheduledTask> Tasks { get; set; } = [];
}
