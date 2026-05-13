using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using NovelMoyo.Models;
using NovelMoyo.Services.BookSource;
using NovelMoyo.Services.Download;

namespace NovelMoyo.ViewModels;

public class OnlineBookStoreViewModel : INotifyPropertyChanged
{
    private readonly SearchService _searchService;
    private readonly DownloadService _downloadService;

    private string _searchKeyword = string.Empty;
    private string _urlInput = string.Empty;
    private bool _isSearching;
    private string _statusMessage = string.Empty;
    private SearchResult? _selectedResult;
    private string _downloadPath = string.Empty;
    private DownloadTaskInfo? _activeDownload;

    public ObservableCollection<SearchResult> SearchResults { get; } = [];
    public ObservableCollection<DownloadTaskInfo> DownloadTasks { get; } = [];

    public string SearchKeyword
    {
        get => _searchKeyword;
        set { _searchKeyword = value; OnPropertyChanged(); }
    }

    public string UrlInput
    {
        get => _urlInput;
        set { _urlInput = value; OnPropertyChanged(); }
    }

    public bool IsSearching
    {
        get => _isSearching;
        set { _isSearching = value; OnPropertyChanged(); }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    public SearchResult? SelectedResult
    {
        get => _selectedResult;
        set
        {
            _selectedResult = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedResult));
        }
    }

    public bool HasSelectedResult => _selectedResult != null;

    public string DownloadPath
    {
        get => _downloadPath;
        set { _downloadPath = value; OnPropertyChanged(); }
    }

    public DownloadTaskInfo? ActiveDownload
    {
        get => _activeDownload;
        set { _activeDownload = value; OnPropertyChanged(); }
    }

    public ICommand SearchCommand { get; }
    public ICommand SearchByUrlCommand { get; }
    public ICommand StartDownloadCommand { get; }
    public ICommand CancelDownloadCommand { get; }
    public ICommand BrowsePathCommand { get; }

    public OnlineBookStoreViewModel(SearchService searchService, DownloadService downloadService)
    {
        _searchService = searchService;
        _downloadService = downloadService;

        // Default download path is the novels directory
        DownloadPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "StealthReader", "novels");

        SearchCommand = new AsyncRelayCommand(SearchAsync);
        SearchByUrlCommand = new AsyncRelayCommand(SearchByUrlAsync);
        StartDownloadCommand = new AsyncRelayCommand(StartDownloadAsync);
        CancelDownloadCommand = new RelayCommand<DownloadTaskInfo>(CancelDownload);
        BrowsePathCommand = new RelayCommand(BrowsePath);

        // Load existing download tasks
        foreach (var task in _downloadService.Tasks)
            DownloadTasks.Add(task);

        _downloadService.ProgressChanged += OnDownloadProgressChanged;
        _downloadService.TaskAdded += OnTaskAdded;
        _downloadService.TaskCompleted += OnTaskCompleted;
    }

    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchKeyword)) return;

        IsSearching = true;
        StatusMessage = "搜索中...";
        SearchResults.Clear();
        SelectedResult = null;

        try
        {
            var results = await _searchService.SearchAsync(SearchKeyword);
            foreach (var result in results)
            {
                SearchResults.Add(result);
            }
            StatusMessage = $"找到 {results.Count} 个结果";
        }
        catch (Exception ex)
        {
            StatusMessage = $"搜索失败: {ex.Message}";
        }
        finally
        {
            IsSearching = false;
        }
    }

    private async Task SearchByUrlAsync()
    {
        if (string.IsNullOrWhiteSpace(UrlInput)) return;

        IsSearching = true;
        StatusMessage = "解析URL...";
        SearchResults.Clear();
        SelectedResult = null;

        try
        {
            var results = await _searchService.SearchByUrlAsync(UrlInput);
            foreach (var result in results)
            {
                SearchResults.Add(result);
            }
            StatusMessage = results.Count > 0 ? "解析成功" : "无法解析该URL";
        }
        catch (Exception ex)
        {
            StatusMessage = $"解析失败: {ex.Message}";
        }
        finally
        {
            IsSearching = false;
        }
    }

    private async Task StartDownloadAsync()
    {
        if (SelectedResult == null) return;

        // Ensure download directory exists and is writable
        if (!string.IsNullOrEmpty(DownloadPath))
        {
            try
            {
                Directory.CreateDirectory(DownloadPath);
                // M23: Verify write permission
                var testFile = Path.Combine(DownloadPath, ".write_test");
                File.WriteAllText(testFile, "");
                File.Delete(testFile);
            }
            catch (Exception ex)
            {
                StatusMessage = $"下载目录不可写: {ex.Message}";
                return;
            }
        }

        StatusMessage = $"开始下载: {SelectedResult.Title}";
        var task = await _downloadService.StartDownloadAsync(SelectedResult, DownloadPath);
        ActiveDownload = task;
    }

    private void CancelDownload(DownloadTaskInfo? task)
    {
        if (task == null) return;
        // Update UI first, then cancel in service
        task.Status = DownloadStatus.Failed;
        task.ErrorMessage = "用户取消";
        _downloadService.CancelDownload(task.Id);
        DownloadTasks.Remove(task);
        if (ActiveDownload?.Id == task.Id)
            ActiveDownload = null;
        StatusMessage = $"已取消: {task.Title}";
    }

    private void BrowsePath()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择下载目录",
            InitialDirectory = DownloadPath
        };

        if (dialog.ShowDialog() == true)
        {
            DownloadPath = dialog.FolderName;
        }
    }

    private void OnTaskAdded(DownloadTaskInfo task)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            DownloadTasks.Add(task);
        });
    }

    private void OnDownloadProgressChanged(DownloadProgress progress)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            DownloadTaskInfo? task = null;
            for (int i = 0; i < DownloadTasks.Count; i++)
            {
                if (DownloadTasks[i].Id == progress.TaskId)
                {
                    task = DownloadTasks[i];
                    break;
                }
            }

            if (task == null) return;

            task.CompletedChapters = progress.CompletedChapters;
            task.TotalChapters = progress.TotalChapters;

            if (progress.IsCompleted)
            {
                task.Status = DownloadStatus.Completed;
                StatusMessage = $"下载完成: {task.Title}（{progress.TotalChapters} 章）";
                ActiveDownload = null;
                // Remove completed task from UI (H10: no blocking MessageBox)
                DownloadTasks.Remove(task);
                _downloadService.RemoveTask(task.Id);
            }
            else if (!string.IsNullOrEmpty(progress.ErrorMessage))
            {
                task.Status = DownloadStatus.Failed;
                task.ErrorMessage = progress.ErrorMessage;
                StatusMessage = $"下载失败: {task.Title}";
            }
            else if (task.Status != DownloadStatus.Paused)
            {
                task.Status = DownloadStatus.Downloading;
                StatusMessage = $"下载中: {task.Title} {progress.CompletedChapters}/{progress.TotalChapters} - {progress.CurrentChapter}";
            }
        });
    }

    private void OnTaskCompleted(string bookTitle)
    {
        // Notify bookshelf window to refresh
        Application.Current?.Dispatcher.Invoke(() =>
        {
            BookshelfRefreshRequested?.Invoke(this, EventArgs.Empty);
        });
    }

    public static event EventHandler? BookshelfRefreshRequested;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

// Async command implementations
public class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;
    private bool _isExecuting;

    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => !_isExecuting && (_canExecute?.Invoke() ?? true);

    public async void Execute(object? parameter)
    {
        if (_isExecuting) return;
        _isExecuting = true;
        CommandManager.InvalidateRequerySuggested();
        try
        {
            await _execute();
        }
        finally
        {
            _isExecuting = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }
}

public class AsyncRelayCommand<T> : ICommand
{
    private readonly Func<T?, Task> _execute;
    private readonly Predicate<T?>? _canExecute;
    private bool _isExecuting;

    public AsyncRelayCommand(Func<T?, Task> execute, Predicate<T?>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => !_isExecuting && (_canExecute?.Invoke((T?)parameter) ?? true);

    public async void Execute(object? parameter)
    {
        if (_isExecuting) return;
        _isExecuting = true;
        CommandManager.InvalidateRequerySuggested();
        try
        {
            await _execute((T?)parameter);
        }
        finally
        {
            _isExecuting = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
