using CommunityToolkit.Mvvm.ComponentModel;
using QuickLaunch.Core.Models;

namespace QuickLaunch.App.ViewModels;

public sealed partial class TaskItemVm : ObservableObject
{
    public ScheduledTask Model { get; }

    public string Name => Model.Name;
    public bool Enabled => Model.Enabled;
    public string KindText => Model.Kind switch
    {
        ScheduleKind.Interval => $"每 {Model.IntervalMinutes} 分钟",
        ScheduleKind.Daily => $"每日 {Model.TimeOfDay}",
        ScheduleKind.Once => $"一次 {Model.TimeOfDay}",
        _ => Model.Kind.ToString()
    };
    public string LastResult => string.IsNullOrWhiteSpace(Model.LastResult) ? "—" : Model.LastResult;
    public string NextRun => Model.NextRunUtc is null
        ? "—"
        : Model.NextRunUtc.Value.ToLocalTime().ToString("MM-dd HH:mm");

    public TaskItemVm(ScheduledTask model) => Model = model;

    public void Refresh()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Enabled));
        OnPropertyChanged(nameof(KindText));
        OnPropertyChanged(nameof(LastResult));
        OnPropertyChanged(nameof(NextRun));
    }
}
