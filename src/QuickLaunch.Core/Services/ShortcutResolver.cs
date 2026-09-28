using System.Text;
using QuickLaunch.Core.Models;

namespace QuickLaunch.Core.Services;

/// <summary>Resolves .lnk / .url into a concrete launch item.</summary>
public static class ShortcutResolver
{
    public static LaunchItem FromPath(string path)
    {
        path = path.Trim().Trim('"');
        if (path.EndsWith(".url", StringComparison.OrdinalIgnoreCase))
        {
            return FromUrlFile(path);
        }

        if (path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) && OperatingSystem.IsWindows())
        {
            return FromLnk(path);
        }

        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext is ".ps1" or ".bat" or ".cmd" or ".sh")
        {
            return new LaunchItem
            {
                Name = Path.GetFileNameWithoutExtension(path),
                Type = LaunchItemType.Script,
                Path = path
            };
        }

        if (Directory.Exists(path))
        {
            return new LaunchItem
            {
                Name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
                Type = LaunchItemType.Folder,
                Path = path
            };
        }

        if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return new LaunchItem
            {
                Name = path,
                Type = LaunchItemType.Url,
                Path = path
            };
        }

        // Documents / media / etc. open via shell association
        if (File.Exists(path) && ext is not (".exe" or ".com" or ".msi" or ".msc" or ".cpl" or ".scr"))
        {
            return new LaunchItem
            {
                Name = Path.GetFileNameWithoutExtension(path) is { Length: > 0 } n ? n : Path.GetFileName(path),
                Type = LaunchItemType.File,
                Path = path
            };
        }

        return new LaunchItem
        {
            Name = Path.GetFileNameWithoutExtension(path) is { Length: > 0 } n2 ? n2 : Path.GetFileName(path),
            Type = LaunchItemType.App,
            Path = path
        };
    }

    public static LaunchItem FromUrlFile(string path)
    {
        var url = path;
        var name = Path.GetFileNameWithoutExtension(path);
        try
        {
            foreach (var line in File.ReadLines(path, Encoding.UTF8))
            {
                if (line.StartsWith("URL=", StringComparison.OrdinalIgnoreCase))
                {
                    url = line[4..].Trim();
                }
            }
        }
        catch
        {
            // Keep defaults.
        }

        return new LaunchItem
        {
            Name = name,
            Type = LaunchItemType.Url,
            Path = url
        };
    }

    public static LaunchItem FromLnk(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        try
        {
            var type = Type.GetTypeFromProgID("WScript.Shell");
            if (type is null)
            {
                return new LaunchItem { Name = name, Type = LaunchItemType.App, Path = path };
            }

            dynamic shell = Activator.CreateInstance(type)!;
            dynamic shortcut = shell.CreateShortcut(path);
            string target = shortcut.TargetPath ?? path;
            string args = shortcut.Arguments ?? string.Empty;
            string workDir = shortcut.WorkingDirectory ?? string.Empty;
            string desc = shortcut.Description ?? string.Empty;

            LaunchItemType kind;
            var ext = Path.GetExtension(target).ToLowerInvariant();
            if (Directory.Exists(target))
            {
                kind = LaunchItemType.Folder;
            }
            else if (target.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                     || target.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                kind = LaunchItemType.Url;
            }
            else if (ext is ".ps1" or ".bat" or ".cmd" or ".sh")
            {
                kind = LaunchItemType.Script;
            }
            else
            {
                kind = LaunchItemType.App;
            }

            return new LaunchItem
            {
                Name = string.IsNullOrWhiteSpace(desc) ? name : desc,
                Type = kind,
                Path = string.IsNullOrWhiteSpace(target) ? path : target,
                Arguments = args,
                WorkingDirectory = workDir
            };
        }
        catch
        {
            return new LaunchItem { Name = name, Type = LaunchItemType.App, Path = path };
        }
    }
}
