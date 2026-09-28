using QuickLaunch.Core.Models;

namespace QuickLaunch.Core.Services;

/// <summary>Background scheduler with power-save aware ticking.</summary>
public sealed class TaskSchedulerService : IDisposable
{
    private readonly LauncherService _launcher;
    private readonly Func<AppConfig> _getConfig;
    private readonly Action<AppConfig> _saveConfig;
    private readonly Action<string>? _log;
    private Timer? _timer;
    private bool _windowVisible = true;
    private int _running;

    public TaskSchedulerService(
        LauncherService launcher,
        Func<AppConfig> getConfig,
        Action<AppConfig> saveConfig,
        Action<string>? log = null)
    {
        _launcher = launcher;
        _getConfig = getConfig;
        _saveConfig = saveConfig;
        _log = log;
    }

    public void Start()
    {
        _timer = new Timer(_ => Tick(), null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15));
    }

    public void SetWindowVisible(bool visible)
    {
        _windowVisible = visible;
        var config = _getConfig();
        var seconds = visible || !config.PowerSaveWhenHidden ? 15 : 60;
        _timer?.Change(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(seconds));
    }

    private void Tick()
    {
        if (Interlocked.Exchange(ref _running, 1) == 1)
        {
            return;
        }

        try
        {
            var config = _getConfig();
            var now = DateTime.UtcNow;
            var changed = false;

            foreach (var task in config.Tasks.Where(t => t.Enabled).ToList())
            {
                EnsureNextRun(task, now);
                if (task.NextRunUtc is null || task.NextRunUtc > now)
                {
                    continue;
                }

                try
                {
                    RunTask(config, task);
                    task.LastRunUtc = now;
                    task.LastResult = "OK " + DateTime.Now.ToString("HH:mm:ss");
                    ScheduleNext(task, now);
                    changed = true;
                    _log?.Invoke($"任务完成: {task.Name}");
                }
                catch (Exception ex)
                {
                    task.LastRunUtc = now;
                    task.LastResult = "FAIL: " + ex.Message;
                    ScheduleNext(task, now);
                    changed = true;
                    _log?.Invoke($"任务失败: {task.Name} - {ex.Message}");
                }
            }

            if (changed)
            {
                _saveConfig(config);
            }
        }
        finally
        {
            Interlocked.Exchange(ref _running, 0);
        }
    }

    private void RunTask(AppConfig config, ScheduledTask task)
    {
        var item = config.Groups.SelectMany(g => g.Items)
            .FirstOrDefault(i => i.Id == task.LaunchItemId);
        if (item is null)
        {
            throw new InvalidOperationException("未找到关联启动项");
        }

        _launcher.Launch(item);

        foreach (var nextId in task.NextTaskIds)
        {
            var next = config.Tasks.FirstOrDefault(t => t.Id == nextId);
            if (next is null) continue;
            var nextItem = config.Groups.SelectMany(g => g.Items)
                .FirstOrDefault(i => i.Id == next.LaunchItemId);
            if (nextItem is not null)
            {
                _launcher.Launch(nextItem);
            }
        }
    }

    private static void EnsureNextRun(ScheduledTask task, DateTime nowUtc)
    {
        if (task.NextRunUtc is null)
        {
            ScheduleNext(task, nowUtc, forceFromNow: true);
        }
    }

    private static void ScheduleNext(ScheduledTask task, DateTime nowUtc, bool forceFromNow = false)
    {
        var localNow = nowUtc.ToLocalTime();
        switch (task.Kind)
        {
            case ScheduleKind.Interval:
                var minutes = Math.Max(1, task.IntervalMinutes);
                task.NextRunUtc = nowUtc.AddMinutes(minutes);
                break;
            case ScheduleKind.Daily:
            case ScheduleKind.Once:
                if (!TimeSpan.TryParse(task.TimeOfDay, out var tod))
                {
                    tod = new TimeSpan(9, 0, 0);
                }

                var nextLocal = localNow.Date.Add(tod);
                if (!forceFromNow && task.Kind == ScheduleKind.Once && task.LastRunUtc is not null)
                {
                    task.Enabled = false;
                    task.NextRunUtc = null;
                    break;
                }

                if (nextLocal <= localNow)
                {
                    nextLocal = nextLocal.AddDays(1);
                }

                task.NextRunUtc = nextLocal.ToUniversalTime();
                break;
        }
    }

    public void Dispose() => _timer?.Dispose();
}
