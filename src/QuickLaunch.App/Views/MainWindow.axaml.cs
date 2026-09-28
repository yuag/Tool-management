using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using QuickLaunch.App.ViewModels;
using QuickLaunch.Core.Models;

namespace QuickLaunch.App.Views;

public partial class MainWindow : Window
{
    private const double ResizeEdge = 5;
    private bool _reallyExit;
    private TrayIcon? _tray;
    private WindowEdge? _hoverEdge;
    private CancellationTokenSource? _autoHideCts;

    public Action<bool>? OnVisibilityChanged { get; set; }

    public MainWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
        Closed += OnClosed;
        PropertyChanged += OnWindowPropertyChanged;
        Activated += OnActivated;
        Deactivated += OnDeactivated;
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        DataContextChanged += (_, _) => ApplyChrome();
    }

    public void ApplyChrome()
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        Application.Current!.RequestedThemeVariant = vm.Config.Theme == AppTheme.Dark
            ? ThemeVariant.Dark
            : ThemeVariant.Light;

        App.ApplyThemeColors(vm.Config.Colors);
        vm.NotifyLayoutChanged();
    }

    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == IsVisibleProperty)
        {
            OnVisibilityChanged?.Invoke(IsVisible);
        }
    }

    private void OnActivated(object? sender, EventArgs e) => _autoHideCts?.Cancel();

    private async void OnDeactivated(object? sender, EventArgs e)
    {
        if (DataContext is not MainViewModel vm || !vm.Config.AutoHideOnDeactivate)
            return;

        _autoHideCts?.Cancel();
        _autoHideCts = new CancellationTokenSource();
        var token = _autoHideCts.Token;
        try
        {
            await Task.Delay(220, token);
            if (!token.IsCancellationRequested && !IsActive && IsVisible)
            {
                HideWindow();
            }
        }
        catch (TaskCanceledException)
        {
            // ignore
        }
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        EnsureTray();
        ApplyChrome();
        OnVisibilityChanged?.Invoke(true);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _autoHideCts?.Cancel();
        _tray?.Dispose();
        _tray = null;
    }

    private void EnsureTray()
    {
        if (_tray is not null) return;
        _tray = new TrayIcon
        {
            ToolTipText = "工具管理",
            IsVisible = true,
            Icon = CreateTrayIcon()
        };
        var menu = new NativeMenu();
        var showItem = new NativeMenuItem("显示/隐藏");
        showItem.Click += OnTrayShow;
        var exitItem = new NativeMenuItem("退出");
        exitItem.Click += OnTrayExit;
        menu.Add(showItem);
        menu.Add(new NativeMenuItemSeparator());
        menu.Add(exitItem);
        _tray.Menu = menu;
        _tray.Clicked += OnTrayShow;
    }

    private static WindowIcon CreateTrayIcon()
    {
        const int size = 32;
        var bitmap = new WriteableBitmap(new PixelSize(size, size), new Vector(96, 96), PixelFormats.Bgra8888, AlphaFormat.Premul);
        using var fb = bitmap.Lock();
        unsafe
        {
            var ptr = (byte*)fb.Address;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var i = y * fb.RowBytes + x * 4;
                var inside = x is >= 4 and < 28 && y is >= 4 and < 28;
                ptr[i] = inside ? (byte)0x60 : (byte)0;
                ptr[i + 1] = ptr[i];
                ptr[i + 2] = ptr[i];
                ptr[i + 3] = inside ? (byte)0xFF : (byte)0;
            }
        }

        return new WindowIcon(bitmap);
    }

    public void ToggleVisible()
    {
        if (IsVisible && WindowState != WindowState.Minimized)
        {
            HideWindow();
            return;
        }

        ShowFromTray();
    }

    public void HideWindow()
    {
        // Never Shutdown — only hide to tray. Process must keep running.
        ShowInTaskbar = false;
        Hide();
        OnVisibilityChanged?.Invoke(false);
    }

    public void ShowFromTray()
    {
        ShowInTaskbar = true;
        Show();
        WindowState = WindowState.Normal;
        Activate();
        Topmost = true;
        Topmost = false;
        OnVisibilityChanged?.Invoke(true);
    }

    private void OnTitleBarPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        if (point.Properties.IsLeftButtonPressed)
        {
            if (e.ClickCount == 2)
            {
                ToggleMaximize();
                e.Handled = true;
                return;
            }

            BeginMoveDrag(e);
        }
    }

    private void OnMaximizeClick(object? sender, RoutedEventArgs e) => ToggleMaximize();

    private void ToggleMaximize() =>
        WindowState = WindowState == WindowState.Maximized
            ? global::Avalonia.Controls.WindowState.Normal
            : global::Avalonia.Controls.WindowState.Maximized;

    private void OnMinimizeClick(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void OnCloseClick(object? sender, RoutedEventArgs e) => HideWindow();
    private void OnMenuClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm) vm.ToggleMenuCommand.Execute(null);
    }

    private void OnWindowPointerMoved(object? sender, PointerEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            ClearValue(CursorProperty);
            _hoverEdge = null;
            return;
        }

        _hoverEdge = HitTestResizeEdge(e.GetPosition(this));
        if (_hoverEdge is null)
        {
            ClearValue(CursorProperty);
            return;
        }

        Cursor = _hoverEdge switch
        {
            WindowEdge.West or WindowEdge.East => new Cursor(StandardCursorType.SizeWestEast),
            WindowEdge.North or WindowEdge.South => new Cursor(StandardCursorType.SizeNorthSouth),
            WindowEdge.NorthWest or WindowEdge.SouthEast => new Cursor(StandardCursorType.TopLeftCorner),
            WindowEdge.NorthEast or WindowEdge.SouthWest => new Cursor(StandardCursorType.TopRightCorner),
            _ => Cursor.Default
        };
    }

    private void OnWindowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if (WindowState == WindowState.Maximized) return;

        var edge = HitTestResizeEdge(e.GetPosition(this));
        if (edge is null) return;

        BeginResizeDrag(edge.Value, e);
        e.Handled = true;
    }

    private void OnWindowPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
    }

    private void OnWindowPointerExited(object? sender, PointerEventArgs e)
    {
        ClearValue(CursorProperty);
        _hoverEdge = null;
    }

    private WindowEdge? HitTestResizeEdge(Point p)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w <= 0 || h <= 0) return null;

        var sidebar = DataContext is MainViewModel vm ? vm.SidebarWidth : 160;

        var left = p.X <= ResizeEdge;
        var right = p.X >= w - ResizeEdge;
        var top = p.Y <= ResizeEdge;
        var bottom = p.Y >= h - ResizeEdge;

        // Don't steal clicks from category list on the left.
        if (p.X < sidebar && p.Y > 36 && p.X > 2)
        {
            left = false;
        }

        if (top && left) return WindowEdge.NorthWest;
        if (top && right) return WindowEdge.NorthEast;
        if (bottom && left) return WindowEdge.SouthWest;
        if (bottom && right) return WindowEdge.SouthEast;
        if (left) return WindowEdge.West;
        if (right) return WindowEdge.East;
        if (top) return WindowEdge.North;
        if (bottom) return WindowEdge.South;
        return null;
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        if (e.Key == Key.Escape)
        {
            if (vm.IsAddPanelOpen) vm.CloseAddPanelCommand.Execute(null);
            else if (vm.IsEditPanelOpen) vm.CloseEditPanelCommand.Execute(null);
            else if (vm.IsRenameGroupOpen) vm.CloseRenameGroupCommand.Execute(null);
            else if (vm.IsSettingsOpen) vm.CloseSettingsCommand.Execute(null);
            else if (vm.IsSearchOpen) vm.ToggleSearchCommand.Execute(null);
            else if (vm.IsMenuOpen) vm.ToggleMenuCommand.Execute(null);
            else HideWindow();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            if (vm.IsRenameGroupOpen)
            {
                vm.SaveRenameGroupCommand.Execute(null);
                e.Handled = true;
                return;
            }

            vm.LaunchCommand.Execute(vm.SelectedItem);
            e.Handled = true;
        }
        else if (e.Key == Key.F && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            vm.ToggleSearchCommand.Execute(null);
            if (vm.IsSearchOpen) QueryBox?.Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.Up && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            vm.MoveSelectedUpCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Down && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            vm.MoveSelectedDownCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnTilePressed(object? sender, PointerPressedEventArgs e)
    {
        var props = e.GetCurrentPoint(this).Properties;
        if (sender is not Border { Tag: LaunchItemVm item } || DataContext is not MainViewModel vm)
        {
            return;
        }

        if (props.IsRightButtonPressed)
        {
            vm.SelectedItem = item;
            vm.IsMenuOpen = false;
            return;
        }

        if (!props.IsLeftButtonPressed) return;

        vm.SelectedItem = item;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            vm.OpenEditPanelCommand.Execute(null);
        }
        else
        {
            vm.LaunchCommand.Execute(item);
        }

        e.Handled = true;
    }

    private void OnSidebarGroupPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if (sender is not Border { Tag: GroupNodeVm node } || DataContext is not MainViewModel vm)
            return;

        vm.IsMenuOpen = false;
        vm.SelectGroupNodeCommand.Execute(node);
        e.Handled = true;
    }

    private void OnExpandGlyphPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if (sender is not Border { Tag: GroupNodeVm node } || DataContext is not MainViewModel vm)
            return;

        vm.SelectedNode = node;
        if (node.HasChildren)
        {
            vm.ToggleExpandCommand.Execute(node);
        }

        e.Handled = true;
    }

    private void OnSidebarContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        if (sender is Border { Tag: GroupNodeVm node })
        {
            vm.SelectedNode = node;
        }

        ShowMenu(CreateSidebarMenu(vm), sender as Control, e);
    }

    private void OnContentContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        ShowMenu(CreateContentMenu(vm), sender as Control, e);
    }

    private void OnTileContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        if (sender is Border { Tag: LaunchItemVm item })
        {
            vm.SelectedItem = item;
        }

        ShowMenu(CreateTileMenu(vm), sender as Control, e);
    }

    private static void ShowMenu(ContextMenu menu, Control? target, ContextRequestedEventArgs e)
    {
        if (target is null) return;
        menu.Open(target);
        e.Handled = true;
    }

    private static ContextMenu CreateSidebarMenu(MainViewModel vm) => new()
    {
        Items =
        {
            new MenuItem { Header = "添加项目", Command = vm.OpenAddPanelCommand },
            new MenuItem { Header = "添加分类", Command = vm.AddCategoryCommand },
            new MenuItem { Header = "重命名分类", Command = vm.OpenRenameGroupCommand },
            new Separator(),
            new MenuItem { Header = "删除分类", Command = vm.RemoveCategoryCommand }
        }
    };

    private static ContextMenu CreateContentMenu(MainViewModel vm) => new()
    {
        Items =
        {
            new MenuItem { Header = "添加项目", Command = vm.OpenAddPanelCommand },
            new MenuItem { Header = "添加分类", Command = vm.AddCategoryCommand },
            new Separator(),
            new MenuItem { Header = "编辑项目", Command = vm.OpenEditPanelCommand },
            new MenuItem { Header = "删除项目", Command = vm.RemoveSelectedCommand }
        }
    };

    private static ContextMenu CreateTileMenu(MainViewModel vm) => new()
    {
        Items =
        {
            new MenuItem { Header = "打开", Command = vm.LaunchCommand, CommandParameter = vm.SelectedItem },
            new MenuItem { Header = "编辑", Command = vm.OpenEditPanelCommand },
            new Separator(),
            new MenuItem { Header = "删除", Command = vm.RemoveSelectedCommand }
        }
    };

    private void OnDragOver(object? sender, DragEventArgs e)
        => e.DragEffects = e.Data.Contains(DataFormats.Files) ? DragDropEffects.Copy : DragDropEffects.None;

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not MainViewModel vm || !e.Data.Contains(DataFormats.Files)) return;
        var files = e.Data.GetFiles()?.Select(f => f.Path.LocalPath).Where(p => !string.IsNullOrWhiteSpace(p)).ToArray();
        if (files is not { Length: > 0 }) return;

        if (e.Source is Control control)
        {
            var border = control as Border ?? control.FindAncestorOfType<Border>();
            if (border?.Tag is GroupNodeVm node)
            {
                vm.AddFilesToGroup(node.Model.Id, files);
                e.Handled = true;
                return;
            }
        }

        vm.AddFilesCommand.Execute(files);
        e.Handled = true;
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_reallyExit) return;
        e.Cancel = true;
        HideWindow();
    }

    private void OnTrayShow(object? sender, EventArgs e) => ShowFromTray();

    private void OnTrayExit(object? sender, EventArgs e)
    {
        _reallyExit = true;
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
        else
            Close();
    }
}

internal static class VisualTreeHelpers
{
    public static T? FindAncestorOfType<T>(this Control control) where T : Control
    {
        var current = control.Parent;
        while (current is not null)
        {
            if (current is T match) return match;
            current = current.Parent;
        }

        return null;
    }
}
