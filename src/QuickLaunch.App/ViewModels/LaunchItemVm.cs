using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using QuickLaunch.App.Services;
using QuickLaunch.Core.Models;

namespace QuickLaunch.App.ViewModels;

public sealed partial class LaunchItemVm : ObservableObject
{
    public LaunchItem Model { get; }
    public string Name => Model.Name;
    public string Path => Model.Path;
    public string Hotkey => Model.Hotkey;
    public LaunchItemType Type => Model.Type;
    public string TypeLabel => IconFactory.GetTypeLabel(Model);
    /// <summary>Hotkey if set, otherwise file type like TXT / EXE.</summary>
    public string Subtitle =>
        !string.IsNullOrWhiteSpace(Model.Hotkey) ? Model.Hotkey : TypeLabel;
    public Bitmap Icon { get; }

    public LaunchItemVm(LaunchItem model)
    {
        Model = model;
        Icon = IconFactory.Get(model);
    }
}
