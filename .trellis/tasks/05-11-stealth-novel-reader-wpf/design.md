# Technical Design - Stealth Novel Reader

## Architecture Overview

```
┌─────────────────────────────────────────────────┐
│                   App (WPF)                     │
├─────────────┬─────────────┬─────────────────────┤
│  UI Layer   │  Services   │    Data Layer       │
│             │             │                     │
│ MainWindow  │ NovelParser │ BookshelfStore      │
│ (透明窗口)  │ (txt/epub)  │ (JSON持久化)        │
│             │             │                     │
│ SettingsWin │ BookmarkSvc │ SettingsStore       │
│ (设置窗口)  │             │ (JSON持久化)        │
│             │ HotkeySvc   │                     │
│ BookshelfWin│ (全局热键)  │ ReadingProgress     │
│ (书架窗口)  │             │ (JSON持久化)        │
│             │ TrayIconSvc │                     │
│ StatusBar   │ (系统托盘)  │                     │
├─────────────┴─────────────┴─────────────────────┤
│              Models / Entities                   │
│  Novel, Chapter, Bookmark, ReadingProgress,     │
│  AppSettings, HotkeyConfig                      │
└─────────────────────────────────────────────────┘
```

## Project Structure

```
novel-moyo/
├── NovelMoyo.sln
├── src/
│   └── NovelMoyo/
│       ├── NovelMoyo.csproj
│       ├── App.xaml / App.xaml.cs
│       ├── Models/
│       │   ├── Novel.cs              # 小说实体（含章节列表）
│       │   ├── Chapter.cs            # 章节实体
│       │   ├── Bookmark.cs           # 书签实体
│       │   ├── ReadingProgress.cs    # 阅读进度
│       │   ├── AppSettings.cs        # 应用设置
│       │   └── HotkeyConfig.cs       # 快捷键配置
│       ├── Services/
│       │   ├── NovelParser/
│       │   │   ├── INovelParser.cs   # 解析器接口
│       │   │   ├── TxtParser.cs      # txt 解析（正则识别章节）
│       │   │   └── EpubParser.cs     # epub 解析
│       │   ├── BookshelfService.cs   # 书架管理
│       │   ├── BookmarkService.cs    # 书签管理
│       │   ├── HotkeyService.cs      # 全局热键注册/注销
│       │   ├── AutoScrollService.cs  # 自动滚动逻辑
│       │   └── DataStore.cs          # JSON 读写
│       ├── ViewModels/
│       │   ├── MainViewModel.cs
│       │   ├── SettingsViewModel.cs
│       │   └── BookshelfViewModel.cs
│       ├── Views/
│       │   ├── MainWindow.xaml       # 透明阅读窗口
│       │   ├── SettingsWindow.xaml   # 设置窗口
│       │   ├── BookshelfWindow.xaml  # 书架窗口
│       │   └── Controls/
│       │       └── ReadingStatusBar.xaml  # 底部状态栏控件
│       ├── Converters/
│       │   └── ...                   # WPF 值转换器
│       └── Resources/
│           ├── Themes/               # 主题/皮肤资源
│           └── Icons/                # 托盘图标
└── tests/
    └── NovelMoyo.Tests/
```

## Key Technical Decisions

### 1. 透明窗口实现
- 使用 `WindowStyle="None"` + `AllowsTransparency="True"` + `Background="Transparent"`
- 置顶：`Topmost="True"`，通过代码动态切换
- 任务栏隐藏：`ShowInTaskbar="False"`
- 边框显隐：绑定 `MouseEnter`/`MouseLeave` 事件，动态控制 Border 的 `Visibility`

### 2. 鼠标穿透
- Win32 API `SetWindowLong` 设置 `WS_EX_TRANSPARENT | WS_EX_LAYERED`
- 穿透模式下窗口不接收任何鼠标事件
- 需要 P/Invoke 调用 user32.dll

### 3. 全局热键
- Win32 API `RegisterHotKey` / `UnregisterHotKey`
- 在 `Window.SourceInitialized` 事件中注册，通过 `HwndSource.AddHook` 监听 `WM_HOTKEY` 消息
- 窗口关闭时必须 `UnregisterHotKey` 释放

### 4. 小说解析
- **TxtParser**: 按行读取，正则匹配章节标题（`第\d+章`、`Chapter \d+` 等模式），分割为 Chapter 对象
- **EpubParser**: 使用 `VersOne.Epub` NuGet 包解析 epub 结构，提取章节 HTML 内容，转换为纯文本
- 统一接口 `INovelParser.Parse(filePath) -> Novel`

### 5. 自动滚动
- `DispatcherTimer` 定时调用 `ScrollViewer.LineDown()`
- 速度档位：通过调整 Timer Interval 实现（如 5 档：2000ms, 1500ms, 1000ms, 500ms, 200ms）
- 快捷键切换档位时更新 Interval

### 6. 数据存储
- `%AppData%\StealthReader\` 下存储：
  - `settings.json` — 应用设置 + 快捷键配置
  - `bookshelf.json` — 书架列表（文件路径 + 元数据）
  - `progress/{novelId}.json` — 每本书的阅读进度 + 书签
- 使用 `System.Text.Json` 序列化/反序列化
- 写入时先写临时文件再 rename，防止数据损坏

### 7. 系统托盘
- 使用 `System.Windows.Forms.NotifyIcon`（WPF 没有原生托盘控件）
- 右键菜单：打开书架、打开设置、退出
- 左键单击：显示/隐藏阅读窗口

### 8. 主题/皮肤
- WPF ResourceDictionary 切换
- 预定义主题：默认暗色、护眼绿、暖色
- 每个主题定义：背景色、文字颜色、状态栏颜色

## Dependencies (NuGet)

| 包 | 用途 |
|---|---|
| VersOne.Epub | epub 文件解析 |
| System.Text.Json | JSON 序列化（.NET 内置） |
| Microsoft.Toolkit.Uwp.Notifications | 可选：托盘通知 |

## Risk Areas

1. **鼠标穿透 + 滚轮冲突**: 穿透模式下滚轮事件也穿透，无法在穿透时滚动阅读。需要用户退出穿透模式才能滚动，或提供自动滚动作为替代
2. **全局热键冲突**: 用户可能与其他软件快捷键冲突，需要自定义功能 + 冲突检测提示
3. **epub 解析复杂度**: 部分 epub 格式不规范，需要容错处理
