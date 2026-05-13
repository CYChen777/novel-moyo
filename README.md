# NovelMoyo - 摸鱼小说阅读器 | Stealth Novel Reader

<p align="center">
  <em>一款 Windows 桌面端隐蔽小说阅读器，让你在工作时悄悄看书</em><br/>
  <em>A stealthy Windows desktop novel reader — read novels at work without being noticed</em>
</p>

---

## 简介 | Introduction

**NovelMoyo** 是一款专为 Windows 设计的"摸鱼"小说阅读器。通过背景透明、无任务栏、鼠标穿透等特性，让用户可以在工作时隐蔽地阅读小说。

**NovelMoyo** is a "stealth" novel reader designed for Windows. With features like transparent backgrounds, no taskbar presence, and mouse passthrough, you can read novels at work without drawing attention.

## 功能特性 | Features

### 核心阅读 | Core Reading

- **透明无边框窗口** — 背景完全透明，仅显示文字，可自由调整透明度
- **鼠标悬停显隐** — 鼠标移入显示文本，移出自动隐藏
- **锁定模式** — 锁定后鼠标移开文字也不会消失
- **鼠标穿透** — 穿透模式下鼠标直接穿过窗口，可操作下层应用
- **置顶显示** — 默认置顶，可随时切换

> **Transparent borderless window** — fully transparent background showing only text, with adjustable opacity
> **Hover to show/hide** — text appears on mouse enter, hides on mouse leave
> **Lock mode** — keep text visible even when mouse moves away
> **Mouse passthrough** — click through the window to interact with apps underneath
> **Always on top** — pinned by default, toggle anytime

### 文本导航 | Text Navigation

- **滚轮滚动** — 鼠标滚轮上下翻页，速度可调（0.5x ~ 4.5x）
- **自动滚动** — 5 档速度自动滚动，快捷键加减速
- **章节跳转** — 快捷键切换上一章/下一章
- **渐进式加载** — 仅加载当前章节附近内容，轻量流畅

> **Mouse wheel scrolling** — scroll up/down with adjustable speed (0.5x ~ 4.5x)
> **Auto scroll** — 5-speed auto scrolling with hotkey speed control
> **Chapter navigation** — jump to previous/next chapter via hotkeys
> **Progressive loading** — only loads nearby chapters for smooth performance

### 文件格式 | File Formats

- 支持 **.txt** 格式（自动识别章节标题，支持 GBK/UTF-8/UTF-16 编码）
- 支持 **.epub** 格式（使用 VersOne.Epub 解析）
- 导入方式：文件选择器 + 拖拽导入

> Supports **.txt** (auto chapter detection, GBK/UTF-8/UTF-16 encoding) and **.epub** (via VersOne.Epub)
> Import via file picker or drag & drop

### 书架与进度 | Bookshelf & Progress

- **书架管理** — 展示所有导入的小说列表，点击切换
- **独立进度** — 每本书独立保存阅读进度，切换后自动恢复
- **书签功能** — 可在任意位置添加多个书签，支持备注
- **最近阅读** — 记录最近阅读的小说

> **Bookshelf** — view all imported novels, switch with one click
> **Independent progress** — each book saves progress separately, auto-restores on switch
> **Bookmarks** — add multiple bookmarks with notes at any position
> **Recent reads** — tracks recently opened novels

### 设置界面 | Settings

- **外观** — 透明度、字体大小、字体颜色、背景色、主题皮肤
- **阅读** — 行间距、段间距、自动滚动速度、滚轮速度
- **快捷键** — 所有快捷键均可自定义重新绑定
- **其他** — 开机自启动、启动时自动打开上次阅读

> **Appearance** — opacity, font size, font color, background color, themes
> **Reading** — line spacing, paragraph spacing, auto-scroll speed, wheel speed
> **Hotkeys** — all hotkeys are fully customizable
> **Others** — auto-start on boot, resume last novel on launch

### 全局快捷键 | Global Hotkeys

| 功能 Function | 默认快捷键 Default Hotkey |
|---|---|
| 显示/隐藏阅读窗口 Show/Hide window | `Ctrl+Alt+H` |
| 锁定/解锁显示 Lock/Unlock display | `Ctrl+Alt+L` |
| 开启/关闭自动滚动 Toggle auto scroll | `Ctrl+Alt+S` |
| 滚动加速 Speed up scroll | `Ctrl+Alt+Up` |
| 滚动减速 Slow down scroll | `Ctrl+Alt+Down` |
| 上一章 Previous chapter | `Ctrl+Alt+Left` |
| 下一章 Next chapter | `Ctrl+Alt+Right` |
| 鼠标穿透开/关 Toggle mouse passthrough | `Ctrl+Alt+P` |
| 切换置顶 Toggle always on top | `Ctrl+Alt+T` |
| 添加书签 Add bookmark | `Ctrl+Alt+M` |
| 打开书架 Open bookshelf | `Ctrl+Alt+B` |
| 打开设置 Open settings | `Ctrl+Alt+,` |

### 系统托盘 | System Tray

- 程序启动后静默最小化到系统托盘，不在任务栏显示
- 托盘右键菜单：打开书架、打开设置、退出
- 托盘左键单击：显示/隐藏阅读窗口

> Minimizes to system tray on launch, no taskbar presence
> Right-click menu: bookshelf, settings, exit
> Left-click: toggle reading window

## 系统要求 | Requirements

- **操作系统**: Windows 10 / 11 (x64)
- **运行时**: .NET 8.0 Desktop Runtime（框架依赖模式需要）

> **OS**: Windows 10 / 11 (x64)
> **Runtime**: .NET 8.0 Desktop Runtime (required for framework-dependent mode)

## 安装方式 | Installation

### 方式一：下载安装包 | Option 1: Installer

从 [Releases](https://github.com/CYChen777/novel-moyo/releases) 下载 `NovelMoyo-Setup.exe`，运行安装即可。

Download `NovelMoyo-Setup.exe` from [Releases](https://github.com/CYChen777/novel-moyo/releases) and run the installer.

### 方式二：免安装版本 | Option 2: Portable

从 [Releases](https://github.com/CYChen777/novel-moyo/releases) 下载单文件 `NovelMoyo.exe`，直接运行。删除文件即卸载。用户数据存储在 `%APPDATA%\StealthReader\`。

Download the single-file `NovelMoyo.exe` from [Releases](https://github.com/CYChen777/novel-moyo/releases) and run directly. Delete the file to uninstall. User data is stored at `%APPDATA%\StealthReader\`.

### 方式三：从源码构建 | Option 3: Build from Source

```bash
# 克隆仓库 | Clone the repo
git clone https://github.com/CYChen777/novel-moyo.git
cd novel-moyo

# 构建 | Build
dotnet build src/NovelMoyo/NovelMoyo.csproj

# 运行 | Run
dotnet run --project src/NovelMoyo/NovelMoyo.csproj

# 发布（框架依赖，约 2.5MB）| Publish (framework-dependent, ~2.5MB)
dotnet publish src/NovelMoyo/NovelMoyo.csproj -c Release -r win-x64

# 发布（独立部署，约 155MB）| Publish (self-contained, ~155MB)
dotnet publish src/NovelMoyo/NovelMoyo.csproj -c Release -r win-x64 --self-contained
```

## 项目结构 | Project Structure

```
novel-moyo/
├── NovelMoyo.sln
├── installer.iss                    # Inno Setup 安装脚本
└── src/NovelMoyo/
    ├── App.xaml / App.xaml.cs       # 应用入口 Composition root
    ├── Models/                      # 数据模型 POCOs
    │   ├── AppSettings.cs           # 应用设置
    │   ├── Bookmark.cs              # 书签
    │   ├── BookshelfEntry.cs        # 书架条目
    │   ├── Chapter.cs               # 章节
    │   ├── HotkeyEntry.cs           # 快捷键配置
    │   ├── Novel.cs                 # 小说实体
    │   └── ReadingProgress.cs       # 阅读进度
    ├── Services/                    # 业务逻辑
    │   ├── NovelParser/             # 小说解析器（txt/epub）
    │   ├── AutoScrollService.cs     # 自动滚动
    │   ├── BookmarkService.cs       # 书签管理
    │   ├── BookshelfService.cs      # 书架管理
    │   ├── DataStore.cs             # JSON 数据持久化
    │   └── HotkeyService.cs         # 全局热键
    ├── ViewModels/                  # MVVM 视图模型
    ├── Views/                       # XAML 窗口
    │   ├── MainWindow.xaml(.cs)     # 透明阅读窗口
    │   ├── SettingsWindow.xaml(.cs) # 设置窗口
    │   ├── BookshelfWindow.xaml(.cs)# 书架窗口
    │   └── Controls/                # 自定义控件
    ├── Converters/                  # WPF 值转换器
    └── Resources/Themes/            # 主题资源（暗色/护眼绿/暖色）
```

## 技术栈 | Tech Stack

| 技术 Technology | 用途 Purpose |
|---|---|
| WPF (.NET 8) | 桌面 UI 框架 |
| C# 12 | 编程语言 |
| VersOne.Epub | EPUB 文件解析 |
| System.Text.Json | JSON 序列化 |
| Win32 API (P/Invoke) | 全局热键、鼠标穿透、鼠标钩子 |
| Inno Setup | 安装包制作 |

## 数据存储 | Data Storage

所有用户数据存储在 `%APPDATA%\StealthReader\`：

```
%APPDATA%\StealthReader\
├── settings.json              # 应用设置 + 快捷键配置
├── bookshelf.json             # 书架列表
└── progress/
    └── {novelId}.json         # 每本书的阅读进度 + 书签
```

All user data is stored at `%APPDATA%\StealthReader\`:
- `settings.json` — app settings and hotkey configuration
- `bookshelf.json` — bookshelf list
- `progress/{novelId}.json` — reading progress and bookmarks per novel

## 许可证 | License

本项目仅供学习交流使用。

This project is for educational and personal use only.
