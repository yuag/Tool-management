using System.Runtime.InteropServices;
using QuickLaunch.Core.Platform;

namespace QuickLaunch.App.Platform;

/// <summary>Windows multi-hotkey service (toggle + per-item).</summary>
public sealed class WindowsGlobalHotkeyService : IGlobalHotkeyService
{
    private const int ToggleId = 0x514C;
    private const int ItemIdBase = 0x6000;
    private const int WmHotkey = 0x0312;

    private IntPtr _hwnd;
    private Thread? _thread;
    private volatile bool _running;
    private WndProcDelegate? _wndProc;
    private readonly object _gate = new();
    private readonly Dictionary<int, string> _itemMap = new();
    private int _nextItemId = ItemIdBase;
    private bool _toggleRegistered;
    private uint _toggleMods;
    private uint _toggleKey;

    public event Action? ToggleWindowHotkeyPressed;
    public event Action<string>? ItemHotkeyPressed;

    public bool TryRegisterToggle(string hotkey)
    {
        if (!OperatingSystem.IsWindows() || !TryParse(hotkey, out _toggleMods, out _toggleKey))
        {
            return false;
        }

        EnsureThread();
        for (var i = 0; i < 50 && _hwnd == IntPtr.Zero; i++)
        {
            Thread.Sleep(20);
        }

        if (_hwnd == IntPtr.Zero)
        {
            return false;
        }

        lock (_gate)
        {
            if (_toggleRegistered)
            {
                UnregisterHotKey(_hwnd, ToggleId);
                _toggleRegistered = false;
            }

            _toggleRegistered = RegisterHotKey(_hwnd, ToggleId, _toggleMods, _toggleKey);
            return _toggleRegistered;
        }
    }

    public void RegisterItemHotkeys(IEnumerable<(string Id, string Hotkey)> items)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        EnsureThread();
        for (var i = 0; i < 50 && _hwnd == IntPtr.Zero; i++)
        {
            Thread.Sleep(20);
        }

        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        lock (_gate)
        {
            ClearItemHotkeysUnlocked();
            foreach (var (id, hotkey) in items)
            {
                if (string.IsNullOrWhiteSpace(hotkey) || !TryParse(hotkey, out var mods, out var key))
                {
                    continue;
                }

                var hid = _nextItemId++;
                if (RegisterHotKey(_hwnd, hid, mods, key))
                {
                    _itemMap[hid] = id;
                }
            }
        }
    }

    public void ClearItemHotkeys()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        lock (_gate)
        {
            ClearItemHotkeysUnlocked();
        }
    }

    public void Dispose()
    {
        _running = false;
        lock (_gate)
        {
            if (_hwnd != IntPtr.Zero)
            {
                if (_toggleRegistered)
                {
                    UnregisterHotKey(_hwnd, ToggleId);
                }

                ClearItemHotkeysUnlocked();
                PostMessage(_hwnd, 0x0012, IntPtr.Zero, IntPtr.Zero);
                _hwnd = IntPtr.Zero;
            }
        }
    }

    private void ClearItemHotkeysUnlocked()
    {
        foreach (var id in _itemMap.Keys.ToList())
        {
            UnregisterHotKey(_hwnd, id);
        }

        _itemMap.Clear();
    }

    private void EnsureThread()
    {
        if (_thread is { IsAlive: true })
        {
            return;
        }

        _running = true;
        _thread = new Thread(() =>
        {
            if (!CreateMessageWindow())
            {
                return;
            }

            while (_running)
            {
                var ret = GetMessage(out var msg, IntPtr.Zero, 0, 0);
                if (ret <= 0)
                {
                    break;
                }

                if (msg.message == WmHotkey)
                {
                    var id = (int)msg.wParam;
                    if (id == ToggleId)
                    {
                        ToggleWindowHotkeyPressed?.Invoke();
                    }
                    else
                    {
                        string? itemId;
                        lock (_gate)
                        {
                            _itemMap.TryGetValue(id, out itemId);
                        }

                        if (!string.IsNullOrEmpty(itemId))
                        {
                            ItemHotkeyPressed?.Invoke(itemId);
                        }
                    }
                }

                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }
        })
        {
            IsBackground = true,
            Name = "QuickLaunch-Hotkey"
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    private bool CreateMessageWindow()
    {
        var className = "QuickLaunchHotkeyWnd2";
        _wndProc = DefWindowProc;
        var wndClass = new WndClassEx
        {
            cbSize = (uint)Marshal.SizeOf<WndClassEx>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            hInstance = GetModuleHandle(null),
            lpszClassName = className
        };

        RegisterClassEx(ref wndClass);
        _hwnd = CreateWindowEx(
            0, className, "QuickLaunchHotkey",
            0, 0, 0, 0, 0,
            new IntPtr(-3),
            IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        return _hwnd != IntPtr.Zero;
    }

    internal static bool TryParse(string hotkey, out uint modifiers, out uint key)
    {
        modifiers = 0;
        key = 0;
        if (string.IsNullOrWhiteSpace(hotkey))
        {
            return false;
        }

        var parts = hotkey.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        foreach (var part in parts.Take(parts.Length - 1))
        {
            switch (part.ToLowerInvariant())
            {
                case "ctrl":
                case "control":
                    modifiers |= 0x0002;
                    break;
                case "alt":
                    modifiers |= 0x0001;
                    break;
                case "shift":
                    modifiers |= 0x0004;
                    break;
                case "win":
                case "windows":
                    modifiers |= 0x0008;
                    break;
                default:
                    return false;
            }
        }

        var keyName = parts[^1];
        if (keyName.Equals("space", StringComparison.OrdinalIgnoreCase))
        {
            key = 0x20;
            return true;
        }

        if (keyName.Length == 1)
        {
            var ch = char.ToUpperInvariant(keyName[0]);
            if (ch is >= 'A' and <= 'Z' or >= '0' and <= '9')
            {
                key = ch;
                return true;
            }
        }

        if (keyName.StartsWith("F", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(keyName[1..], out var fn)
            && fn is >= 1 and <= 24)
        {
            key = (uint)(0x70 + fn - 1);
            return true;
        }

        return false;
    }

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WndClassEx
    {
        public uint cbSize;
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Msg
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int ptX;
        public int ptY;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassEx(ref WndClassEx lpwcx);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(
        int dwExStyle, string lpClassName, string lpWindowName,
        int dwStyle, int x, int y, int nWidth, int nHeight,
        IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll")]
    private static extern int GetMessage(out Msg lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref Msg lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref Msg lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
}
