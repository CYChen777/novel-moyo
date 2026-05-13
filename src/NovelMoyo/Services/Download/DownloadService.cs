using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using NovelMoyo.Models;
using NovelMoyo.Services.BookSource;

namespace NovelMoyo.Services.Download;

public class DownloadProgress
{
    public string TaskId { get; set; } = string.Empty;
    public int CompletedChapters { get; set; }
    public int TotalChapters { get; set; }
    public string CurrentChapter { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public string? ErrorMessage { get; set; }
}

public class DownloadService : IDisposable
{
    private static readonly string AppDataRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "StealthReader");

    private static readonly string DownloadsDir = Path.Combine(AppDataRoot, "downloads");
    private static readonly string TasksPath = Path.Combine(DownloadsDir, "tasks.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly SearchService _searchService;
    private readonly BookSourceParser _parser;
    private readonly HttpClient _httpClient;
    private readonly SocketsHttpHandler _handler;
    private readonly DataStore _dataStore;
    private readonly ObservableCollection<DownloadTaskInfo> _tasks = [];
    private readonly object _tasksLock = new();
    private readonly SemaphoreSlim _chapterSemaphore = new(10, 10);
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _taskCancellations = new();
    private readonly ConcurrentDictionary<string, Task> _activeDownloads = new();

    public ReadOnlyObservableCollection<DownloadTaskInfo> Tasks { get; }
    public event Action<DownloadProgress>? ProgressChanged;
    public event Action<DownloadTaskInfo>? TaskAdded;
    public event Action<string>? TaskCompleted;

    public DownloadService(SearchService searchService, DataStore dataStore)
    {
        _searchService = searchService;
        _handler = new SocketsHttpHandler
        {
            ConnectTimeout = TimeSpan.FromSeconds(15),
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            SslOptions = new System.Net.Security.SslClientAuthenticationOptions
            {
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
            }
        };
        _httpClient = new HttpClient(_handler)
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
        _httpClient.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        _parser = new BookSourceParser(_httpClient);
        _dataStore = dataStore;
        Tasks = new ReadOnlyObservableCollection<DownloadTaskInfo>(_tasks);

        Directory.CreateDirectory(DownloadsDir);
        LoadTasks();
    }

    public Task<DownloadTaskInfo> StartDownloadAsync(SearchResult result, string? downloadPath = null)
    {
        var outputDir = !string.IsNullOrEmpty(downloadPath)
            ? downloadPath
            : Path.Combine(AppDataRoot, "novels");

        Directory.CreateDirectory(outputDir);

        var outputPath = Path.Combine(outputDir, $"{SanitizeFileName(result.Title)}-{SanitizeFileName(result.Author)}.txt");

        // If file already exists, append a numeric suffix (H9)
        if (File.Exists(outputPath))
        {
            var baseName = Path.GetFileNameWithoutExtension(outputPath);
            var dir = Path.GetDirectoryName(outputPath)!;
            int suffix = 2;
            while (File.Exists(Path.Combine(dir, $"{baseName}({suffix}).txt")))
                suffix++;
            outputPath = Path.Combine(dir, $"{baseName}({suffix}).txt");
        }

        var task = new DownloadTaskInfo
        {
            Title = result.Title,
            Author = result.Author,
            BookUrl = result.Url,
            SourceName = result.SourceName,
            Status = DownloadStatus.Pending,
            OutputPath = outputPath
        };

        var taskCts = new CancellationTokenSource();
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, taskCts.Token);
        _taskCancellations[task.Id] = taskCts;

        lock (_tasksLock)
        {
            _tasks.Add(task);
        }
        SaveTasks();
        TaskAdded?.Invoke(task);

        var downloadTask = Task.Run(async () =>
        {
            try
            {
                await DownloadBookAsync(task, result.Source, linkedCts.Token);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DownloadBookAsync unhandled: {ex}");
                task.Status = DownloadStatus.Failed;
                task.ErrorMessage = $"内部错误: {ex.Message}";
                SaveTasks();
                ProgressChanged?.Invoke(new DownloadProgress
                {
                    TaskId = task.Id,
                    IsCompleted = false,
                    ErrorMessage = task.ErrorMessage
                });
            }
            finally
            {
                _activeDownloads.TryRemove(task.Id, out _);
                _taskCancellations.TryRemove(task.Id, out _);
                linkedCts.Dispose();
            }
        });

        _activeDownloads[task.Id] = downloadTask;

        return Task.FromResult(task);
    }

    public void CancelDownload(string taskId)
    {
        lock (_tasksLock)
        {
            var task = _tasks.FirstOrDefault(t => t.Id == taskId);
            if (task != null)
            {
                task.Status = DownloadStatus.Failed;
                task.ErrorMessage = "用户取消";
            }
        }

        // Cancel the token; the download task will clean itself up
        if (_taskCancellations.TryRemove(taskId, out var taskCts))
            taskCts.Cancel();

        SaveTasks();
    }

    public void RemoveTask(string taskId)
    {
        var task = _tasks.FirstOrDefault(t => t.Id == taskId);
        if (task != null)
        {
            _tasks.Remove(task);
            SaveTasks();
        }
    }

    private async Task DownloadBookAsync(DownloadTaskInfo task, Models.BookSource source, CancellationToken ct)
    {
        try
        {
            task.Status = DownloadStatus.Downloading;
            SaveTasks();

            // Step 1: Get catalog
            List<ChapterInfo> chapters;
            try
            {
                chapters = await _parser.GetCatalogAsync(source, task.BookUrl);
            }
            catch (Exception ex)
            {
                task.Status = DownloadStatus.Failed;
                task.ErrorMessage = $"获取目录失败: {ex.Message}";
                SaveTasks();
                ProgressChanged?.Invoke(new DownloadProgress
                {
                    TaskId = task.Id,
                    IsCompleted = false,
                    ErrorMessage = task.ErrorMessage
                });
                return;
            }

            if (chapters.Count == 0)
            {
                task.Status = DownloadStatus.Failed;
                task.ErrorMessage = "章节目录为空";
                SaveTasks();
                ProgressChanged?.Invoke(new DownloadProgress
                {
                    TaskId = task.Id,
                    IsCompleted = false,
                    ErrorMessage = "章节目录为空"
                });
                return;
            }

            task.TotalChapters = chapters.Count;
            task.Chapters = chapters.Select(c => new DownloadChapter
            {
                Index = c.Index,
                Title = c.Title,
                Url = c.Url
            }).ToList();
            SaveTasks();

            ProgressChanged?.Invoke(new DownloadProgress
            {
                TaskId = task.Id,
                CompletedChapters = 0,
                TotalChapters = task.TotalChapters,
                CurrentChapter = "准备下载..."
            });

            // Step 2: Download chapters in parallel (C4: uses _chapterSemaphore, not _outerSemaphore)
            var downloadedContent = new ConcurrentBag<(int index, string title, string content)>();
            int completedCount = 0;

            var downloadTasks = chapters.Select((chapter, i) => Task.Run(async () =>
            {
                if (ct.IsCancellationRequested) return;

                await _chapterSemaphore.WaitAsync(ct);
                try
                {
                    var downloadChapter = task.Chapters[i];
                    var contentUrl = string.IsNullOrEmpty(source.Content.UrlTemplate)
                        ? chapter.Url
                        : task.BookUrl;
                    var (title, content) = await _parser.GetContentAsync(source, contentUrl, i + 1);

                    if (string.IsNullOrWhiteSpace(content))
                    {
                        downloadChapter.Content = "[内容为空]";
                        downloadChapter.IsDownloaded = false;
                    }
                    else
                    {
                        downloadChapter.Content = content;
                        downloadChapter.IsDownloaded = true;
                        downloadChapter.Title = !string.IsNullOrEmpty(title) ? title : chapter.Title;
                        downloadedContent.Add((i, downloadChapter.Title, content));
                    }

                    var currentCompleted = Interlocked.Increment(ref completedCount);
                    task.CompletedChapters = currentCompleted;

                    if (currentCompleted % 10 == 0)
                        SaveTasks();

                    ProgressChanged?.Invoke(new DownloadProgress
                    {
                        TaskId = task.Id,
                        CompletedChapters = currentCompleted,
                        TotalChapters = task.TotalChapters,
                        CurrentChapter = downloadChapter.Title
                    });
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    var downloadChapter = task.Chapters[i];
                    System.Diagnostics.Debug.WriteLine($"[DownloadService] 章节下载失败: {downloadChapter.Title} - {ex.Message}");
                    downloadChapter.Content = $"[下载失败: {ex.Message}]";
                    downloadChapter.IsDownloaded = false;

                    var currentCompleted = Interlocked.Increment(ref completedCount);
                    task.CompletedChapters = currentCompleted;

                    ProgressChanged?.Invoke(new DownloadProgress
                    {
                        TaskId = task.Id,
                        CompletedChapters = currentCompleted,
                        TotalChapters = task.TotalChapters,
                        CurrentChapter = $"失败: {downloadChapter.Title}"
                    });
                }
                finally
                {
                    _chapterSemaphore.Release();
                }
            })).ToArray();

            await Task.WhenAll(downloadTasks);

            if (ct.IsCancellationRequested) return;

            // Step 3: Export to txt
            if (downloadedContent.IsEmpty)
            {
                task.Status = DownloadStatus.Failed;
                task.ErrorMessage = "所有章节下载失败";
                SaveTasks();
                ProgressChanged?.Invoke(new DownloadProgress
                {
                    TaskId = task.Id,
                    IsCompleted = false,
                    ErrorMessage = "所有章节下载失败"
                });
                return;
            }

            var sortedContent = downloadedContent.OrderBy(x => x.index).Select(x => (x.title, x.content)).ToList();
            ExportToTxt(task, sortedContent);

            // Step 4: Add to bookshelf
            AddToBookshelf(task);

            task.Status = DownloadStatus.Completed;
            SaveTasks();

            ProgressChanged?.Invoke(new DownloadProgress
            {
                TaskId = task.Id,
                CompletedChapters = task.CompletedChapters,
                TotalChapters = task.TotalChapters,
                CurrentChapter = task.Title,
                IsCompleted = true
            });

            TaskCompleted?.Invoke(task.Title);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DownloadService] 下载任务失败: {task.Title} - {ex.Message}");
            task.Status = DownloadStatus.Failed;
            task.ErrorMessage = $"内部错误: {ex.Message}";
            SaveTasks();

            ProgressChanged?.Invoke(new DownloadProgress
            {
                TaskId = task.Id,
                IsCompleted = false,
                ErrorMessage = ex.Message
            });
        }
    }

    private void ExportToTxt(DownloadTaskInfo task, List<(string title, string content)> chapters)
    {
        var sb = new StringBuilder();

        foreach (var (title, content) in chapters)
        {
            sb.AppendLine(title);
            sb.AppendLine();
            sb.AppendLine(content);
            sb.AppendLine();
        }

        File.WriteAllText(task.OutputPath, sb.ToString(), Encoding.UTF8);
    }

    private void AddToBookshelf(DownloadTaskInfo task)
    {
        var bookshelf = _dataStore.LoadBookshelf();
        var existing = bookshelf.FirstOrDefault(e =>
            string.Equals(e.FilePath, task.OutputPath, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            existing.LastReadAt = DateTime.Now;
        }
        else
        {
            bookshelf.Add(new BookshelfEntry
            {
                NovelId = Guid.NewGuid().ToString("N")[..8],
                Title = task.Title,
                FilePath = task.OutputPath,
                Format = "txt",
                AddedAt = DateTime.Now,
                LastReadAt = DateTime.Now
            });
        }

        _dataStore.SaveBookshelf(bookshelf);
    }

    private void LoadTasks()
    {
        if (!File.Exists(TasksPath)) return;

        try
        {
            var json = File.ReadAllText(TasksPath);
            var tasks = JsonSerializer.Deserialize<List<DownloadTaskInfo>>(json, JsonOptions) ?? [];
            // Only load completed/failed tasks, discard stale downloading/pending ones
            foreach (var task in tasks.Where(t => t.Status is DownloadStatus.Completed or DownloadStatus.Failed))
            {
                _tasks.Add(task);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LoadTasks failed: {ex.Message}");
        }
    }

    private void SaveTasks()
    {
        try
        {
            List<DownloadTaskInfo> snapshot;
            lock (_tasksLock)
            {
                snapshot = _tasks.ToList();
            }
            var json = JsonSerializer.Serialize(snapshot, JsonOptions);
            File.WriteAllText(TasksPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SaveTasks failed: {ex.Message}");
        }
    }

    private static string SanitizeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.All(c => Path.GetInvalidFileNameChars().Contains(c)))
            return "未知";
        var invalidChars = Path.GetInvalidFileNameChars();
        return new string(name.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray());
    }

    public void Dispose()
    {
        _cts.Cancel();

        // C2: Wait for all active downloads to complete before disposing
        var activeTasks = _activeDownloads.Values.ToArray();
        if (activeTasks.Length > 0)
        {
            try
            {
                Task.WaitAll(activeTasks, TimeSpan.FromSeconds(10));
            }
            catch (AggregateException) { }
        }

        _cts.Dispose();
        _chapterSemaphore.Dispose();
        _httpClient.Dispose();
        _handler.Dispose();

        foreach (var cts in _taskCancellations.Values)
            cts.Dispose();
    }
}
