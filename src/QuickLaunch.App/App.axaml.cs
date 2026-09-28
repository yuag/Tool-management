using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using QuickLaunch.App.Platform;
using QuickLaunch.App.ViewModels;
using QuickLaunch.App.Views;
using QuickLaunch.Core.Models;
using QuickLaunch.Core.Platform;
using QuickLaunch.Core.Services;

namespace QuickLaunch.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        Styles.Add(new FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var store = new ConfigStore();
            var config = store.Load();
            // Keep UI simple: always show all categories (no collapse trees).
            config.AutoCollapseGroups = false;
            // Fix mismatched theme/colors (e.g. dark theme + light palette).
            config.Colors ??= ThemeColors.Light();
            config.Colors.CopyFrom(ThemeColors.ForTheme(config.Theme));
            store.Save(config);
            foreach (var g in config.Groups)
            {
                if (config.Groups.Any(c => c.ParentId == g.Id)
                    && !config.ExpandedGroupIds.Contains(g.Id))
                {
                    config.ExpandedGroupIds.Add(g.Id);
                }
            }

            var launcher = new LauncherService();
            IGlobalHotkeyService hotkey = OperatingSystem.IsWindows()
                ? new WindowsGlobalHotkeyService()
                : new NullGlobalHotkeyService();

            MainWindow? window = null;
            MainViewModel? vm = null;

            vm = new MainViewModel(
                store,
                config,
                launcher,
                hotkey,
                hideWindow: () => Avalonia.Threading.Dispatcher.UIThread.Post(
                    () => window?.HideWindow(),
                    Avalonia.Threading.DispatcherPriority.Background),
                applyChrome: () =>
                {
                    window?.ApplyChrome();
                    ApplyThemeColors(vm?.Config.Colors);
                });

            window = new MainWindow { DataContext = vm };
            desktop.MainWindow = window;
            desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnExplicitShutdown;

            ApplyThemeColors(config.Colors);

            hotkey.TryRegisterToggle(config.Hotkey);
            hotkey.ToggleWindowHotkeyPressed += () =>
                Avalonia.Threading.Dispatcher.UIThread.Post(() => window.ToggleVisible());
            hotkey.ItemHotkeyPressed += id =>
                Avalonia.Threading.Dispatcher.UIThread.Post(() => vm.LaunchById(id));

            Program.ShowRequested += () =>
                Avalonia.Threading.Dispatcher.UIThread.Post(() => window.ShowFromTray());

            desktop.Exit += (_, _) => hotkey.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }

    public static void ApplyThemeColors(ThemeColors? colors)
    {
        if (colors is null || Current?.Resources is null) return;
        SetBrush("SidebarBg", colors.SidebarBg);
        SetBrush("SidebarSelected", colors.SidebarSelected);
        SetBrush("ContentBg", colors.ContentBg);
        SetBrush("TitleBg", colors.TitleBg);
        SetBrush("PanelBg", string.IsNullOrWhiteSpace(colors.PanelBg) ? colors.TitleBg : colors.PanelBg);
        SetBrush("BorderLine", colors.BorderLine);
        SetBrush("TextPrimary", colors.TextPrimary);
        SetBrush("TextMuted", string.IsNullOrWhiteSpace(colors.TextMuted) ? "#FF888888" : colors.TextMuted);
        SetBrush("Accent", string.IsNullOrWhiteSpace(colors.Accent) ? "#FF4FC3F7" : colors.Accent);
        SetBrush("MenuBg", string.IsNullOrWhiteSpace(colors.MenuBg) ? colors.ContentBg : colors.MenuBg);
    }

    private static void SetBrush(string key, string hex)
    {
        if (Current is null) return;
        try
        {
            // Accept #RGB #RRGGBB #AARRGGBB
            var color = Color.Parse(hex.Trim());
            Current.Resources[key] = new SolidColorBrush(color);
        }
        catch
        {
            // Keep previous brush if parse fails.
        }
    }
}
