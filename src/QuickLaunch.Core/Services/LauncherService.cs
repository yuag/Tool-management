using System.Diagnostics;
using QuickLaunch.Core.Models;

namespace QuickLaunch.Core.Services;

public sealed class LauncherService
{
    private static readonly HashSet<string> ExecutableExts = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".com", ".msi", ".msp", ".msc", ".cpl", ".scr", ".application"
    };

    public void Launch(LaunchItem item, string? searchQuery = null)
    {
        if (item is null || string.IsNullOrWhiteSpace(item.Path))
        {
            return;
        }

        var path = PathVariables.Expand(item.Path.Trim(), searchQuery: searchQuery);
        var args = PathVariables.Expand(item.Arguments ?? string.Empty, searchQuery: searchQuery);
        var workDir = string.IsNullOrWhiteSpace(item.WorkingDirectory)
            ? string.Empty
            : PathVariables.Expand(item.WorkingDirectory, searchQuery: searchQuery);

        switch (item.Type)
        {
            case LaunchItemType.Url:
                OpenUrl(path);
                break;
            case LaunchItemType.Folder:
                OpenFolder(path);
                break;
            case LaunchItemType.Script:
                OpenScript(path, args, workDir);
                break;
            case LaunchItemType.File:
                OpenDocument(path);
                break;
            default:
                // Auto-detect document files wrongly stored as App
                if (LooksLikeDocument(path))
                    OpenDocument(path);
                else
                    OpenApp(path, args, workDir);
                break;
        }
    }

    private static bool LooksLikeDocument(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return false;

        var ext = Path.GetExtension(path);
        if (string.IsNullOrEmpty(ext)) return false;
        if (ExecutableExts.Contains(ext)) return false;
        if (ext is ".ps1" or ".bat" or ".cmd" or ".sh") return false;
        return File.Exists(path);
    }

    private static void OpenUrl(string url)
    {
        if (!url.Contains("://", StringComparison.Ordinal))
        {
            url = "https://" + url;
        }

        StartShell(url);
    }

    private static void OpenFolder(string path)
    {
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException($"Folder not found: {path}");
        }

        StartShell(path);
    }

    private static void OpenDocument(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("文件不存在", path);
        }

        // Open with the system default association (Notepad for .txt, etc.)
        StartShell(path);
    }

    private static void OpenApp(string path, string arguments, string workingDirectory)
    {
        var psi = new ProcessStartInfo
        {
            FileName = path,
            Arguments = arguments,
            UseShellExecute = true
        };
        if (!string.IsNullOrWhiteSpace(workingDirectory) && Directory.Exists(workingDirectory))
        {
            psi.WorkingDirectory = workingDirectory;
        }

        var process = Process.Start(psi);
        // Some shell associations return null — not an error.
        _ = process;
    }

    private static void StartShell(string target)
    {
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = target,
            UseShellExecute = true,
            ErrorDialog = true
        });
        _ = process;
    }

    private static void OpenScript(string path, string arguments, string workingDirectory)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Script not found", path);
        }

        var ext = Path.GetExtension(path).ToLowerInvariant();
        ProcessStartInfo psi;
        if (ext is ".ps1")
        {
            psi = new ProcessStartInfo
            {
                FileName = OperatingSystem.IsWindows() ? "powershell" : "pwsh",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{path}\" {arguments}",
                UseShellExecute = false,
                CreateNoWindow = true
            };
        }
        else if (ext is ".bat" or ".cmd")
        {
            psi = new ProcessStartInfo
            {
                FileName = path,
                Arguments = arguments,
                UseShellExecute = true,
                CreateNoWindow = true
            };
        }
        else if (ext is ".sh")
        {
            psi = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"\"{path}\" {arguments}",
                UseShellExecute = false,
                CreateNoWindow = true
            };
        }
        else
        {
            psi = new ProcessStartInfo
            {
                FileName = path,
                Arguments = arguments,
                UseShellExecute = true
            };
        }

        if (!string.IsNullOrWhiteSpace(workingDirectory) && Directory.Exists(workingDirectory))
        {
            psi.WorkingDirectory = workingDirectory;
        }

        Process.Start(psi);
    }
}
