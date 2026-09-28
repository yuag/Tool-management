using System.Text.RegularExpressions;

namespace QuickLaunch.Core.Services;

/// <summary>
/// Expands %ENV%, %mp% (app dir), %mr% (drive root),
/// %so% (search query), %so-url% (url-encoded search query).
/// </summary>
public static class PathVariables
{
    private static readonly Regex TokenRegex = new(@"%([^%]+)%", RegexOptions.Compiled);

    public static string Expand(string input, string? appDirectory = null, string? searchQuery = null)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        var appDir = appDirectory
            ?? AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var driveRoot = Path.GetPathRoot(appDir) ?? string.Empty;
        driveRoot = driveRoot.TrimEnd('\\', '/');
        var so = searchQuery ?? string.Empty;

        var expanded = TokenRegex.Replace(input, m =>
        {
            var key = m.Groups[1].Value;
            if (key.Equals("mp", StringComparison.OrdinalIgnoreCase)) return appDir;
            if (key.Equals("mr", StringComparison.OrdinalIgnoreCase)) return driveRoot;
            if (key.Equals("so", StringComparison.OrdinalIgnoreCase)) return so;
            if (key.Equals("so-url", StringComparison.OrdinalIgnoreCase)) return Uri.EscapeDataString(so);
            return Environment.GetEnvironmentVariable(key) ?? m.Value;
        });

        if (expanded.StartsWith("~/") || expanded.StartsWith("~\\"))
        {
            expanded = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                expanded[2..]);
        }

        return Environment.ExpandEnvironmentVariables(expanded);
    }
}
