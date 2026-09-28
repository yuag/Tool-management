using Avalonia;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using QuickLaunch.Core.Models;

namespace QuickLaunch.App.ViewModels;

public sealed partial class GroupNodeVm : ObservableObject
{
    public LaunchGroup Model { get; }
    public int Depth { get; }
    public bool HasChildren { get; }

    [ObservableProperty] private bool _isExpanded;

    public bool IsNested => Depth > 0;
    public string Name => Model.Name;
    public string TreeMark => !HasChildren ? "●" : IsExpanded ? "▼" : "▶";
    public FontWeight NameWeight => Depth == 0 ? FontWeight.SemiBold : FontWeight.Normal;
    public double NameOpacity => Depth == 0 ? 1.0 : 0.92;
    /// <summary>Left padding only — keeps selection full-width while nesting.</summary>
    public Thickness RowPadding => new(6 + Depth * 12, 7, 8, 7);
    public string Hint => IsNested
        ? $"{Name}\n虚拟目录 · 层级 {Depth}"
        : HasChildren
            ? $"{Name}\n{(IsExpanded ? "点击收起子分类" : "点击展开子分类")}"
            : Name;

    public GroupNodeVm(LaunchGroup model, int depth, bool hasChildren, bool isExpanded)
    {
        Model = model;
        Depth = depth;
        HasChildren = hasChildren;
        _isExpanded = isExpanded;
    }

    public void NotifyExpandedChanged()
    {
        OnPropertyChanged(nameof(IsExpanded));
        OnPropertyChanged(nameof(TreeMark));
        OnPropertyChanged(nameof(Hint));
    }
}
