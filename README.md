# 工具管理

跨平台快速启动器（Avalonia + .NET 8）：分类管理常用程序 / 文件 / 网址，热键呼出，拖拽添加。

## 怎么用

1. 拖文件或快捷方式到窗口
2. 左侧管理分类；右键可添加 / 重命名 / 删除
3. 默认 `Alt+Space` 呼出（可在设置里改）
4. 单击打开；`Ctrl+单击` 编辑

## 设置

- 呼出热键、浅色 / 深色、缩放、侧栏宽度
- 开机启动、启动程序后隐藏窗口

## 编译

一键多平台（需已安装 [.NET 8 SDK](https://dotnet.microsoft.com/download)）：

```bat
publish-all.bat
```

或单独发布：

```bat
dotnet publish src/QuickLaunch.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish/win-x64
dotnet publish src/QuickLaunch.App -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o publish/linux-x64
dotnet publish src/QuickLaunch.App -c Release -r osx-x64 --self-contained true -p:PublishSingleFile=true -o publish/osx-x64
dotnet publish src/QuickLaunch.App -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true -o publish/osx-arm64
```

| 平台 | 目录 | 运行 |
|------|------|------|
| Windows | `publish/win-x64/QuickLaunch.exe` | 双击 |
| Linux | `publish/linux-x64/QuickLaunch` | `chmod +x QuickLaunch && ./QuickLaunch` |
| Intel Mac | `publish/osx-x64/QuickLaunch` | 同上 |
| Apple Silicon | `publish/osx-arm64/QuickLaunch` | 同上 |

说明：全局热键与开机启动目前主要在 Windows 可用；Linux / macOS 可用界面与拖拽启动。

配置文件位置：

- Windows：`%AppData%/QuickLaunch/config.json`
- macOS：`~/Library/Application Support/QuickLaunch/config.json`
- Linux：`~/.config/QuickLaunch/config.json`
