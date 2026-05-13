# Changelog

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
