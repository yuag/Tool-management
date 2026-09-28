using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuickLaunch.Core.Models;
using QuickLaunch.Core.Platform;
using QuickLaunch.Core.Services;

namespace QuickLaunch.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly ConfigStore _store;
    private readonly LauncherService _launcher;
    private readonly IGlobalHotkeyService _hotkeys;
    private readonly Action? _hideWindow;
    private readonly Action? _applyChrome;
    private readonly Action<string>? _log;

    [ObservableProperty] private AppConfig _config;
    [ObservableProperty] private GroupNodeVm? _selectedNode;
    [ObservableProperty] private LaunchItemVm? _selectedItem;
    [ObservableProperty] private TaskItemVm? _selectedTask;
    [ObservableProperty] private string _query = string.Empty;
    [ObservableProperty] private bool _isRenameGroupOpen;
    [ObservableProperty] private string _editGroupName = string.Empty;
    [ObservableProperty] private bool _isSearchOpen;
    [ObservableProperty] private bool _isMenuOpen;
    [ObservableProperty] private bool _isAddPanelOpen;
    [ObservableProperty] private bool _isEditPanelOpen;
    [ObservableProperty] private bool _isSettingsOpen;
    [ObservableProperty] private bool _isTasksOpen;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private string _newName = string.Empty;
    [ObservableProperty] private string _newPath = string.Empty;
    [ObservableProperty] private string _newArgs = string.Empty;
    [ObservableProperty] private string _newWorkDir = string.Empty;
    [ObservableProperty] private string _newHotkey = string.Empty;
    [ObservableProperty] private LaunchItemType _newType = LaunchItemType.App;
    [ObservableProperty] private string _moveTargetGroupName = string.Empty;
    [ObservableProperty] private string _editName = string.Empty;
    [ObservableProperty] private string _editPath = string.Empty;
    [ObservableProperty] private string _editArgs = string.Empty;
    [ObservableProperty] private string _editWorkDir = string.Empty;
    [ObservableProperty] private string _editHotkey = string.Empty;
    [ObservableProperty] private LaunchItemType _editType = LaunchItemType.App;

    [ObservableProperty] private string _taskName = string.Empty;
    [ObservableProperty] private bool _taskEnabled = true;
    [ObservableProperty] private bool _taskSilent = true;
    [ObservableProperty] private ScheduleKind _taskKind = ScheduleKind.Interval;
    [ObservableProperty] private int _taskIntervalMinutes = 60;
    [ObservableProperty] private string _taskTimeOfDay = "09:00";
    [ObservableProperty] private string _taskLaunchItemId = string.Empty;
    [ObservableProperty] private string _taskNextIds = string.Empty;

    public ObservableCollection<GroupNodeVm> GroupNodes { get; } = [];
    public ObservableCollection<LaunchItemVm> VisibleItems { get; } = [];
    public ObservableCollection<TaskItemVm> TaskItems { get; } = [];
    public ObservableCollection<string> GroupNames { get; } = [];
    public ObservableCollection<LaunchItemPick> AllLaunchItems { get; } = [];
    public IReadOnlyList<LaunchItemType> ItemTypes { get; } = Enum.GetValues<LaunchItemType>();
    public IReadOnlyList<AppTheme> Themes { get; } = Enum.GetValues<AppTheme>();
    public IReadOnlyList<ScheduleKind> ScheduleKinds { get; } = Enum.GetValues<ScheduleKind>();

    public LaunchGroup? SelectedGroup => SelectedNode?.Model;
    public string ConfigPath => _store.ConfigPath;
    public string Title => "工具管理";

    public double UiScale => Config.Scale;
    public double TileWidth => Config.TileWidth;
    public double TileHeight => Config.TileHeight;
    public double SidebarWidth => Config.SidebarWidth;

    public MainViewModel(
        ConfigStore store,
        AppConfig config,
        LauncherService launcher,
        IGlobalHotkeyService hotkeys,
        Action? hideWindow = null,
        Action? applyChrome = null,
        Action<string>? log = null)
    {
        _store = store;
        _config = config;
        _launcher = launcher;
        _hotkeys = hotkeys;
        _hideWindow = hideWindow;
        _applyChrome = applyChrome;
        _log = log;
        ReloadGroups(selectFirst: true);
        RefreshTasks();
        RefreshItemHotkeys();
        StatusText = $"配置: {ConfigPath}";
    }

    partial void OnQueryChanged(string value) => RefreshVisibleItems();
    partial void OnSelectedNodeChanged(GroupNodeVm? value)
    {
        OnPropertyChanged(nameof(SelectedGroup));
        RefreshVisibleItems();
        MoveTargetGroupName = value?.Model.Name ?? string.Empty;
    }

    [RelayCommand] private void ToggleSearch()
    {
        IsSearchOpen = !IsSearchOpen;
        if (!IsSearchOpen) Query = string.Empty;
    }

    [RelayCommand] private void ToggleMenu() => IsMenuOpen = !IsMenuOpen;

    [RelayCommand]
    private void OpenAddPanel()
    {
        IsMenuOpen = false;
        IsAddPanelOpen = true;
        IsEditPanelOpen = false;
        IsSettingsOpen = false;
        IsTasksOpen = false;
        IsRenameGroupOpen = false;
    }

    [RelayCommand] private void CloseAddPanel() => IsAddPanelOpen = false;

    [RelayCommand]
    private void OpenSettings()
    {
        IsMenuOpen = false;
        IsSettingsOpen = true;
        IsAddPanelOpen = false;
        IsEditPanelOpen = false;
        IsTasksOpen = false;
        IsRenameGroupOpen = false;
    }

    [RelayCommand] private void CloseSettings() => IsSettingsOpen = false;

    [RelayCommand]
    private void OpenRenameGroup()
    {
        if (SelectedGroup is null)
        {
            StatusText = "请先选择分类";
            return;
        }

        IsMenuOpen = false;
        IsRenameGroupOpen = true;
        IsAddPanelOpen = false;
        IsEditPanelOpen = false;
        IsSettingsOpen = false;
        IsTasksOpen = false;
        EditGroupName = SelectedGroup.Name;
    }

    [RelayCommand] private void CloseRenameGroup() => IsRenameGroupOpen = false;

    [RelayCommand]
    private void SaveRenameGroup()
    {
        if (SelectedGroup is null) return;
        var name = EditGroupName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            StatusText = "分类名不能为空";
            return;
        }

        SelectedGroup.Name = name;
        Persist();
        IsRenameGroupOpen = false;
        ReloadGroups(false);
        StatusText = $"已重命名为 {name}";
    }

    [RelayCommand]
    private void SelectGroupNode(GroupNodeVm? node)
    {
        if (node is null) return;
        SelectedNode = node;
    }

    [RelayCommand]
    private void ToggleExpand(GroupNodeVm? node)
    {
        if (node is null || !node.HasChildren) return;
        if (node.IsExpanded) CollapseGroup(node);
        else ExpandGroup(node);
    }

    private void ExpandGroup(GroupNodeVm node)
    {
        var id = node.Model.Id;
        if (Config.AutoCollapseGroups)
        {
            var keep = GetAncestorIds(id);
            keep.Add(id);
            Config.ExpandedGroupIds.RemoveAll(x => !keep.Contains(x));
        }

        if (!Config.ExpandedGroupIds.Contains(id))
            Config.ExpandedGroupIds.Add(id);

        Persist();
        ReloadGroups(false);
        SelectedNode = GroupNodes.FirstOrDefault(n => n.Model.Id == id) ?? SelectedNode;
    }

    private void CollapseGroup(GroupNodeVm node)
    {
        var id = node.Model.Id;
        var drop = CollectDescendantIds(id);
        drop.Add(id);
        Config.ExpandedGroupIds.RemoveAll(drop.Contains);
        Persist();
        ReloadGroups(false);
        SelectedNode = GroupNodes.FirstOrDefault(n => n.Model.Id == id) ?? SelectedNode;
    }

    private HashSet<string> GetAncestorIds(string groupId)
    {
        var set = new HashSet<string>();
        var current = Config.Groups.FirstOrDefault(g => g.Id == groupId);
        while (current is not null && !string.IsNullOrEmpty(current.ParentId))
        {
            set.Add(current.ParentId);
            current = Config.Groups.FirstOrDefault(g => g.Id == current.ParentId);
        }

        return set;
    }

    [RelayCommand]
    private void OpenTasks()
    {
        IsMenuOpen = false;
        IsTasksOpen = true;
        IsAddPanelOpen = false;
        IsEditPanelOpen = false;
        IsSettingsOpen = false;
        RefreshLaunchItemPicks();
        RefreshTasks();
    }

    [RelayCommand] private void CloseTasks() => IsTasksOpen = false;

    [RelayCommand]
    private void OpenEditPanel()
    {
        if (SelectedItem is null)
        {
            StatusText = "请先选中一个项目";
            return;
        }

        IsMenuOpen = false;
        IsEditPanelOpen = true;
        IsAddPanelOpen = false;
        IsSettingsOpen = false;
        IsTasksOpen = false;
        EditName = SelectedItem.Model.Name;
        EditPath = SelectedItem.Model.Path;
        EditArgs = SelectedItem.Model.Arguments;
        EditWorkDir = SelectedItem.Model.WorkingDirectory;
        EditHotkey = SelectedItem.Model.Hotkey;
        EditType = SelectedItem.Model.Type;
    }

    [RelayCommand] private void CloseEditPanel() => IsEditPanelOpen = false;

    [RelayCommand]
    private void Launch(LaunchItemVm? item)
    {
        var target = item ?? SelectedItem;
        if (target is null) return;
        try
        {
            var so = string.IsNullOrWhiteSpace(Query) ? null : Query.Trim();
            _launcher.Launch(target.Model, so);
            var willHide = ShouldHideAfterLaunch(target.Model);
            StatusText = willHide
                ? $"已打开: {target.Name}（已隐藏，Alt+Space / 托盘唤出）"
                : $"已打开: {target.Name}";
            if (willHide) _hideWindow?.Invoke();
        }
        catch (Exception ex)
        {
            StatusText = $"启动失败: {ex.Message}";
        }
    }

    public void LaunchById(string id)
    {
        var item = Config.Groups.SelectMany(g => g.Items).FirstOrDefault(i => i.Id == id);
        if (item is null) return;
        try
        {
            var so = string.IsNullOrWhiteSpace(Query) ? null : Query.Trim();
            _launcher.Launch(item, so);
            var willHide = ShouldHideAfterLaunch(item);
            StatusText = willHide
                ? $"已打开: {item.Name}（已隐藏，Alt+Space / 托盘唤出）"
                : $"已打开: {item.Name}";
            if (willHide) _hideWindow?.Invoke();
        }
        catch (Exception ex)
        {
            StatusText = $"启动失败: {ex.Message}";
        }
    }

    /// <summary>
    /// Documents/folders/urls keep the launcher visible — hiding feels like a crash.
    /// </summary>
    private bool ShouldHideAfterLaunch(LaunchItem item)
    {
        if (!Config.HideAfterLaunch) return false;
        return item.Type is LaunchItemType.App or LaunchItemType.Script;
    }

    [RelayCommand]
    private void AddItem()
    {
        if (string.IsNullOrWhiteSpace(NewName) || string.IsNullOrWhiteSpace(NewPath))
        {
            StatusText = "请填写名称和路径";
            return;
        }

        var group = SelectedGroup ?? EnsureGroup("分类-1");
        group.Items.Add(new LaunchItem
        {
            Name = NewName.Trim(),
            Path = NewPath.Trim(),
            Type = NewType,
            Arguments = NewArgs.Trim(),
            WorkingDirectory = NewWorkDir.Trim(),
            Hotkey = NewHotkey.Trim(),
            SortOrder = group.Items.Count
        });
        Persist();
        ClearNewFields();
        IsAddPanelOpen = false;
        RefreshVisibleItems();
        RefreshItemHotkeys();
        RefreshLaunchItemPicks();
        StatusText = "已添加项目";
    }

    [RelayCommand]
    private void SaveEdit()
    {
        if (SelectedItem is null) return;
        SelectedItem.Model.Name = EditName.Trim();
        SelectedItem.Model.Path = EditPath.Trim();
        SelectedItem.Model.Arguments = EditArgs.Trim();
        SelectedItem.Model.WorkingDirectory = EditWorkDir.Trim();
        SelectedItem.Model.Hotkey = EditHotkey.Trim();
        SelectedItem.Model.Type = EditType;
        Persist();
        IsEditPanelOpen = false;
        RefreshVisibleItems();
        RefreshItemHotkeys();
        RefreshLaunchItemPicks();
        StatusText = "已保存项目";
    }

    [RelayCommand]
    private void AddFiles(IEnumerable<string> paths)
    {
        var group = SelectedGroup ?? EnsureGroup("分类-1");
        var added = 0;
        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path)) continue;
            var item = ShortcutResolver.FromPath(path);
            item.SortOrder = group.Items.Count;
            group.Items.Add(item);
            added++;
        }

        if (added == 0) return;
        Persist();
        RefreshVisibleItems();
        RefreshItemHotkeys();
        RefreshLaunchItemPicks();
        StatusText = $"已添加 {added} 个项目（支持 lnk/url/脚本）";
    }

    public void AddFilesToGroup(string groupId, IEnumerable<string> paths)
    {
        var group = Config.Groups.FirstOrDefault(g => g.Id == groupId);
        if (group is null) return;
        SelectedNode = GroupNodes.FirstOrDefault(n => n.Model.Id == groupId) ?? SelectedNode;
        AddFiles(paths);
    }

    [RelayCommand]
    private void AddCategory()
    {
        IsMenuOpen = false;
        var group = new LaunchGroup
        {
            Name = $"分类-{Config.Groups.Count + 1}",
            ParentId = string.Empty,
            SortOrder = Config.Groups.Count
        };
        Config.Groups.Add(group);
        Persist();
        ReloadGroups(false);
        SelectedNode = GroupNodes.FirstOrDefault(n => n.Model.Id == group.Id);
        StatusText = $"已创建 {group.Name}";
    }

    [RelayCommand]
    private void AddSubCategory()
    {
        IsMenuOpen = false;
        if (SelectedGroup is null)
        {
            StatusText = "请先选择父分类";
            return;
        }

        var parent = SelectedGroup;
        var group = new LaunchGroup
        {
            Name = $"{parent.Name}-子分类",
            ParentId = parent.Id,
            SortOrder = Config.Groups.Count(g => g.ParentId == parent.Id)
        };
        Config.Groups.Add(group);
        if (!Config.ExpandedGroupIds.Contains(parent.Id))
            Config.ExpandedGroupIds.Add(parent.Id);
        Persist();
        ReloadGroups(false);
        SelectedNode = GroupNodes.FirstOrDefault(n => n.Model.Id == group.Id);
        StatusText = $"已创建子分类 {group.Name}";
    }

    [RelayCommand]
    private void RemoveSelected()
    {
        if (SelectedItem is null || SelectedGroup is null) return;
        SelectedGroup.Items.RemoveAll(i => i.Id == SelectedItem.Model.Id);
        Persist();
        RefreshVisibleItems();
        RefreshItemHotkeys();
        RefreshLaunchItemPicks();
        StatusText = "已删除项目";
    }

    [RelayCommand]
    private void RemoveCategory()
    {
        IsMenuOpen = false;
        if (SelectedGroup is null || Config.Groups.Count <= 1)
        {
            StatusText = "至少保留一个分类";
            return;
        }

        var removeIds = CollectDescendantIds(SelectedGroup.Id);
        removeIds.Add(SelectedGroup.Id);
        if (Config.Groups.Count - removeIds.Count < 1)
        {
            StatusText = "至少保留一个分类";
            return;
        }

        Config.Groups.RemoveAll(g => removeIds.Contains(g.Id));
        Persist();
        ReloadGroups(true);
        StatusText = "已删除分类（含子分类）";
    }

    [RelayCommand]
    private void MoveSelectedUp()
    {
        if (SelectedItem is null || SelectedGroup is null) return;
        var list = SelectedGroup.Items.OrderBy(i => i.SortOrder).ToList();
        var idx = list.FindIndex(i => i.Id == SelectedItem.Model.Id);
        if (idx <= 0) return;
        (list[idx - 1].SortOrder, list[idx].SortOrder) = (list[idx].SortOrder, list[idx - 1].SortOrder);
        SelectedGroup.Items = list.OrderBy(i => i.SortOrder).ToList();
        Reindex(SelectedGroup);
        Persist();
        RefreshVisibleItems();
    }

    [RelayCommand]
    private void MoveSelectedDown()
    {
        if (SelectedItem is null || SelectedGroup is null) return;
        var list = SelectedGroup.Items.OrderBy(i => i.SortOrder).ToList();
        var idx = list.FindIndex(i => i.Id == SelectedItem.Model.Id);
        if (idx < 0 || idx >= list.Count - 1) return;
        (list[idx + 1].SortOrder, list[idx].SortOrder) = (list[idx].SortOrder, list[idx + 1].SortOrder);
        SelectedGroup.Items = list.OrderBy(i => i.SortOrder).ToList();
        Reindex(SelectedGroup);
        Persist();
        RefreshVisibleItems();
    }

    [RelayCommand]
    private void MoveSelectedToGroup()
    {
        if (SelectedItem is null || SelectedGroup is null) return;
        var target = Config.Groups.FirstOrDefault(g =>
            string.Equals(g.Name, MoveTargetGroupName, StringComparison.OrdinalIgnoreCase));
        if (target is null || target.Id == SelectedGroup.Id)
        {
            StatusText = "请选择其他分类";
            return;
        }

        var item = SelectedItem.Model;
        SelectedGroup.Items.RemoveAll(i => i.Id == item.Id);
        item.SortOrder = target.Items.Count;
        target.Items.Add(item);
        Persist();
        RefreshVisibleItems();
        StatusText = $"已移动到 {target.Name}";
    }

    [RelayCommand]
    private void AddTask()
    {
        if (string.IsNullOrWhiteSpace(TaskName) || string.IsNullOrWhiteSpace(TaskLaunchItemId))
        {
            StatusText = "请填写任务名称并选择启动项";
            return;
        }

        var task = new ScheduledTask
        {
            Name = TaskName.Trim(),
            Enabled = TaskEnabled,
            Silent = TaskSilent,
            Kind = TaskKind,
            IntervalMinutes = Math.Max(1, TaskIntervalMinutes),
            TimeOfDay = string.IsNullOrWhiteSpace(TaskTimeOfDay) ? "09:00" : TaskTimeOfDay.Trim(),
            LaunchItemId = TaskLaunchItemId,
            NextTaskIds = ParseIdList(TaskNextIds)
        };
        Config.Tasks.Add(task);
        Persist();
        ClearTaskFields();
        RefreshTasks();
        StatusText = $"已添加任务: {task.Name}";
    }

    [RelayCommand]
    private void RemoveTask()
    {
        if (SelectedTask is null) return;
        Config.Tasks.RemoveAll(t => t.Id == SelectedTask.Model.Id);
        Persist();
        RefreshTasks();
        StatusText = "已删除任务";
    }

    [RelayCommand]
    private void ToggleTaskEnabled()
    {
        if (SelectedTask is null) return;
        SelectedTask.Model.Enabled = !SelectedTask.Model.Enabled;
        Persist();
        SelectedTask.Refresh();
        StatusText = SelectedTask.Model.Enabled ? "任务已启用" : "任务已禁用";
    }

    [RelayCommand]
    private void RunTaskNow()
    {
        if (SelectedTask is null) return;
        try
        {
            var item = Config.Groups.SelectMany(g => g.Items)
                .FirstOrDefault(i => i.Id == SelectedTask.Model.LaunchItemId);
            if (item is null)
            {
                StatusText = "未找到关联启动项";
                return;
            }

            _launcher.Launch(item);
            SelectedTask.Model.LastRunUtc = DateTime.UtcNow;
            SelectedTask.Model.LastResult = "OK 手动 " + DateTime.Now.ToString("HH:mm:ss");
            Persist();
            SelectedTask.Refresh();
            StatusText = $"已手动执行: {SelectedTask.Name}";
        }
        catch (Exception ex)
        {
            StatusText = $"执行失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ApplySettings()
    {
        if (Config.Scale < 0.8) Config.Scale = 0.8;
        if (Config.Scale > 1.6) Config.Scale = 1.6;

        // Switching Light/Dark must refresh chrome colors together,
        // otherwise dark theme keeps light backgrounds and looks broken.
        Config.Colors.CopyFrom(ThemeColors.ForTheme(Config.Theme));

        Persist();
        try
        {
            var exe = Environment.ProcessPath ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(exe))
            {
                AutostartService.SetEnabled(Config.StartWithWindows, exe);
            }
        }
        catch (Exception ex)
        {
            StatusText = $"开机启动设置失败: {ex.Message}";
        }

        _hotkeys.TryRegisterToggle(Config.Hotkey);
        RefreshItemHotkeys();
        _applyChrome?.Invoke();
        NotifyLayoutChanged();
        IsSettingsOpen = false;
        StatusText = Config.Theme == AppTheme.Dark ? "已切换深色主题" : "已切换浅色主题";
    }

    [RelayCommand]
    private void Save()
    {
        Persist();
        StatusText = "配置已保存";
    }

    [RelayCommand]
    private void Reload()
    {
        Config = _store.Load();
        ReloadGroups(true);
        RefreshTasks();
        RefreshItemHotkeys();
        _applyChrome?.Invoke();
        StatusText = "已重新加载配置";
    }

    public void AppendLog(string message)
    {
        StatusText = message;
        _log?.Invoke(message);
    }

    public void RefreshTasksFromScheduler()
    {
        // Config already mutated by scheduler; refresh VMs.
        foreach (var t in TaskItems) t.Refresh();
        if (IsTasksOpen) RefreshTasks();
    }

    private static void Reindex(LaunchGroup group)
    {
        var i = 0;
        foreach (var item in group.Items.OrderBy(x => x.SortOrder))
        {
            item.SortOrder = i++;
        }

        group.Items = group.Items.OrderBy(x => x.SortOrder).ToList();
    }

    private void ClearNewFields()
    {
        NewName = NewPath = NewArgs = NewWorkDir = NewHotkey = string.Empty;
        NewType = LaunchItemType.App;
    }

    private void ClearTaskFields()
    {
        TaskName = string.Empty;
        TaskEnabled = true;
        TaskSilent = true;
        TaskKind = ScheduleKind.Interval;
        TaskIntervalMinutes = 60;
        TaskTimeOfDay = "09:00";
        TaskLaunchItemId = string.Empty;
        TaskNextIds = string.Empty;
    }

    private static List<string> ParseIdList(string raw) =>
        string.IsNullOrWhiteSpace(raw)
            ? []
            : raw.Split([',', ';', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

    public void NotifyLayoutChanged()
    {
        OnPropertyChanged(nameof(UiScale));
        OnPropertyChanged(nameof(TileWidth));
        OnPropertyChanged(nameof(TileHeight));
        OnPropertyChanged(nameof(SidebarWidth));
    }

    private void Persist() => _store.Save(Config);

    private LaunchGroup EnsureGroup(string name)
    {
        var existing = Config.Groups.FirstOrDefault(g =>
            string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing is not null) return existing;
        var created = new LaunchGroup { Name = name, SortOrder = Config.Groups.Count };
        Config.Groups.Add(created);
        return created;
    }

    private HashSet<string> CollectDescendantIds(string parentId)
    {
        var result = new HashSet<string>();
        void Walk(string pid)
        {
            foreach (var child in Config.Groups.Where(g => g.ParentId == pid))
            {
                if (result.Add(child.Id)) Walk(child.Id);
            }
        }

        Walk(parentId);
        return result;
    }

    private void ReloadGroups(bool selectFirst)
    {
        var previousId = SelectedNode?.Model.Id;
        GroupNodes.Clear();
        GroupNames.Clear();

        // Simple mode: always show the full tree (no collapse UX).
        Config.AutoCollapseGroups = false;
        foreach (var parent in Config.Groups.Where(g =>
                     Config.Groups.Any(c => (c.ParentId ?? string.Empty) == g.Id)))
        {
            if (!Config.ExpandedGroupIds.Contains(parent.Id))
                Config.ExpandedGroupIds.Add(parent.Id);
        }

        var remaining = Config.Groups.ToDictionary(g => g.Id);
        void AddChildren(string parentId, int depth)
        {
            foreach (var g in remaining.Values
                         .Where(x => (x.ParentId ?? string.Empty) == parentId)
                         .OrderBy(x => x.SortOrder).ThenBy(x => x.Name))
            {
                var hasChildren = remaining.Values.Any(x =>
                    (x.ParentId ?? string.Empty) == g.Id);
                var expanded = Config.ExpandedGroupIds.Contains(g.Id);
                GroupNodes.Add(new GroupNodeVm(g, depth, hasChildren, expanded));
                GroupNames.Add(g.Name);

                // Only show children when parent is expanded
                if (hasChildren && expanded)
                {
                    AddChildren(g.Id, depth + 1);
                }
            }
        }

        AddChildren(string.Empty, 0);

        // Orphan groups (bad ParentId) still show at root.
        var shown = GroupNodes.Select(n => n.Model.Id).ToHashSet();
        foreach (var orphan in Config.Groups.Where(g => !shown.Contains(g.Id)).OrderBy(g => g.SortOrder))
        {
            // Only surface orphans whose parent is missing entirely
            if (!string.IsNullOrEmpty(orphan.ParentId) && remaining.ContainsKey(orphan.ParentId))
                continue;
            var hasChildren = Config.Groups.Any(c => c.ParentId == orphan.Id);
            GroupNodes.Add(new GroupNodeVm(orphan, 0, hasChildren, Config.ExpandedGroupIds.Contains(orphan.Id)));
            if (!GroupNames.Contains(orphan.Name)) GroupNames.Add(orphan.Name);
            if (hasChildren && Config.ExpandedGroupIds.Contains(orphan.Id))
                AddChildren(orphan.Id, 1);
        }

        SelectedNode = selectFirst || previousId is null
            ? GroupNodes.FirstOrDefault()
            : GroupNodes.FirstOrDefault(n => n.Model.Id == previousId) ?? GroupNodes.FirstOrDefault();
        MoveTargetGroupName = SelectedGroup?.Name ?? string.Empty;
        RefreshVisibleItems();
        RefreshLaunchItemPicks();
    }

    private void RefreshVisibleItems()
    {
        var selectedId = SelectedItem?.Model.Id;
        VisibleItems.Clear();

        IEnumerable<LaunchItem> source;
        if (!string.IsNullOrWhiteSpace(Query))
        {
            var q = Query.Trim();
            source = Config.Groups.SelectMany(g => g.Items)
                .Where(i => i.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                            || i.Path.Contains(q, StringComparison.OrdinalIgnoreCase)
                            || i.Arguments.Contains(q, StringComparison.OrdinalIgnoreCase));
        }
        else if (SelectedGroup is not null)
        {
            source = SelectedGroup.Items.OrderBy(i => i.SortOrder).ThenBy(i => i.Name);
        }
        else
        {
            source = [];
        }

        foreach (var item in source)
        {
            VisibleItems.Add(new LaunchItemVm(item));
        }

        SelectedItem = VisibleItems.FirstOrDefault(v => v.Model.Id == selectedId)
                       ?? VisibleItems.FirstOrDefault();
    }

    private void RefreshTasks()
    {
        var selectedId = SelectedTask?.Model.Id;
        TaskItems.Clear();
        foreach (var t in Config.Tasks.OrderBy(t => t.Name))
        {
            TaskItems.Add(new TaskItemVm(t));
        }

        SelectedTask = TaskItems.FirstOrDefault(t => t.Model.Id == selectedId)
                       ?? TaskItems.FirstOrDefault();
    }

    private void RefreshLaunchItemPicks()
    {
        AllLaunchItems.Clear();
        foreach (var item in Config.Groups.SelectMany(g => g.Items).OrderBy(i => i.Name))
        {
            AllLaunchItems.Add(new LaunchItemPick(item.Id, item.Name));
        }
    }

    private void RefreshItemHotkeys()
    {
        var pairs = Config.Groups
            .SelectMany(g => g.Items)
            .Where(i => !string.IsNullOrWhiteSpace(i.Hotkey))
            .Select(i => (i.Id, i.Hotkey));
        _hotkeys.RegisterItemHotkeys(pairs);
    }
}

public sealed record LaunchItemPick(string Id, string Name)
{
    public override string ToString() => Name;
}
