using Avalonia;
using Avalonia.Threading;
using System;
using System.Threading;

namespace QuickLaunch.App;

internal static class Program
{
    private const string MutexName = @"Local\QuickLaunch.Maye.SingleInstance";
    private const string ShowEventName = @"Local\QuickLaunch.Maye.ShowWindow";

    private static Mutex? _mutex;
    private static EventWaitHandle? _showEvent;
    private static CancellationTokenSource? _showWaitCts;

    public static event Action? ShowRequested;

    [STAThread]
    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            try
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "QuickLaunch");
                Directory.CreateDirectory(dir);
                File.AppendAllText(
                    Path.Combine(dir, "crash.log"),
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {e.ExceptionObject}\n\n");
            }
            catch
            {
                // ignore
            }
        };

        _mutex = new Mutex(true, MutexName, out var createdNew);
        if (!createdNew)
        {
            // Another instance is running — ask it to show, then exit.
            try
            {
                using var ev = EventWaitHandle.OpenExisting(ShowEventName);
                ev.Set();
            }
            catch
            {
                // ignore
            }

            return;
        }

        try
        {
            _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
            _showWaitCts = new CancellationTokenSource();
            var token = _showWaitCts.Token;
            _ = Task.Run(() =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        if (_showEvent.WaitOne(500))
                        {
                            Dispatcher.UIThread.Post(() => ShowRequested?.Invoke());
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }
                    catch
                    {
                        // ignore
                    }
                }
            }, token);

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            _showWaitCts?.Cancel();
            _showEvent?.Dispose();
            _mutex.ReleaseMutex();
            _mutex.Dispose();
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
