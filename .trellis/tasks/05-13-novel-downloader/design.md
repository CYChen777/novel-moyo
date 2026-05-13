# Technical Design: 内置小说下载器

## 架构总览

在 NovelMoyo 现有 MVVM 架构中新增下载相关模块：

```
Models/
  BookSource.cs          # 书源配置模型
  SearchResult.cs        # 搜索结果模型
  DownloadTask.cs        # 下载任务模型
  DownloadChapter.cs     # 单章节下载状态

Services/
  BookSource/
    BookSourceService.cs       # 书源加载/保存/管理
    BookSourceParser.cs        # 解析单个书源（HTML/API 两种模式）
    SearchService.cs           # 跨书源并行搜索
  Download/
    DownloadService.cs         # 下载队列管理、并发控制
    ChapterDownloader.cs       # 单章节下载（HTTP 请求 + 内容提取）
    TxtExporter.cs             # 将下载内容导出为 .txt 并写入 bookshelf.json

ViewModels/
  OnlineBookStoreViewModel.cs  # 在线书城窗口 VM
  DownloadTaskViewModel.cs     # 单个下载任务 VM

Views/
  OnlineBookStoreWindow.xaml   # 在线书城窗口
```

## 书源格式设计

参考 Reader.exe 的 bs.json XPath 格式，扩展支持 API 模式。使用 XPath（而非 CSS 选择器），因为 Reader.exe 已验证 XPath 在小说站解析中的有效性，且 HtmlAgilityPack 原生支持。

```json
{
  "book_sources": [
    {
      "title": "书源名称",
      "enabled": true,
      "host": "https://www.example.com",
      "charset": "utf-8",

      "search": {
        "url": "https://www.example.com/search?q={keyword}",
        "method": "GET",
        "params": "",
        "result_type": "html",
        "selectors": {
          "item": "//div[@class='result-item']",
          "name": "./a[@class='name']",
          "author": "./span[@class='author']",
          "url": "./a[@class='name']/@href",
          "latest_chapter": "./span[@class='latest']"
        }
      },

      "catalog": {
        "result_type": "html",
        "selectors": {
          "item": "//div[@id='list']/dl/dd/a",
          "title": "./text()",
          "url": "./@href"
        }
      },

      "content": {
        "result_type": "html",
        "selectors": {
          "title": "//h1",
          "body": "//div[@id='content']"
        },
        "filter": {
          "type": "remove_keywords",
          "keywords": ["请收藏本站", "手机阅读"]
        }
      },

      "pagination": {
        "catalog_next": null,
        "content_next": null
      }
    },
    {
      "title": "API模式书源示例",
      "enabled": true,
      "host": "https://api.example.com",
      "charset": "utf-8",

      "search": {
        "url": "https://api.example.com/api/search?keyword={keyword}",
        "method": "GET",
        "result_type": "json",
        "json_path": {
          "item": "$.data.books[*]",
          "name": "$.book_name",
          "author": "$.author",
          "url": "$.book_url",
          "latest_chapter": "$.last_chapter"
        }
      },

      "catalog": {
        "result_type": "json",
        "json_path": {
          "item": "$.data.chapters[*]",
          "title": "$.chapter_name",
          "url": "$.chapter_url"
        }
      },

      "content": {
        "result_type": "json",
        "json_path": {
          "title": "$.data.chapter_name",
          "body": "$.data.content"
        }
      }
    }
  ]
}
```

### 关键设计说明

- **result_type**: `"html"` 用 XPath 解析 HTML，`"json"` 用 JSON Path 解析 API 响应
- **XPath** 沿用 Reader.exe 生态的 XPath 语法（HtmlAgilityPack 支持）
- **JSON Path** 使用 `$.` 前缀的简化路径语法（自行实现或用 JsonPath 库）
- **charset**: 每个书源可独立指定编码（utf-8 / gbk）
- **filter**: 正文内容过滤，去除广告文字

## 数据模型

### BookSource
```csharp
public class BookSource
{
    public string Title { get; set; }
    public bool Enabled { get; set; } = true;
    public string Host { get; set; }
    public string Charset { get; set; } = "utf-8";
    public SearchConfig Search { get; set; }
    public CatalogConfig Catalog { get; set; }
    public ContentConfig Content { get; set; }
    public PaginationConfig Pagination { get; set; }
}
```

### SearchResult
```csharp
public class SearchResult
{
    public string Title { get; set; }
    public string Author { get; set; }
    public string Url { get; set; }
    public string LatestChapter { get; set; }
    public string SourceName { get; set; }
    public BookSource Source { get; set; }
}
```

### DownloadTask
```csharp
public class DownloadTask
{
    public string Id { get; set; }
    public string Title { get; set; }
    public string Author { get; set; }
    public string BookUrl { get; set; }
    public BookSource Source { get; set; }
    public DownloadStatus Status { get; set; } // Pending/Downloading/Paused/Completed/Failed
    public int TotalChapters { get; set; }
    public int CompletedChapters { get; set; }
    public List<DownloadChapter> Chapters { get; set; }
    public string OutputPath { get; set; }
}
```

## 服务层设计

### BookSourceService
- 加载/保存 `%APPDATA%\StealthReader\sources.json`
- 提供默认书源（首次运行时创建）
- CRUD 操作（添加/删除/启用/禁用/导入/导出）

### SearchService
- 接收关键词，遍历所有 enabled 书源
- 并行请求各书源的搜索 URL
- 根据 result_type 选择 HTML 或 JSON 解析器
- 返回统一的 SearchResult 列表

### DownloadService
- 下载队列（ConcurrentQueue 或 Channel）
- 可配置并发数（SemaphoreSlim 控制，默认 3）
- 每个任务：先获取目录页 → 解析章节列表 → 逐章下载正文
- 进度回调（IProgress 或事件）
- 断点续传：已下载的章节内容缓存在内存或临时文件中

### ChapterDownloader
- 根据书源配置的 content 选择器提取正文
- HTML 模式：HtmlAgilityPack + XPath
- JSON 模式：解析 JSON 响应 + JSON Path
- 应用 filter 规则去除广告文字
- 返回纯文本正文

### TxtExporter
- 将所有章节拼接为标准 .txt 格式（与 TxtParser 兼容）
- 章节标题格式：`第X章 标题\n\n正文内容\n\n`
- 保存到 `%APPDATA%\StealthReader\novels\{书名}-{作者}.txt`
- 自动更新 bookshelf.json（调用 DataStore）

## HTTP 请求层

使用 .NET 内置 `HttpClient`，配置：
- User-Agent 模拟浏览器
- 可配置代理（复用 NovelMoyo 现有设置，如有）
- 请求超时 30s
- 重试 2 次（指数退避）
- 编码处理：根据书源 charset 配置解码响应

## 与 NovelMoyo 的集成点

1. **BookshelfWindow.xaml** — 底部按钮栏新增"在线书城"按钮
2. **OnlineBookStoreWindow** — 新窗口，包含搜索、URL 粘贴、下载管理三个区域
3. **DataStore** — 下载完成后调用 DataStore 写入 bookshelf.json
4. **App.xaml.cs** — 注册新服务（BookSourceService, SearchService, DownloadService）
5. **settings.json** — 新增下载相关设置（下载目录、并发数、默认书源路径）

## 文件存储

```
%APPDATA%\StealthReader\
├── settings.json          # 现有
├── bookshelf.json         # 现有（下载后自动更新）
├── progress/              # 现有
├── sources.json           # 新增：书源配置
├── novels/                # 新增：下载的小说文件
│   └── {书名}-{作者}.txt
└── downloads/             # 新增：下载任务状态持久化
    └── tasks.json         # 断点续传用
```

## 依赖

- **HtmlAgilityPack** — HTML 解析 + XPath（新增 NuGet 包）
- **System.Text.Json** — JSON 解析（已有）
- **System.Net.Http** — HTTP 请求（内置）
