using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using NovelMoyo.Models;
using NovelMoyo.Services;

namespace NovelMoyo.ViewModels;

public class SettingsViewModel : INotifyPropertyChanged
{
    private readonly DataStore _dataStore;
    private readonly BookmarkService? _bookmarkService;
    public readonly string? CurrentNovelId;
    private AppSettings _settings;

    private double _opacity;
    private int _fontSize;
    private string _fontColor = "#FFFFFF";
    private string _backgroundColor = "#000000";
    private double _lineSpacing;
    private double _paragraphSpacing;
    private string _theme = "Dark";
    private int _autoScrollSpeed;
    private bool _autoStart;
    private bool _startWithLastNovel;
    private bool _startMinimizedToTray;
    private double _scrollSpeed;

    public SettingsViewModel(DataStore dataStore, BookmarkService? bookmarkService = null, string? currentNovelId = null)
    {
        _dataStore = dataStore;
        _bookmarkService = bookmarkService;
        CurrentNovelId = currentNovelId;
        _settings = dataStore.LoadSettings();

        Themes = ["Dark", "Green", "Warm"];
        HotkeyEntries = [];

        LoadFromSettings(_settings);

        SaveCommand = new RelayCommand(Save);
        ResetCommand = new RelayCommand(ResetToDefaults);
        ImportFileCommand = new RelayCommand(ImportFile);
        DeleteBookmarkCommand = new RelayCommand(DeleteBookmark, () => _selectedBookmark is not null);
    }

    public ObservableCollection<string> Themes { get; }
    public ObservableCollection<HotkeyEntry> HotkeyEntries { get; }
    public ObservableCollection<Chapter> Chapters { get; } = [];
    public ObservableCollection<Bookmark> Bookmarks { get; } = [];

    public ICommand SaveCommand { get; }
    public ICommand ResetCommand { get; }
    public ICommand ImportFileCommand { get; }
    public ICommand DeleteBookmarkCommand { get; }

    /// <summary>
    /// Raised when the user imports a file from settings. Passes the file path.
    /// </summary>
    public event Action<string>? OnFileImported;

    /// <summary>
    /// Raised when the user selects a chapter from the list. Passes chapter index.
    /// </summary>
    public event Action<int>? OnChapterSelected;

    /// <summary>
    /// Raised when the user selects a bookmark to navigate to. Passes chapter index and scroll ratio.
    /// </summary>
    public event Action<int, double>? OnBookmarkSelected;

    /// <summary>
    /// Raised when settings are saved, so the main window can apply changes immediately.
    /// </summary>
    public event Action? OnSettingsApplied;

    private int _currentChapterIndex;
    public int CurrentChapterIndex
    {
        get => _currentChapterIndex;
        set { _currentChapterIndex = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Populate chapter list from the current novel.
    /// </summary>
    public void LoadChapters(IEnumerable<Chapter> chapters, int currentIndex)
    {
        Chapters.Clear();
        foreach (var ch in chapters)
            Chapters.Add(ch);
        _currentChapterIndex = currentIndex;
        OnPropertyChanged(nameof(CurrentChapterIndex));
    }

    public void SelectChapter(int index)
    {
        OnChapterSelected?.Invoke(index);
    }

    public void LoadBookmarks(IEnumerable<Bookmark> bookmarks)
    {
        Bookmarks.Clear();
        foreach (var bm in bookmarks)
            Bookmarks.Add(bm);
    }

    private Bookmark? _selectedBookmark;
    public Bookmark? SelectedBookmark
    {
        get => _selectedBookmark;
        set { _selectedBookmark = value; OnPropertyChanged(); }
    }

    public void SelectBookmark(Bookmark bookmark)
    {
        OnBookmarkSelected?.Invoke(bookmark.ChapterIndex, bookmark.ScrollRatio);
    }

    private void DeleteBookmark()
    {
        if (_selectedBookmark is null || _bookmarkService is null || CurrentNovelId is null) return;
        _bookmarkService.RemoveBookmark(CurrentNovelId, _selectedBookmark.Id);
        Bookmarks.Remove(_selectedBookmark);
        _selectedBookmark = null;
        OnPropertyChanged(nameof(SelectedBookmark));
    }

    public double Opacity
    {
        get => _opacity;
        set { _opacity = Math.Clamp(value, 0, 1.0); OnPropertyChanged(); }
    }

    public int FontSize
    {
        get => _fontSize;
        set { _fontSize = Math.Clamp(value, 10, 36); OnPropertyChanged(); }
    }

    public string FontColor
    {
        get => _fontColor;
        set { _fontColor = value; OnPropertyChanged(); }
    }

    public string BackgroundColor
    {
        get => _backgroundColor;
        set { _backgroundColor = value; OnPropertyChanged(); }
    }

    public double LineSpacing
    {
        get => _lineSpacing;
        set { _lineSpacing = Math.Clamp(value, 1.0, 3.0); OnPropertyChanged(); }
    }

    public double ParagraphSpacing
    {
        get => _paragraphSpacing;
        set { _paragraphSpacing = Math.Clamp(value, 0, 20); OnPropertyChanged(); }
    }

    public string Theme
    {
        get => _theme;
        set { _theme = value; OnPropertyChanged(); }
    }

    public int AutoScrollSpeed
    {
        get => _autoScrollSpeed;
        set { _autoScrollSpeed = Math.Clamp(value, 1, 5); OnPropertyChanged(); }
    }

    public bool AutoStart
    {
        get => _autoStart;
        set { _autoStart = value; OnPropertyChanged(); }
    }

    public bool StartWithLastNovel
    {
        get => _startWithLastNovel;
        set { _startWithLastNovel = value; OnPropertyChanged(); }
    }

    public bool StartMinimizedToTray
    {
        get => _startMinimizedToTray;
        set { _startMinimizedToTray = value; OnPropertyChanged(); }
    }

    public double ScrollSpeed
    {
        get => _scrollSpeed;
        set { _scrollSpeed = Math.Clamp(value, 0.5, 4.5); OnPropertyChanged(); }
    }

    public void Save()
    {
        _settings.Opacity = _opacity;
        _settings.FontSize = _fontSize;
        _settings.FontColor = _fontColor;
        _settings.BackgroundColor = _backgroundColor;
        _settings.LineSpacing = _lineSpacing;
        _settings.ParagraphSpacing = _paragraphSpacing;
        _settings.Theme = _theme;
        _settings.AutoScrollSpeed = _autoScrollSpeed;
        _settings.AutoStart = _autoStart;
        _settings.StartWithLastNovel = _startWithLastNovel;
        _settings.StartMinimizedToTray = _startMinimizedToTray;
        _settings.ScrollSpeed = _scrollSpeed;

        // Save hotkey changes
        foreach (var entry in HotkeyEntries)
        {
            _settings.Hotkeys[entry.ActionName] = entry.KeyCombination;
        }

        _dataStore.SaveSettings(_settings);

        // Handle auto-start registry
        UpdateAutoStart();

        // Notify listeners to apply settings immediately
        OnSettingsApplied?.Invoke();

        System.Windows.MessageBox.Show("设置已保存", "提示",
            System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
    }

    public void ResetToDefaults()
    {
        LoadFromSettings(new AppSettings());
    }

    private void ImportFile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "小说文件 (*.txt;*.epub)|*.txt;*.epub|文本文件 (*.txt)|*.txt|EPUB文件 (*.epub)|*.epub",
            Title = "选择小说文件"
        };

        if (dialog.ShowDialog() == true)
        {
            OnFileImported?.Invoke(dialog.FileName);
        }
    }

    private void LoadFromSettings(AppSettings s)
    {
        _opacity = s.Opacity;
        _fontSize = s.FontSize;
        _fontColor = s.FontColor;
        _backgroundColor = s.BackgroundColor;
        _lineSpacing = s.LineSpacing;
        _paragraphSpacing = s.ParagraphSpacing;
        _theme = s.Theme;
        _autoScrollSpeed = s.AutoScrollSpeed;
        _autoStart = s.AutoStart;
        _startWithLastNovel = s.StartWithLastNovel;
        _startMinimizedToTray = s.StartMinimizedToTray;
        _scrollSpeed = s.ScrollSpeed;

        HotkeyEntries.Clear();
        // Display hotkeys in a fixed order defined by displayNames
        var displayNames = new List<(string Action, string DisplayName)>
        {
            ("ToggleVisibility", "显示/隐藏"),
            ("ToggleLock", "锁定/解锁"),
            ("ToggleAutoScroll", "自动滚动"),
            ("SpeedUp", "加速"),
            ("SpeedDown", "减速"),
            ("PrevChapter", "上一章"),
            ("NextChapter", "下一章"),
            ("TogglePassthrough", "鼠标穿透"),
            ("ToggleTopmost", "置顶切换"),
            ("OpenSettings", "打开设置"),
            ("OpenBookshelf", "打开书架"),
            ("AddBookmark", "添加书签")
        };

        foreach (var (action, displayName) in displayNames)
        {
            var combo = s.Hotkeys.GetValueOrDefault(action, "");
            HotkeyEntries.Add(new HotkeyEntry
            {
                ActionName = action,
                DisplayName = displayName,
                KeyCombination = combo
            });
        }

        OnPropertyChanged(nameof(Opacity));
        OnPropertyChanged(nameof(FontSize));
        OnPropertyChanged(nameof(FontColor));
        OnPropertyChanged(nameof(BackgroundColor));
        OnPropertyChanged(nameof(LineSpacing));
        OnPropertyChanged(nameof(ParagraphSpacing));
        OnPropertyChanged(nameof(Theme));
        OnPropertyChanged(nameof(AutoScrollSpeed));
        OnPropertyChanged(nameof(AutoStart));
        OnPropertyChanged(nameof(StartWithLastNovel));
        OnPropertyChanged(nameof(StartMinimizedToTray));
        OnPropertyChanged(nameof(ScrollSpeed));
    }

    private void UpdateAutoStart()
    {
        try
        {
            const string regKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(regKey, true);
            const string appName = "NovelMoyo";

            if (_autoStart)
            {
                var exePath = Environment.ProcessPath ?? "";
                key?.SetValue(appName, $"\"{exePath}\"");
            }
            else
            {
                key?.DeleteValue(appName, false);
            }
        }
        catch
        {
            // Registry access might fail, silently ignore
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
