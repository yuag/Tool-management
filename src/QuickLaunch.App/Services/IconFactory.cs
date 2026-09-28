using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using QuickLaunch.Core.Models;

namespace QuickLaunch.App.Services;

/// <summary>
/// Builds tile icons: prefers Windows shell icons for real files,
/// otherwise draws an extension badge (TXT / PDF / EXE …).
/// </summary>
public static class IconFactory
{
    private static readonly Dictionary<string, Bitmap> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object Gate = new();

    public static Bitmap Get(LaunchItem item, int size = 28)
    {
        var key = $"{item.Type}|{item.Path}|{item.Name}|{size}";
        lock (Gate)
        {
            if (Cache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            Bitmap created;
            if (OperatingSystem.IsWindows()
                && !string.IsNullOrWhiteSpace(item.Path)
                && TryExtractShellIcon(item.Path, size, out var shell)
                && shell is not null)
            {
                created = shell;
            }
            else
            {
                created = CreateBadgeTile(item, size);
            }

            Cache[key] = created;
            return created;
        }
    }

    public static string GetTypeLabel(LaunchItem item)
    {
        return item.Type switch
        {
            LaunchItemType.Url => "网址",
            LaunchItemType.Folder => "文件夹",
            LaunchItemType.Script => ExtOr(item.Path, "脚本").ToUpperInvariant(),
            LaunchItemType.File => ExtOr(item.Path, "文件").ToUpperInvariant(),
            LaunchItemType.App =>
                string.IsNullOrWhiteSpace(Path.GetExtension(item.Path))
                    ? "程序"
                    : ExtOr(item.Path, "程序").ToUpperInvariant(),
            _ => item.Type.ToString()
        };
    }

    private static string ExtOr(string path, string fallback)
    {
        var ext = Path.GetExtension(path).TrimStart('.');
        return string.IsNullOrWhiteSpace(ext) ? fallback : ext;
    }

    private static Bitmap CreateBadgeTile(LaunchItem item, int size)
    {
        var (label, color) = ResolveBadge(item);
        var bitmap = new RenderTargetBitmap(new PixelSize(size, size), new Vector(96, 96));
        using var ctx = bitmap.CreateDrawingContext();
        var radius = size * 0.18;
        ctx.DrawRectangle(
            new SolidColorBrush(color),
            null,
            new RoundedRect(new Rect(0, 0, size, size), radius));

        var fontSize = label.Length switch
        {
            <= 1 => size * 0.48,
            2 => size * 0.36,
            3 => size * 0.28,
            _ => size * 0.22
        };

        var display = label.Length > 4 ? label[..4] : label;
        var text = new FormattedText(
            display,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Bold),
            fontSize,
            Brushes.White);

        if (text.Width > size - 4)
        {
            text = new FormattedText(
                display.Length > 3 ? display[..3] : display,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Bold),
                size * 0.22,
                Brushes.White);
        }

        ctx.DrawText(text, new Point((size - text.Width) / 2, (size - text.Height) / 2));
        return bitmap;
    }

    private static (string Label, Color Color) ResolveBadge(LaunchItem item)
    {
        if (item.Type == LaunchItemType.Url)
            return ("WEB", Color.FromRgb(0x42, 0xA5, 0xF5));
        if (item.Type == LaunchItemType.Folder)
            return ("DIR", Color.FromRgb(0xFF, 0xB3, 0x00));

        var ext = Path.GetExtension(item.Path).TrimStart('.').ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(ext))
        {
            return item.Type switch
            {
                LaunchItemType.Script => ("SH", Color.FromRgb(0x66, 0xBB, 0x6A)),
                LaunchItemType.File => ("FILE", Color.FromRgb(0x90, 0xA4, 0xAE)),
                _ => ("APP", Color.FromRgb(0x5C, 0x6B, 0xC0))
            };
        }

        if (ext.Length > 4) ext = ext[..4];

        return ext switch
        {
            "TXT" or "LOG" or "MD" or "CSV" => (ext, Color.FromRgb(0x54, 0x6E, 0x7A)),
            "PDF" => ("PDF", Color.FromRgb(0xE5, 0x39, 0x35)),
            "DOC" or "DOCX" or "RTF" => (ext.StartsWith("DOC") ? "DOC" : ext, Color.FromRgb(0x1E, 0x88, 0xE5)),
            "XLS" or "XLSX" => ("XLS", Color.FromRgb(0x43, 0xA0, 0x47)),
            "PPT" or "PPTX" => ("PPT", Color.FromRgb(0xFB, 0x8C, 0x00)),
            "PNG" or "JPG" or "JPEG" or "GIF" or "BMP" or "WEBP" or "ICO" =>
                (ext is "JPEG" ? "JPG" : ext.Length > 3 ? ext[..3] : ext, Color.FromRgb(0x8E, 0x24, 0xAA)),
            "MP3" or "WAV" or "FLAC" or "AAC" => (ext, Color.FromRgb(0x00, 0x89, 0x7B)),
            "MP4" or "MKV" or "AVI" or "MOV" => (ext, Color.FromRgb(0xC6, 0x28, 0x28)),
            "ZIP" or "RAR" or "7Z" or "GZ" => (ext, Color.FromRgb(0x6D, 0x4C, 0x41)),
            "JSON" or "XML" or "YAML" or "YML" => (ext.Length > 3 ? ext[..4] : ext, Color.FromRgb(0xF9, 0xA8, 0x25)),
            "PS1" or "BAT" or "CMD" or "SH" => (ext, Color.FromRgb(0x66, 0xBB, 0x6A)),
            "EXE" or "MSI" or "COM" => ("EXE", Color.FromRgb(0x5C, 0x6B, 0xC0)),
            "LNK" => ("LNK", Color.FromRgb(0x78, 0x90, 0x9C)),
            "URL" => ("URL", Color.FromRgb(0x42, 0xA5, 0xF5)),
            _ => (ext, Color.FromRgb(0x78, 0x90, 0x9C))
        };
    }

    private static bool TryExtractShellIcon(string path, int size, out Bitmap? bitmap)
    {
        bitmap = null;
        try
        {
            var exists = File.Exists(path) || Directory.Exists(path);
            var query = path;
            uint fileAttrs = 0;
            const uint shgfiIcon = 0x000000100;
            const uint shgfiSmallIcon = 0x000000001;
            const uint shgfiLargeIcon = 0x000000000;
            const uint shgfiUseFileAttributes = 0x000000010;

            var flags = shgfiIcon | (size <= 24 ? shgfiSmallIcon : shgfiLargeIcon);
            if (!exists)
            {
                var ext = Path.GetExtension(path);
                if (string.IsNullOrWhiteSpace(ext)) return false;
                query = "file" + ext;
                flags |= shgfiUseFileAttributes;
                fileAttrs = 0x80; // FILE_ATTRIBUTE_NORMAL
            }

            var info = new ShFileInfo();
            var hr = SHGetFileInfo(query, fileAttrs, ref info, (uint)Marshal.SizeOf<ShFileInfo>(), flags);
            if (hr == IntPtr.Zero || info.hIcon == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                bitmap = BitmapFromHIcon(info.hIcon, size);
                return bitmap is not null;
            }
            finally
            {
                DestroyIcon(info.hIcon);
            }
        }
        catch
        {
            bitmap = null;
            return false;
        }
    }

    private static Bitmap? BitmapFromHIcon(IntPtr hIcon, int size)
    {
        if (!GetIconInfo(hIcon, out var iconInfo))
        {
            return null;
        }

        try
        {
            var bmi = new BitmapInfo
            {
                bmiHeader = new BitmapInfoHeader
                {
                    biSize = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
                    biWidth = size,
                    biHeight = -size,
                    biPlanes = 1,
                    biBitCount = 32,
                    biCompression = 0
                }
            };

            var hdcScreen = GetDC(IntPtr.Zero);
            var hdc = CreateCompatibleDC(hdcScreen);
            var hBitmap = CreateDIBSection(hdc, ref bmi, 0, out var bits, IntPtr.Zero, 0);
            if (hBitmap == IntPtr.Zero || bits == IntPtr.Zero)
            {
                DeleteDC(hdc);
                ReleaseDC(IntPtr.Zero, hdcScreen);
                return null;
            }

            var old = SelectObject(hdc, hBitmap);
            var byteCount = size * size * 4;
            var zero = new byte[byteCount];
            Marshal.Copy(zero, 0, bits, byteCount);
            DrawIconEx(hdc, 0, 0, hIcon, size, size, 0, IntPtr.Zero, 0x0003);
            SelectObject(hdc, old);

            var wb = new WriteableBitmap(new PixelSize(size, size), new Vector(96, 96),
                PixelFormats.Bgra8888, AlphaFormat.Unpremul);
            using (var fb = wb.Lock())
            {
                var buffer = new byte[byteCount];
                Marshal.Copy(bits, buffer, 0, byteCount);
                Marshal.Copy(buffer, 0, fb.Address, byteCount);
            }

            DeleteObject(hBitmap);
            DeleteDC(hdc);
            ReleaseDC(IntPtr.Zero, hdcScreen);
            return wb;
        }
        finally
        {
            if (iconInfo.hbmColor != IntPtr.Zero) DeleteObject(iconInfo.hbmColor);
            if (iconInfo.hbmMask != IntPtr.Zero) DeleteObject(iconInfo.hbmMask);
        }
    }

    #region Win32

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShFileInfo
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IconInfo
    {
        public bool fIcon;
        public int xHotspot;
        public int yHotspot;
        public IntPtr hbmMask;
        public IntPtr hbmColor;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        public BitmapInfoHeader bmiHeader;
        public uint bmiColors;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes,
        ref ShFileInfo psfi, uint cbFileInfo, uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    private static extern bool GetIconInfo(IntPtr hIcon, out IconInfo piconinfo);

    [DllImport("user32.dll")]
    private static extern bool DrawIconEx(IntPtr hdc, int xLeft, int yTop, IntPtr hIcon,
        int cxWidth, int cyWidth, uint istepIfAniCur, IntPtr hbrFlickerFreeDraw, uint diFlags);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateDIBSection(IntPtr hdc, ref BitmapInfo pbmi, uint usage,
        out IntPtr ppvBits, IntPtr hSection, uint offset);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr ho);

    #endregion
}
