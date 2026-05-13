# Implementation Plan - Stealth Novel Reader

## Phase 1: 项目骨架与基础窗口

### 1.1 初始化项目
- [x] 创建 .NET 8 WPF 项目 `NovelMoyo`
- [x] 创建解决方案 `NovelMoyo.sln`
- [x] 添加 NuGet 包：`VersOne.Epub`
- [x] 创建目录结构（Models, Services, Views, ViewModels, Resources）

### 1.2 基础透明窗口
- [x] 实现 `MainWindow.xaml`：无边框、透明背景、不显示在任务栏
- [x] 实现置顶功能（Topmost 绑定）
- [x] 实现鼠标悬停显示边框、移出隐藏边框
- [x] 实现窗口拖拽（MouseLeftButtonDown + DragMove）
- [x] 实现窗口调整大小（自定义 ResizeGrip 或边框拖拽）

### 1.3 系统托盘
- [x] 实现托盘图标（NotifyIcon）
- [x] 托盘右键菜单：打开书架、设置、退出
- [x] 左键单击切换阅读窗口显隐
- [x] 程序启动时静默到托盘（StartMinimizedToTray 设置项）

## Phase 2: 文本解析与阅读

### 2.1 小说解析器
- [x] 定义 `INovelParser` 接口和 `Novel`/`Chapter` 模型
- [x] 实现 `TxtParser`：读取 txt 文件，正则识别章节标题，分割章节
- [x] 实现 `EpubParser`：使用 VersOne.Epub 解析，提取章节纯文本
- [x] 实现解析器工厂，根据文件扩展名选择解析器

### 2.2 阅读窗口核心
- [x] 实现文本渲染区域（TextBlock inside ScrollViewer）
- [x] 实现滚轮滚动（低级鼠标钩子 + 速度倍率 0.5x~4.5x）
- [x] 实现底部状态栏（章节名 + 阅读百分比）
- [x] 实现章节跳转（上一章/下一章）
- [x] 渐进式章节加载（仅加载当前章节前后各3章，动态追加/裁剪）

### 2.3 自动滚动
- [x] 实现 `AutoScrollService`：DispatcherTimer + 5速度档位
- [x] 快捷键开启/关闭自动滚动
- [x] 快捷键加/减速
- [x] 自动滚动应用 ScrollSpeed 倍率

## Phase 3: 高级窗口特性

### 3.1 鼠标穿透
- [x] P/Invoke `SetWindowLongPtr` 实现 WS_EX_TRANSPARENT（64位安全）
- [x] 快捷键切换穿透模式
- [x] 穿透模式下通过自动滚动替代滚轮操作
- [x] 穿透模式指示器（"穿透模式" 角标）

### 3.2 显隐锁定
- [x] 实现锁定状态 bool
- [x] 锁定时鼠标移出不隐藏文本
- [x] 快捷键切换锁定

### 3.3 全局热键
- [x] 实现 `HotkeyService`：RegisterHotKey / UnregisterHotKey
- [x] 注册所有默认快捷键（12个，含添加书签）
- [x] HwndSource Hook 监听 WM_HOTKEY 分发到对应操作
- [x] 快捷键自定义 UI + 冲突检测
- [x] 快捷键持久化保存

## Phase 4: 书架与进度

### 4.1 数据存储
- [x] 实现 `DataStore`：JSON 读写，AppData 路径管理
- [x] 实现写入时原子替换（File.Replace，NTFS 原子操作）
- [x] 定义 settings.json / bookshelf.json / progress/*.json 结构
- [x] NovelId 文件名安全过滤

### 4.2 书架功能
- [x] 实现 `BookshelfService`：添加/删除/列表小说（含解析缓存）
- [x] 实现 `BookshelfWindow`：展示书架列表，点击切换
- [x] 实现文件选择器导入
- [x] 实现拖拽导入（Drop 事件处理）
- [x] 书架显示每本书阅读进度
- [x] 文件缺失检测

### 4.3 阅读进度与书签
- [x] 实现进度自动保存（3秒防抖 + 关闭/切换时保存）
- [x] 实现手动书签（标记位置 + 备注 + 滚动比例跳转）— Ctrl+Alt+M 添加、设置窗口书签Tab查看/跳转/删除
- [x] 实现最近阅读记录
- [x] 切换小说时恢复进度（scroll ratio 恢复）
- [x] 启动时自动加载上次阅读小说（StartWithLastNovel）
- [x] 打开新小说时自动保存当前进度

## Phase 5: 设置界面

### 5.1 设置窗口
- [x] 实现 `SettingsWindow.xaml`：Tab 分组（外观、阅读、快捷键、目录、书签、其他）
- [x] 透明度滑块
- [x] 字体大小滑块
- [x] 字体颜色选择（8预设 + 自定义取色器）
- [x] 背景色选择（8预设 + 自定义取色器）
- [x] 行间距调节
- [x] 段间距调节 — 设置界面滑块 + 段落间插入额外换行
- [x] 主题/皮肤切换
- [x] 自动滚动速度调节
- [x] 滚轮速度调节（0.5x~4.5x，0.25步进）
- [x] 开机自启动复选框（注册表读写）
- [x] 启动最小化到托盘复选框

### 5.2 快捷键自定义
- [x] 设置界面展示所有快捷键及其当前绑定（12个，固定顺序）
- [x] 点击快捷键输入框，捕获新的按键组合
- [x] 保存自定义快捷键，立即生效（含冲突检测）
- [x] 新增热键自动合并到已有用户的设置文件

### 5.3 主题系统
- [x] 创建 ResourceDictionary 主题文件（暗色、护眼绿、暖色）
- [x] 实现运行时动态切换主题（只替换 Theme.xaml 字典，保留其他合并字典）

## Phase 6: 打磨与测试

### 6.1 边界情况处理
- [x] 文件编码检测（GBK/UTF-8/UTF-16）— CodePagesEncodingProvider 启动时注册 + CJK 内容校验防乱码
- [x] epub 解析容错（readingOrder null 项跳过）
- [x] 窗口拖出屏幕的恢复机制（EnsureWindowOnScreen + DPI 适配，无 dummy HwndSource）
- [x] 多显示器适配
- [x] \r\n 换行符统一处理

### 6.2 打包
- [x] 配置 PublishSingleFile 单文件发布
- [x] 配置 SelfContained 或 FrameworkDependent
- [x] 测试安装/卸载流程 — 免安装，复制即用，删除即卸，数据在 %APPDATA%\StealthReader

### 6.3 Bug 修复记录

#### 第一轮修复（初始构建问题）
- [x] WindowInteropHelper.GetHandle → new WindowInteropHelper(owner).Handle
- [x] 设置保存双重应用 → 提取 ApplySettingsFromDisk + ShowDialog 后安全网
- [x] 主题切换丢失合并字典 → 只移除 Theme.xaml 字典
- [x] 自动滚动未应用速度倍率 → 按 ScrollSpeed 调用 LineDown 次数
- [x] _isUpdatingFromScroll 异常未重置 → try/finally 包裹
- [x] CodePagesEncodingProvider 重复注册 → 移到 App.OnStartup
- [x] BookmarkService 多实例 → 改为 App 单例共享
- [x] OnExit 空捕获 → 合并 null 检查 + 具体 InvalidOperationException
- [x] 窗口隐藏时也保存进度 → Hide 后 return，只在真正关闭时保存
- [x] 设置保存后不生效 → OnSettingsApplied 回调 + ShowDialog 后二次应用
- [x] 目录章节列表为空 → 代码构建 Tab 内容 + 直接赋值 ItemsSource
- [x] 滚轮快速下滑飞章节 → 重入保护 + 边界阈值提升 + while→if 逐章裁剪 + 缓冲增大

#### 第二轮修复（功能补全）
- [x] 书签 UI — Ctrl+Alt+M 添加、设置窗口书签 Tab 查看/跳转/删除
- [x] 段间距调节 — 设置界面滑块 + ApplyParagraphSpacing 应用
- [x] 发布测试 — FDD 2.5MB / SCD 155MB，均验证可运行
- [x] 托盘菜单打开设置 → 统一委托给 MainWindow.OpenSettingsFromTray
- [x] 新增热键 AddBookmark 自动合并到已有用户设置文件

#### 第三轮修复（全面代码审计 28 项）

**Critical:**
- [x] #1 SetWindowLong 用 int → 改为 GetWindowLongPtr/SetWindowLongPtr 64位安全
- [x] #2 鼠标钩子窗口隐藏时仍全局拦截 → MouseHookCallback 加 IsVisible 检查

**High:**
- [x] #3 ShowToast 泄漏 DispatcherTimer → 复用单个 Timer 实例
- [x] #4 ReadingPercent 整章算已读 → 改为已完成章 + 当前章滚动比例
- [x] #7 托盘菜单回调非 UI 线程 → 全部包 Dispatcher.Invoke
- [x] #9 OnExit 读已销毁 ScrollViewer → 移除，OnClosing 已保存
- [x] #27 窗口隐藏时 AutoScroll 仍运行 → Hide 前停止自动滚动

**Medium:**
- [x] #6 RemoveFirstChapter 留分隔符 → LastIndexOf 找分隔符位置裁剪
- [x] #8 事件处理器不取消订阅 → OnClosing 取消 PropertyChanged，HotkeyService Dispose 取消 SourceInitialized
- [x] #10 File.Move 非原子 → 改用 File.Replace
- [x] #12 EPUB readingOrder[i] 可为 null → 加 null check
- [x] #14 HotkeyService SourceInitialized 不取消 → 存为字段，Dispose 时取消
- [x] #16 FindResource 硬转 SolidColorBrush → TryFindResource + as 模式匹配
- [x] #17 PageDisplay 多算页数 → Ceiling((scrollable+viewport)/viewport)
- [x] #18 DragMove 空捕获 → 改为捕获 InvalidOperationException
- [x] #19 OpenFile 不保存当前进度 → LoadNovel 开头调 SaveCurrentProgress
- [x] #21 ApplyParagraphSpacing 不处理 \r\n → 先 Normalize 换行符
- [x] #26 书签 CharOffsetInChapter=0 → Bookmark 加 ScrollRatio，导航时使用
- [x] #28 短章节进度不保存 → ScrollableHeight==0 时仍定期保存

**Low:**
- [x] #5 ScrollToCharOffset 字符比例不准 → 新增 ScrollToRatio，章节跳转用比例滚动
- [x] #11 NovelId 作文件名不安全 → GetProgressPath 过滤非法字符
- [x] #13 GBK 误判为 UTF-8 → UTF-8 解码后校验 CJK 内容比例，<80% 回退 GBK
- [x] #15 首尾章节导航无反馈 → SwitchToChapter Toast 提示边界
- [x] #20 热键输入占位符覆盖 → 仅文本等于 KeyCombination 时才替换
- [x] #22 GetNovel 每次重新解析 → BookshelfService 加解析缓存
- [x] #23 Dummy HwndSource → 改用 System.Drawing.Graphics 读系统 DPI
- [x] #24 RelayCommand 不持强引用 → 改为显式字段 + 构造函数
- [x] #25 _settings 字段过时 → OnClosing 保存前先 LoadSettings 获取磁盘最新

## Validation Commands

```bash
# 构建
dotnet build src/NovelMoyo/NovelMoyo.csproj

# 运行
dotnet run --project src/NovelMoyo/NovelMoyo.csproj

# 发布
dotnet publish src/NovelMoyo/NovelMoyo.csproj -c Release -r win-x64 --self-contained
```
