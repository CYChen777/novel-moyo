# Changelog

## v1.0.3 (2026-05-22)

### Bug Fixes (17 项)

**Critical (4) — 滚轮跳章根本修复**
- 字符→像素线性假设导致分隔符区域定位偏移 — 新增 `_chapterPixelOffsets[]` 数组和 `MeasureChapterHeights()` 方法，用临时 TextBlock 实测每章渲染高度。`UpdateCurrentChapterFromScroll`、`TrimOldChapters`、`ComputeChapterRelativeRatio`、`ScrollToChapterRatio` 全部改用像素偏移替代字符偏移，消除章节分隔符高度误差。窗口 resize 时自动重测
- AppendNextChapter 后无同步布局 — 改预测量方式：文本修改前通过临时 TextBlock 测量新章节高度，直接追加到 `_chapterPixelOffsets`，不再依赖 UpdateLayout+ScrollableHeight 差值
- ScrollChanged 异步触发时保护标志已清除 — 新增 `_needsDeferredScrollUpdate` 标志，文本修改后第一次异步 ScrollChanged 检查并消费，防止在布局未同步时重复触发 Trim/Append
- 快速连续滚轮时 Trim 累积误差 — 像素偏移方案从根本消除每帧像素偏差；RemoveFirstChapter 使用 `_chapterPixelOffsets[1]` 实测值而非 ScrollableHeight 差值

**High (4)**
- 2 秒冷却期不够导致渐进加载干扰初始恢复 — AppendNextChapter / PrependPreviousChapter 末尾重置 `_contentLoadTime`
- 快捷键切章 + ScrollChanged 竞争 — CurrentChapter 变更时确保 MeasureChapterHeights 在 ScrollToChapterRatio 前已调用
- _pendingScrollRatio 未消费时 CompositionTarget.Rendering 事件泄漏 — OnClosing 中清理 pending scroll restore 事件
- nearBottom/nearTop 阈值在短章节时太敏感 — 增加像素绝对值阈值（500px）双重判定

**Medium (3)**
- ParagraphSpacing 变更 BuildFullBookContent 丢失滚动位置 — 重建前保存并恢复 VerticalOffset
- HandleMouseWheel 对高 delta 鼠标放大过度 — Math.Clamp(delta, -1000, 1000) 限制输入
- LoadNovel 切书时 scrollOffset=0 写入进度 — 移除冗余 SaveCurrentProgressWithScroll(0, ...) 调用

**Low (4)**
- ChapterSeparator static readonly → const
- 鼠标钩子可能重复安装 — InstallMouseHook 增加重复防护
- 窗口隐藏时不保存进度 — OnClosing 隐藏分支新增 SaveCurrentProgress
- ApplyParagraphSpacing 小值无效 — Round(spacing/4)=0 修复为 Math.Max(1, spacing/2)

### Internal

- `_chapterPixelOffsets[]` + `MeasureChapterHeights()` — 基于实测像素的章节↔滚动位置映射，替代字符偏移线性估算
- `_needsDeferredScrollUpdate` — 文本修改后异步 ScrollChanged 保护标志
- `SizeChanged` 事件处理器 — 窗口大小变化时重测像素偏移并更新进度

---

### Bug Fixes (12 项)

**Critical (1)**
- 滚轮跳章 — 鼠标滚轮快速向下滚动时，触底加载会以「整本缓冲的相对比例」错位恢复滚动，每次 Trim 就跳约 1 章，连续滚导致瞬间跳几章；改为同步 `UpdateLayout()` + 按真实像素差还原，并调换 `TrimOldChapters` / `Append-Prepend` 顺序保证读到一致的缓冲状态

**High (3)**
- 跨次启动跳章 — `ReadingProgress.ChapterScrollRatio` 之前实际保存的是「整个加载窗口的 ratio」，下次启动加载到同一章但缓冲窗口不同就会落到 X+2 章；新增 `ComputeChapterRelativeRatio` / `ScrollToChapterRatio` 改用章内 ratio 持久化
- OnlineBookStoreViewModel 事件泄漏 — `_downloadService` 是单例，VM 订阅的 ProgressChanged/TaskAdded/TaskCompleted 在窗口关闭时未解绑；VM 实现 IDisposable，`OnlineBookStoreWindow.Closed` 触发 Dispose
- DownloadService taskCts 资源泄漏 — 任务结束时 `TryRemove(out _)` 丢弃了 `CancellationTokenSource`；改为 `TryRemove(out var cts); cts.Dispose()`，`CancelDownload` 改用 `TryGetValue` 让 finally 统一负责释放

**Medium (6)**
- TxtParser 章节标题混入正文首行 — `contentStart` 从匹配末尾改为标题行换行后，正文不再重复一次标题文字
- ReadingPercent 空引用判定失效 — `_currentNovel?.Chapters.Count == 0` 永远为 `false`，改为显式 null-check
- OpenFile 命令切书丢精确进度 — 新增 `OnLoadNovelRequested` 事件，宿主窗口先 `SaveCurrentProgress()` 再 `LoadNovel`
- DownloadService / BookSourceService 非原子写 — 提取 `DataStore.WriteAtomically(path, content)` 共用，崩溃时不再留下截断的 `tasks.json` / `sources.json`
- DownloadService.RemoveTask 未加锁 — 用 `_tasksLock` 包住 `_tasks` 操作，与其它读写一致
- 自动滚动到全书末尾不停 — `OnScrollTick` 检测到 `_loadedChapterEnd >= chapters.Count - 1` 且已触底时主动 Toggle 关闭定时器并 Toast「已到达末尾」

**Low (2)**
- 删除 `MainViewModel.OnScrollTick` 死代码（无人订阅的转发事件，泄漏闭包）
- `MainWindow.RegisterHotkeys` 字典里两个 entry 挤一行的排版

### Internal

- `DataStore.WriteJsonAtomically<T>` 拆出 `internal static WriteAtomically(string path, string content)`，可被同程序集的其它服务复用
- `MainWindow` 新增 `ComputeChapterRelativeRatio()` / `ScrollToChapterRatio(idx, ratio)`，统一进度持久化与恢复的换算
- `OnLoadNovelRequested` 事件作为 OpenFile 命令与宿主之间的解耦点

---

## v1.0.1 (2026-05-13)

### New Features

- **内置小说下载器** — 书架窗口新增"在线书城"入口，支持搜索小说、在线下载、自动入库
- **多书源搜索** — 支持同时从多个书源并行搜索，自动去重，章节数即时加载
- **URL 直接解析** — 粘贴小说链接直接识别书源并下载
- **下载管理** — 支持取消下载、下载进度实时显示
- **自动入库** — 下载完成的小说自动添加到书架，无需手动导入
- **断点续传** — 下载任务状态持久化，重启后可继续

### Bug Fixes (24 项)

**Critical (4)**
- HttpClient 资源泄漏：SearchService/DownloadService 实现 IDisposable
- Dispose 等待活跃下载任务完成再释放资源
- ObservableCollection 线程安全保护
- 分离 _chapterSemaphore，避免信号量损坏

**High (8)**
- 搜索去重 key 规范化（ToLowerInvariant + Trim）
- HttpClient.Timeout 改为 InfiniteTimeSpan
- Fire-and-forget 改为跟踪 Task + 顶层异常捕获
- 文件已存在时自动加数字后缀避免覆盖
- 移除阻塞式 MessageBox，改为状态栏通知
- CancelDownload 先更新 UI 状态再取消服务
- 书架刷新回调加 Dispatcher.CheckAccess 保护

**Medium (12)**
- JSON 解析异常加 Debug.WriteLine 日志
- 章节数获取改为 Task.WhenAll 并行
- BookSourceService 加 lock 保护
- DownloadChapter.Content 加 [JsonIgnore] 不持久化大文本
- SanitizeFileName 空字符串兜底
- RefreshList 先构建数据再批量更新集合
- App.OnExit 使用内存 _settings 而非重新加载
- 拖放支持多个文件同时导入
- 搜索可重入由 _isExecuting 保护
- 空 catch 块加日志输出
- 下载前验证目录写入权限
- SearchResult 实现 INotifyPropertyChanged

### Infrastructure

- 新增 HtmlAgilityPack 依赖（HTML 解析）
- 新增 3 个冗余书源域名（容错）
- SocketsHttpHandler + 显式 TLS 1.2/1.3 支持

---

## v1.0.0 (2026-05-12)

### Initial Release

- 透明无边框阅读窗口
- 鼠标悬停显隐 + 锁定模式
- 鼠标穿透 + 置顶显示
- 自动滚动（5 档速度）
- 章节导航（上一章/下一章）
- 支持 .txt（GBK/UTF-8/UTF-16）和 .epub 格式
- 书架管理 + 独立阅读进度
- 书签功能（多书签 + 备注）
- 全局快捷键（可自定义）
- 系统托盘 + 右键菜单
- 设置界面（外观/阅读/快捷键）
- Inno Setup 安装包
