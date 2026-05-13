using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace NovelMoyo.Models;

public enum DownloadStatus
{
    Pending,
    Downloading,
    Paused,
    Completed,
    Failed
}

public class DownloadTaskInfo : INotifyPropertyChanged
{
    private string _id = Guid.NewGuid().ToString("N")[..8];
    private string _title = string.Empty;
    private string _author = string.Empty;
    private string _bookUrl = string.Empty;
    private string _sourceName = string.Empty;
    private DownloadStatus _status = DownloadStatus.Pending;
    private int _totalChapters;
    private int _completedChapters;
    private List<DownloadChapter> _chapters = [];
    private string _outputPath = string.Empty;
    private DateTime _createdAt = DateTime.Now;
    private string? _errorMessage;

    public string Id { get => _id; set { _id = value; OnPropertyChanged(); } }
    public string Title { get => _title; set { _title = value; OnPropertyChanged(); } }
    public string Author { get => _author; set { _author = value; OnPropertyChanged(); } }
    public string BookUrl { get => _bookUrl; set { _bookUrl = value; OnPropertyChanged(); } }
    public string SourceName { get => _sourceName; set { _sourceName = value; OnPropertyChanged(); } }

    public DownloadStatus Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); }
    }

    public string StatusText => _status switch
    {
        DownloadStatus.Pending => "等待中",
        DownloadStatus.Downloading => "下载中",
        DownloadStatus.Paused => "已暂停",
        DownloadStatus.Completed => "已完成",
        DownloadStatus.Failed => string.IsNullOrEmpty(ErrorMessage) ? "失败" : $"失败: {ErrorMessage}",
        _ => ""
    };

    public int TotalChapters
    {
        get => _totalChapters;
        set { _totalChapters = value; OnPropertyChanged(); OnPropertyChanged(nameof(ProgressText)); }
    }

    public int CompletedChapters
    {
        get => _completedChapters;
        set { _completedChapters = value; OnPropertyChanged(); OnPropertyChanged(nameof(ProgressText)); }
    }

    public string ProgressText => TotalChapters > 0 ? $"{CompletedChapters}/{TotalChapters}" : "--";

    public List<DownloadChapter> Chapters { get => _chapters; set { _chapters = value; OnPropertyChanged(); } }
    public string OutputPath { get => _outputPath; set { _outputPath = value; OnPropertyChanged(); } }
    public DateTime CreatedAt { get => _createdAt; set { _createdAt = value; OnPropertyChanged(); } }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set { _errorMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

public class DownloadChapter
{
    public int Index { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    [JsonIgnore] // M16: Don't persist chapter content to tasks.json
    public string Content { get; set; } = string.Empty;
    public bool IsDownloaded { get; set; }
}
