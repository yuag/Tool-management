using QuickLaunch.Core.Platform;

namespace QuickLaunch.App.Platform;

public sealed class NullGlobalHotkeyService : IGlobalHotkeyService
{
    public event Action? ToggleWindowHotkeyPressed
    {
        add { }
        remove { }
    }

    public event Action<string>? ItemHotkeyPressed
    {
        add { }
        remove { }
    }

    public bool TryRegisterToggle(string hotkey) => false;
    public void RegisterItemHotkeys(IEnumerable<(string Id, string Hotkey)> items) { }
    public void ClearItemHotkeys() { }
    public void Dispose() { }
}
