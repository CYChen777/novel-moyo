# feat: 内置小说下载器（可配置书源）

## Goal

在 NovelMoyo 中深度集成小说下载功能，通过可配置的书源系统，实现"搜索 → 下载 → 阅读"一站式体验。

## Background / Known Context

### 参考程序：novel_downloader.exe (D:\ttyueduqi)
- 从笔趣阁等镜像站抓取小说，SQLite 管理书架/下载队列/断点续传
- config.json 配置镜像站，实际使用 bqg192.cc（SPA 站）

### 目标阅读器：NovelMoyo (D:\projects\novel-moyo)
- .NET 8 WPF，C# 12，项目结构：Models/ Services/ ViewModels/ Views/
- 数据存储 `%APPDATA%\StealthReader\`（bookshelf.json + progress/*.json）
- .txt 章节通过 TxtParser 动态解析，支持 GBK/UTF-8/UTF-16
- 已有依赖：VersOne.Epub, System.Text.Json, Win32 P/Invoke

## Decision (ADR-lite)

**Context**: 需要选择小说网站的解析方式。很多小说站（如 bqg192.cc）是 SPA 前端，纯 HTML 解析拿不到数据。

**Decision**: 采用 **HTML + API 混合模式**（推荐方案）

**理由**：
1. SPA 站点的前端本质上是调后端 API 拿 JSON 数据，直接请求 API 更高效
2. 传统站点用 CSS 选择器解析 HTML，SPA 站配置 API 端点 + JSON Path
3. HtmlAgilityPack/.NET 生态成熟，无需 WebView2 依赖
4. 轻量、快速、可离线使用

**Consequences**：书源配置需要支持两种解析模式（css_selector / json_path），配置复杂度略高

## Requirements

### R1. 书源配置系统
- JSON 格式书源文件，存储在 `%APPDATA%\StealthReader\sources.json`
- 每个书源定义：名称、URL、搜索方式、章节列表、正文提取规则
- 支持两种解析模式：
  - `html` 模式：CSS 选择器提取（传统服务端渲染站点）
  - `api` 模式：URL 模板 + JSON Path 提取（SPA/API 站点）
- 用户可在设置界面中添加/编辑/删除/导入/导出书源

### R2. 小说搜索
- 在 NovelMoyo 中新增"在线书城"入口（书架界面或菜单）
- 用户输入书名，跨所有启用的书源并行搜索
- 搜索结果展示：书名、作者、来源站点、最新章节
- 点击结果可查看详情（简介、章节数等）

### R3. URL 粘贴下载
- 支持用户粘贴小说页面 URL，自动匹配书源并解析
- 解析后展示小说信息，确认后开始下载

### R4. 下载管理
- 后台下载，不阻塞阅读
- 下载进度显示（已下载/总章节数）
- 断点续传：下载中断后可从上次位置继续
- 并发控制：可配置同时下载的章节数（默认 3）
- 下载限速：可选，避免被站点封禁

### R5. 自动入库
- 下载完成后自动将 .txt 文件保存到用户指定目录（默认 `%APPDATA%\StealthReader\novels\`）
- 自动更新 bookshelf.json，NovelMoyo 书架中直接显示新书
- 文件命名：`{书名}-{作者}.txt`

### R6. 更新检查
- 书架中的小说可检查是否有新章节
- 可选自动检查（定时轮询）或手动检查（右键菜单）

## Acceptance Criteria

- [ ] 书源 JSON 配置可正常加载和保存
- [ ] 能通过书源搜索小说并展示结果
- [ ] 能粘贴 URL 解析小说信息
- [ ] 能下载整本小说为 .txt 文件
- [ ] 下载的 .txt 可被 NovelMoyo 正常打开和阅读
- [ ] 下载完成后自动出现在 NovelMoyo 书架中
- [ ] 下载中断后可断点续传
- [ ] 能检查书架小说的更新章节

## Out of Scope

- 不实现在线阅读（只下载，阅读用 NovelMoyo 现有功能）
- 不支持需要登录/Cookie 的站点（MVP 阶段）
- 不实现 epub 格式下载（MVP 阶段只下载 .txt）
- 不做反爬对抗（验证码、IP 轮换等）

## Definition of Done

- 功能实现并可正常运行
- 不破坏 NovelMoyo 现有数据和功能
- 代码遵循 NovelMoyo 现有架构模式（MVVM）
- 书源配置文件附带至少 2 个可用示例书源

## UI Integration

- 书架窗口底部按钮栏新增"在线书城"按钮（位于"导入"按钮左侧）
- 点击后弹出独立的 OnlineBookStoreWindow 窗口
- 窗口内包含：搜索框 + 搜索结果列表 + URL 粘贴输入框 + 下载管理面板
- 下载完成后自动刷新书架列表

## Research References

*（待添加：书源配置格式设计、目标站点 API 分析）*
