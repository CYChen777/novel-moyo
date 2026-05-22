using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Win32;
using NovelMoyo.Models;
using NovelMoyo.Services;
using NovelMoyo.Services.NovelParser;

namespace NovelMoyo.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly DataStore _dataStore;
    private readonly BookshelfService _bookshelfService;
    private readonly BookmarkService _bookmarkService;
    private readonly AutoScrollService _autoScrollService;
    private readonly NovelParserFactory _parserFactory;

    /// <summary>
    /// Exposes the AutoScrollService for the MainWindow to wire up scroll ticks.
    /// </summary>
    public AutoScrollService AutoScrollServiceInstance => _autoScrollService;

    private Novel? _currentNovel;
    private Chapter? _currentChapter;
    private ReadingProgress? _currentProgress;
    private int _currentChapterIndex;

    // State
    private bool _isVisible;
    private bool _isLocked;
    private bool _isPassthrough;
    private bool _isTopmost = true;
    private bool _isAutoScrolling;

    // Appearance
    private double _textOpacity = 0.3;
    private string _backgroundColor = "#000000";
    private string _fontColor = "#FFFFFF";
    private int _fontSize = 16;
    private double _lineSpacing = 1.5;
    private double _paragraphSpacing = 8;
    private double _scrollSpeed = 1.0;

    public MainViewModel(
        DataStore dataStore,
        BookshelfService bookshelfService,
        BookmarkService bookmarkService,
        AutoScrollService autoScrollService,
        NovelParserFactory parserFactory)
    {
        _dataStore = dataStore;
        _bookshelfService = bookshelfService;
        _bookmarkService = bookmarkService;
        _autoScrollService = autoScrollService;
        _parserFactory = parserFactory;

        Chapters = [];

        ToggleVisibilityCommand = new RelayCommand(() => IsVisible = !IsVisible);
        ToggleLockCommand = new RelayCommand(() => IsLocked = !IsLocked);
        TogglePassthroughCommand = new RelayCommand(() => IsPassthrough = !IsPassthrough);
        ToggleTopmostCommand = new RelayCommand(() => IsTopmost = !IsTopmost);
        ToggleAutoScrollCommand = new RelayCommand(ToggleAutoScroll);
        SpeedUpCommand = new RelayCommand(() => _autoScrollService.SpeedUp());
        SpeedDownCommand = new RelayCommand(() => _autoScrollService.SpeedDown());
        PrevChapterCommand = new RelayCommand(() => SwitchToChapter(_currentChapterIndex - 1));
        NextChapterCommand = new RelayCommand(() => SwitchToChapter(_currentChapterIndex + 1));
        OpenFileCommand = new RelayCommand(OpenFile);
        AddBookmarkCommand = new RelayCommand(AddBookmark);

        LoadSettings();
    }

    public ObservableCollection<Chapter> Chapters { get; }

    // Commands
    public ICommand ToggleVisibilityCommand { get; }
    public ICommand ToggleLockCommand { get; }
    public ICommand TogglePassthroughCommand { get; }
    public ICommand ToggleTopmostCommand { get; }
    public ICommand ToggleAutoScrollCommand { get; }
    public ICommand SpeedUpCommand { get; }
    public ICommand SpeedDownCommand { get; }
    public ICommand PrevChapterCommand { get; }
    public ICommand NextChapterCommand { get; }
    public ICommand OpenFileCommand { get; }
    public ICommand AddBookmarkCommand { get; }

    // Events
    public event Action<string>? OnBookmarkAdded;
    public event Action<string>? OnToastRequested;
    /// <summary>
    /// Raised when the user picks a file via OpenFileCommand. Subscribed by MainWindow
    /// so it can flush the live scroll position via SaveCurrentProgress() before LoadNovel
    /// runs and replaces the in-memory novel/progress state.
    /// </summary>
    public event Action<string>? OnLoadNovelRequested;

    // Properties
    public Novel? CurrentNovel
    {
        get => _currentNovel;
        set { _currentNovel = value; OnPropertyChanged(); }
    }

    public Chapter? CurrentChapter
    {
        get => _currentChapter;
        set { _currentChapter = value; OnPropertyChanged(); OnPropertyChanged(nameof(ChapterTitle)); }
    }

    public string ChapterTitle => _currentChapter?.Title ?? "未打开文件";

    /// <summary>
    /// Chapter info string for the status bar right side, e.g. "第3章 / 共100章 45%"
    /// </summary>
    public string ChapterDisplay
    {
        get
        {
            if (_currentNovel is null || _currentNovel.Chapters.Count == 0)
                return "未打开文件";
            var percent = ReadingPercent;
            return $"第{_currentChapterIndex + 1}章 / 共{_currentNovel.Chapters.Count}章  {percent:F0}%";
        }
    }

    private double _viewerHeight;
    private double _scrollableHeight;
    private double _scrollOffset;

    /// <summary>
    /// Page display string for the status bar, e.g. "第3页 / 共100页"
    /// </summary>
    public string PageDisplay
    {
        get
        {
            if (_viewerHeight <= 0) return "第1页 / 共1页";
            var totalPages = Math.Max(1, (int)Math.Ceiling((_scrollableHeight + _viewerHeight) / _viewerHeight));
            var currentPage = Math.Clamp((int)(_scrollOffset / _viewerHeight) + 1, 1, totalPages);
            return $"第{currentPage}页 / 共{totalPages}页";
        }
    }

    public void UpdateScrollState(double viewerHeight, double scrollableHeight, double scrollOffset)
    {
        _viewerHeight = viewerHeight;
        _scrollableHeight = scrollableHeight;
        _scrollOffset = scrollOffset;
        OnPropertyChanged(nameof(PageDisplay));
    }

    public double ReadingPercent
    {
        get
        {
            if (_currentNovel is null || _currentNovel.Chapters.Count == 0) return 0;
            var totalChars = _currentNovel.Chapters.Sum(c => c.Content.Length);
            if (totalChars <= 0) return 0;
            // Fully read chapters (before current) + proportional fraction of current chapter
            var readChars = _currentNovel.Chapters.Take(_currentChapterIndex).Sum(c => c.Content.Length);
            var currentChapterChars = _currentNovel.Chapters.ElementAtOrDefault(_currentChapterIndex)?.Content.Length ?? 0;
            var chapterRatio = _currentProgress?.ChapterScrollRatio ?? 0;
            readChars += (int)(currentChapterChars * chapterRatio);
            return Math.Min(100, readChars * 100.0 / totalChars);
        }
    }

    public bool IsVisible
    {
        get => _isVisible;
        set { _isVisible = value; OnPropertyChanged(); }
    }

    public bool IsLocked
    {
        get => _isLocked;
        set { _isLocked = value; OnPropertyChanged(); }
    }

    public bool IsPassthrough
    {
        get => _isPassthrough;
        set { _isPassthrough = value; OnPropertyChanged(); }
    }

    public bool IsTopmost
    {
        get => _isTopmost;
        set { _isTopmost = value; OnPropertyChanged(); }
    }

    public bool IsAutoScrolling
    {
        get => _isAutoScrolling;
        set { _isAutoScrolling = value; OnPropertyChanged(); }
    }

    public int AutoScrollSpeed
    {
        get => _autoScrollService.SpeedLevel;
        set { _autoScrollService.SpeedLevel = value; OnPropertyChanged(); }
    }

    public double TextOpacity
    {
        get => _textOpacity;
        set { _textOpacity = value; OnPropertyChanged(); }
    }

    public string BackgroundColor
    {
        get => _backgroundColor;
        set { _backgroundColor = value; OnPropertyChanged(); }
    }

    public string FontColor
    {
        get => _fontColor;
        set { _fontColor = value; OnPropertyChanged(); }
    }

    public int FontSize
    {
        get => _fontSize;
        set { _fontSize = value; OnPropertyChanged(); }
    }

    public double LineSpacing
    {
        get => _lineSpacing;
        set { _lineSpacing = value; OnPropertyChanged(); }
    }

    public double ParagraphSpacing
    {
        get => _paragraphSpacing;
        set { _paragraphSpacing = value; OnPropertyChanged(); }
    }

    public double ScrollSpeed
    {
        get => _scrollSpeed;
        set { _scrollSpeed = Math.Clamp(value, 0.5, 4.5); OnPropertyChanged(); }
    }

    /// <summary>
    /// Saves current reading progress with the latest scroll position from the UI.
    /// Call this before switching novels or closing to ensure accurate position.
    /// </summary>
    public void SaveCurrentProgressWithScroll(int scrollOffset, double chapterScrollRatio)
    {
        if (_currentNovel is null || _currentProgress is null) return;
        _currentProgress.ChapterIndex = _currentChapterIndex;
        _currentProgress.TotalChapters = _currentNovel.Chapters.Count;
        _currentProgress.ScrollOffset = scrollOffset;
        _currentProgress.ChapterScrollRatio = chapterScrollRatio;
        _currentProgress.UpdatedAt = DateTime.Now;
        _bookmarkService.SaveProgressWithBookmarks(_currentProgress);
    }

    public void LoadNovel(string filePath)
    {
        try
        {
            // NOTE: Callers must call MainWindow.SaveCurrentProgress() before LoadNovel
            // to persist the live scroll offset. We intentionally do NOT save here because
            // we don't have access to the real scroll offset at this point.

            var parser = _parserFactory.GetParser(filePath);
            var novel = parser.Parse(filePath);

            // Add to bookshelf
            _bookshelfService.AddNovel(novel);

            CurrentNovel = novel;
            Chapters.Clear();
            foreach (var ch in novel.Chapters)
                Chapters.Add(ch);

            // Restore reading progress
            _currentProgress = _bookmarkService.LoadProgress(novel.Id);
            _currentChapterIndex = _currentProgress?.ChapterIndex ?? 0;
            if (_currentChapterIndex >= Chapters.Count)
                _currentChapterIndex = 0;
            SavedScrollRatio = _currentProgress?.ChapterScrollRatio ?? 0;
            SwitchToChapter(_currentChapterIndex);

            // Ensure progress file exists with TotalChapters
            _currentProgress ??= new ReadingProgress { NovelId = novel.Id };
            _currentProgress.TotalChapters = novel.Chapters.Count;
            _currentProgress.ChapterIndex = _currentChapterIndex;
            _bookmarkService.SaveProgressWithBookmarks(_currentProgress);

            // Persist last novel ID immediately
            var settings = _dataStore.LoadSettings();
            settings.LastNovelId = novel.Id;
            _dataStore.SaveSettings(settings);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"无法打开文件: {ex.Message}", "错误",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    public void SwitchToChapter(int index)
    {
        if (CurrentNovel is null) return;
        if (index < 0)
        {
            OnToastRequested?.Invoke("已经是第一章了");
            return;
        }
        if (index >= CurrentNovel.Chapters.Count)
        {
            OnToastRequested?.Invoke("已经是最后一章了");
            return;
        }

        _currentChapterIndex = index;
        CurrentChapter = CurrentNovel.Chapters[index];
        OnPropertyChanged(nameof(ReadingPercent));
        OnPropertyChanged(nameof(ChapterDisplay));
    }

    public void RestoreProgress(string novelId)
    {
        _currentProgress = _bookmarkService.LoadProgress(novelId);
        if (_currentProgress is not null)
        {
            _currentChapterIndex = _currentProgress.ChapterIndex;
            SavedScrollRatio = _currentProgress.ChapterScrollRatio;
        }
    }

    /// <summary>
    /// Saved scroll ratio (0.0-1.0) within the current chapter, from last session.
    /// </summary>
    public double SavedScrollRatio { get; set; }

    /// <summary>
    /// Whether a scroll position needs to be restored after the window is fully loaded.
    /// Set to true when a novel is loaded before the window is shown (e.g. on startup).
    /// </summary>
    public bool PendingScrollRestore { get; set; }

    public void UpdateReadingProgress(int scrollOffset = 0, double chapterScrollRatio = 0)
    {
        if (CurrentNovel is null) return;

        _currentProgress ??= new ReadingProgress { NovelId = CurrentNovel.Id };
        _currentProgress.ChapterIndex = _currentChapterIndex;
        _currentProgress.TotalChapters = CurrentNovel.Chapters.Count;
        _currentProgress.ScrollOffset = scrollOffset;
        _currentProgress.ChapterScrollRatio = chapterScrollRatio;
        _currentProgress.UpdatedAt = DateTime.Now;
        _bookmarkService.SaveProgressWithBookmarks(_currentProgress);
    }

    private void AddBookmark()
    {
        if (_currentNovel is null || _currentChapter is null) return;

        var scrollRatio = _currentProgress?.ChapterScrollRatio ?? 0;
        var bookmark = new Bookmark
        {
            ChapterIndex = _currentChapterIndex,
            CharOffsetInChapter = 0,
            ScrollRatio = scrollRatio,
            Note = $"{_currentChapter.Title} ({scrollRatio:P0})"
        };

        _bookmarkService.AddBookmark(_currentNovel.Id, bookmark);

        // Update in-memory progress if loaded
        if (_currentProgress is not null)
            _currentProgress.Bookmarks.Add(bookmark);

        OnBookmarkAdded?.Invoke($"已添加书签: {bookmark.Note}");
    }

    public List<Bookmark> GetBookmarksForCurrentNovel()
    {
        if (_currentNovel is null) return [];
        return _bookmarkService.GetBookmarks(_currentNovel.Id);
    }

    public void RemoveBookmark(string bookmarkId)
    {
        if (_currentNovel is null) return;
        _bookmarkService.RemoveBookmark(_currentNovel.Id, bookmarkId);
        if (_currentProgress is not null)
            _currentProgress.Bookmarks.RemoveAll(b => b.Id == bookmarkId);
    }

    private void ToggleAutoScroll()
    {
        _autoScrollService.Toggle();
        IsAutoScrolling = _autoScrollService.IsRunning;
    }

    private void OpenFile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "小说文件 (*.txt;*.epub)|*.txt;*.epub|文本文件 (*.txt)|*.txt|EPUB文件 (*.epub)|*.epub",
            Title = "选择小说文件"
        };

        if (dialog.ShowDialog() == true)
        {
            // Delegate to MainWindow so it can persist the live scroll position before
            // LoadNovel swaps out the current novel — direct LoadNovel here would lose
            // the last few seconds of scrolling not yet covered by the debounced save.
            if (OnLoadNovelRequested is not null)
                OnLoadNovelRequested.Invoke(dialog.FileName);
            else
                LoadNovel(dialog.FileName);
        }
    }

    private void LoadSettings()
    {
        var settings = _dataStore.LoadSettings();
        _textOpacity = settings.Opacity;
        _fontSize = settings.FontSize;
        _fontColor = settings.FontColor;
        // Strip alpha from background color — opacity slider controls transparency
        _backgroundColor = StripAlpha(settings.BackgroundColor);
        _lineSpacing = settings.LineSpacing;
        _paragraphSpacing = settings.ParagraphSpacing;
        _autoScrollService.SpeedLevel = settings.AutoScrollSpeed;
        _scrollSpeed = settings.ScrollSpeed;
    }

    private static string StripAlpha(string color)
    {
        // #AARRGGBB → #RRGGBB, leave #RGB and #RRGGBB as-is
        if (color.Length == 9 && color.StartsWith('#'))
            return "#" + color[3..];
        return color;
    }

    /// <summary>
    /// Reloads settings from disk and raises property changed for all appearance properties.
    /// Call this after the SettingsWindow closes to apply new settings.
    /// </summary>
    public void ReloadSettings()
    {
        LoadSettings();
        OnPropertyChanged(nameof(FontSize));
        OnPropertyChanged(nameof(FontColor));
        OnPropertyChanged(nameof(BackgroundColor));
        OnPropertyChanged(nameof(TextOpacity));
        OnPropertyChanged(nameof(LineSpacing));
        OnPropertyChanged(nameof(ParagraphSpacing));
        OnPropertyChanged(nameof(AutoScrollSpeed));
        OnPropertyChanged(nameof(ScrollSpeed));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
