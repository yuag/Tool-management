namespace QuickLaunch.Core.Platform;

public interface IGlobalHotkeyService : IDisposable
{
    event Action? ToggleWindowHotkeyPressed;
    event Action<string>? ItemHotkeyPressed;

    bool TryRegisterToggle(string hotkey);
    void RegisterItemHotkeys(IEnumerable<(string Id, string Hotkey)> items);
    void ClearItemHotkeys();
}
