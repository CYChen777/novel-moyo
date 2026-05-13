using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using NovelMoyo.Models;
using NovelMoyo.Services;
using NovelMoyo.Services.NovelParser;
using NovelMoyo.ViewModels;

namespace NovelMoyo.Views;

public partial class MainWindow : Window
{
    // Window style P/Invoke — use Ptr variants for 64-bit compatibility
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hwnd, int index, IntPtr newStyle);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern IntPtr GetWindowLongPtr32(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern IntPtr SetWindowLongPtr32(IntPtr hwnd, int index, IntPtr newStyle);

    private static IntPtr GetWindowLongPtr(IntPtr hwnd, int index)
        => IntPtr.Size == 8 ? GetWindowLongPtr64(hwnd, index) : GetWindowLongPtr32(hwnd, index);
    private static IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr newStyle)
        => IntPtr.Size == 8 ? SetWindowLongPtr64(hwnd, index, newStyle) : SetWindowLongPtr32(hwnd, index, newStyle);

    // Low-level mouse hook
    private const int WH_MOUSE_LL = 14;
    private const int WM_MOUSEWHEEL = 0x020A;
    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(POINT Point);
    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);
    private const uint GA_ROOT = 2;
    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT { public POINT pt; public uint mouseData; public uint flags; public uint time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }
    private IntPtr _mouseHookHandle;
    private LowLevelMouseProc? _mouseHookProc;

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_LAYERED = 0x00080000;

    private readonly MainViewModel _vm;
    private readonly HotkeyService _hotkeyService;
    private readonly AutoScrollService _autoScrollService;
    private AppSettings _settings;

    private bool _isLocked;
    private bool _isContentVisible;
    private bool _isReallyClosing;
    private System.Windows.Threading.DispatcherTimer? _toastTimer;

    /// <summary>
    /// Current scroll position, exposed for App.OnExit to save progress.
    /// </summary>
    public double ReadingScrollViewerVerticalOffset => ReadingScrollViewer.VerticalOffset;
    public double ReadingScrollViewerScrollableHeight => ReadingScrollViewer.ScrollableHeight;

    /// <summary>
    /// Saves the current novel's reading progress with the real-time scroll position.
    /// Call this before switching novels or closing to avoid stale scroll data.
    /// </summary>
    public void SaveCurrentProgress()
    {
        var sv = ReadingScrollViewer;
        var scrollPos = (int)sv.VerticalOffset;
        var chapterRatio = sv.ScrollableHeight > 0 ? sv.VerticalOffset / sv.ScrollableHeight : 0;
        _vm.SaveCurrentProgressWithScroll(scrollPos, chapterRatio);
    }

    /// <summary>
    /// Current novel ID, exposed for App.OnExit to save last novel.
    /// </summary>
    public string? CurrentNovelId => _vm.CurrentNovel?.Id;

    // Progressive chapter loading — only keep a window of chapters in the TextBlock
    private readonly List<int> _chapterCharOffsets = []; // char offset of each chapter start in current TextBlock text
    private bool _isUpdatingFromScroll;
    private bool _isBuildingContent;
    private bool _isAdjustingScroll;
    private bool _isAppendingOrTrimming; // prevents re-entrant AppendNextChapter/TrimOldChapters during ScrollChanged
    private double _pendingScrollRatio; // scroll ratio waiting to be restored after layout
    private int _loadedChapterStart = -1; // first chapter index loaded in TextBlock
    private int _loadedChapterEnd = -1;   // last chapter index loaded in TextBlock
    private const int ChapterBufferSize = 3; // load this many chapters above/below current
    private DateTime _lastProgressSave = DateTime.MinValue;
    private DateTime _contentLoadTime = DateTime.MinValue;

    public MainWindow(MainViewModel vm, AppSettings settings)
    {
        _vm = vm;
        _settings = settings;
        _autoScrollService = _vm.AutoScrollServiceInstance;
        _hotkeyService = new HotkeyService();

        DataContext = _vm;
        InitializeComponent();

        Left = settings.WindowLeft;
        Top = settings.WindowTop;
        Width = settings.WindowWidth;
        Height = settings.WindowHeight;

        // Ensure window is within visible screen bounds
        EnsureWindowOnScreen();

        _autoScrollService.OnScrollTick += () =>
        {
            if (ReadingScrollViewer.VerticalOffset < ReadingScrollViewer.ScrollableHeight)
            {
                // Apply ScrollSpeed multiplier: default 1.0x = 1 LineDown, 3.0x = 3 LineDowns per tick
                var steps = Math.Max(1, (int)Math.Round(_vm.ScrollSpeed));
                for (int i = 0; i < steps; i++)
                    ReadingScrollViewer.LineDown();
            }
        };

        _autoScrollService.OnSpeedChanged += level =>
        {
            ShowToast($"自动滚动速度: {level}档");
            _vm.AutoScrollSpeed = level;
        };

        ReadingText.FontSize = _vm.FontSize;
        ReadingText.LineHeight = _vm.FontSize * _vm.LineSpacing;

        // Apply saved theme on startup
        if (_settings.Theme != "Dark")
            ApplyTheme(_settings.Theme);

        _vm.PropertyChanged += OnViewModelPropertyChanged;
        _vm.OnBookmarkAdded += msg => ShowToast(msg);
        _vm.OnToastRequested += msg => ShowToast(msg);

        AllowDrop = true;
        Drop += OnFileDrop;
        DragOver += OnDragOver;

        Loaded += OnWindowLoaded;
        IsVisibleChanged += (_, e) => _vm.IsVisible = (bool)e.NewValue;

        ShowContent();
    }

    public void InjectServices(DataStore dataStore, BookshelfService bookshelfService, NovelParserFactory parserFactory, BookmarkService bookmarkService)
    {
        _dataStore = dataStore;
        _bookshelfService = bookshelfService;
        _parserFactory = parserFactory;
        _bookmarkService = bookmarkService;
    }

    private DataStore? _dataStore;
    private BookshelfService? _bookshelfService;
    private BookmarkService? _bookmarkService;
    private NovelParserFactory? _parserFactory;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hotkeyService.Init(this);
        RegisterHotkeys();
        InstallMouseHook();
    }

    private void InstallMouseHook()
    {
        _mouseHookProc = MouseHookCallback;
        _mouseHookHandle = SetWindowsHookEx(WH_MOUSE_LL, _mouseHookProc, IntPtr.Zero, 0);
    }

    private void RegisterHotkeys()
    {
        var hotkeys = _settings.Hotkeys;
        int id = 1;

        var actions = new Dictionary<string, Action>
        {
            ["ToggleVisibility"] = () => { if (IsVisible) Hide(); else { Show(); Activate(); } },            ["ToggleLock"] = () => { _isLocked = !_isLocked; _vm.IsLocked = _isLocked; if (_isLocked) ShowContent(); },
            ["ToggleAutoScroll"] = () => _vm.ToggleAutoScrollCommand.Execute(null),
            ["SpeedUp"] = () => _vm.SpeedUpCommand.Execute(null),
            ["SpeedDown"] = () => _vm.SpeedDownCommand.Execute(null),
            ["PrevChapter"] = () => _vm.PrevChapterCommand.Execute(null),
            ["NextChapter"] = () => _vm.NextChapterCommand.Execute(null),
            ["TogglePassthrough"] = () => _vm.TogglePassthroughCommand.Execute(null),
            ["ToggleTopmost"] = () => _vm.ToggleTopmostCommand.Execute(null),
            ["OpenSettings"] = OpenSettings,
            ["OpenBookshelf"] = OpenBookshelf,
            ["AddBookmark"] = AddBookmark
        };

        foreach (var (action, callback) in actions)
        {
            var combo = hotkeys.GetValueOrDefault(action);
            if (combo is not null && TryParseHotkey(combo, out var mod, out var key))
                TryRegister(id++, mod, key, callback);
        }
    }

    private static bool TryParseHotkey(string combo, out System.Windows.Input.ModifierKeys mod, out System.Windows.Input.Key key)
    {
        mod = System.Windows.Input.ModifierKeys.None;
        key = System.Windows.Input.Key.None;

        var parts = combo.Split('+');
        if (parts.Length < 2) return false;

        var keyName = parts[^1].Trim();
        for (int i = 0; i < parts.Length - 1; i++)
        {
            var modStr = parts[i].Trim().ToLowerInvariant();
            switch (modStr)
            {
                case "ctrl": mod |= System.Windows.Input.ModifierKeys.Control; break;
                case "alt": mod |= System.Windows.Input.ModifierKeys.Alt; break;
                case "shift": mod |= System.Windows.Input.ModifierKeys.Shift; break;
                case "win": mod |= System.Windows.Input.ModifierKeys.Windows; break;
            }
        }

        // Handle special key names
        keyName = keyName switch
        {
            "OemComma" => "OemComma",
            "Up" => "Up",
            "Down" => "Down",
            "Left" => "Left",
            "Right" => "Right",
            _ => keyName
        };

        try
        {
            key = (System.Windows.Input.Key)Enum.Parse(typeof(System.Windows.Input.Key), keyName);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void TryRegister(int id, System.Windows.Input.ModifierKeys mod, System.Windows.Input.Key key, Action callback)
    {
        _hotkeyService.RegisterHotkey(id, mod, key, callback);
    }

    private void ReloadHotkeys()
    {
        _hotkeyService.UnregisterAll();
        RegisterHotkeys();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(_vm.CurrentNovel):
                // New novel loaded — build full book content
                BuildFullBookContent();
                break;
            case nameof(_vm.CurrentChapter):
                if (!_isUpdatingFromScroll && _vm.CurrentNovel is not null)
                {
                    var idx = _vm.CurrentChapter?.Index ?? 0;
                    // If the chapter isn't in the loaded range, reload around it
                    if (idx < _loadedChapterStart || idx > _loadedChapterEnd)
                    {
                        _isAdjustingScroll = true;
                        var loadStart = Math.Max(0, idx - ChapterBufferSize);
                        var loadEnd = Math.Min(_vm.CurrentNovel.Chapters.Count - 1, idx + ChapterBufferSize);
                        LoadChapterRange(loadStart, loadEnd);
                    }
                    var localIdx = idx - _loadedChapterStart;
                    if (localIdx >= 0 && localIdx < _chapterCharOffsets.Count)
                    {
                        // Check if we have a saved scroll ratio to restore
                        var savedRatio = _vm.SavedScrollRatio;
                        if (savedRatio > 0.01)
                        {
                            if (IsLoaded)
                            {
                                // Window is already visible — restore immediately after layout
                                _isAdjustingScroll = true;
                                _pendingScrollRatio = savedRatio;
                                _vm.SavedScrollRatio = 0; // consumed
                                CompositionTarget.Rendering += OnRestoreScrollAfterLayout;
                            }
                            else
                            {
                                // Window not yet shown — defer to OnWindowLoaded
                                _vm.PendingScrollRestore = true;
                            }
                        }
                        else
                        {
                            _isAdjustingScroll = false;
                            // Scroll to chapter start using ratio (more reliable than char offset for CJK)
                            var chapterRatio = (double)_chapterCharOffsets[localIdx] / Math.Max(1, ReadingText.Text.Length);
                            ScrollToRatio(chapterRatio);
                        }
                    }
                    else
                    {
                        _isAdjustingScroll = false;
                    }
                }
                break;
            case nameof(_vm.IsTopmost):
                Topmost = _vm.IsTopmost;
                break;
            case nameof(_vm.IsPassthrough):
                SetClickThrough(_vm.IsPassthrough);
                break;
            case nameof(_vm.FontSize):
                ReadingText.FontSize = _vm.FontSize;
                ReadingText.LineHeight = _vm.FontSize * _vm.LineSpacing;
                break;
            case nameof(_vm.FontColor):
                try
                {
                    ReadingText.Foreground = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_vm.FontColor));
                }
                catch { }
                break;
            case nameof(_vm.BackgroundColor):
            case nameof(_vm.TextOpacity):
                if (!_isContentVisible) ApplyHiddenBackground();
                break;
            case nameof(_vm.LineSpacing):
                ReadingText.LineHeight = _vm.FontSize * _vm.LineSpacing;
                break;
            case nameof(_vm.ParagraphSpacing):
                // Rebuild content to apply new paragraph spacing
                BuildFullBookContent();
                break;
        }
    }

    private void EnsureWindowOnScreen()
    {
        // Check if the saved window position is within any visible screen.
        // Screen.WorkingArea uses physical pixels, WPF Left/Top use device-independent pixels (DIP).
        // We must convert DIP → physical pixels for comparison.
        var dpiScale = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice ?? System.Windows.Media.Matrix.Identity;
        var dpiX = dpiScale.M11; // typically 1.0 at 100%, 1.5 at 150%
        var dpiY = dpiScale.M22;

        // If the visual isn't yet in a presentation source (e.g. before Loaded), use system DPI
        if (dpiX == 0 || dpiY == 0)
        {
            // Use WPF's built-in system DPI instead of creating a dummy HwndSource
            using var gfx = System.Drawing.Graphics.FromHwnd(IntPtr.Zero);
            dpiX = gfx.DpiX / 96.0;
            dpiY = gfx.DpiY / 96.0;
        }

        var screens = System.Windows.Forms.Screen.AllScreens;
        var physLeft = (int)(Left * dpiX);
        var physTop = (int)(Top * dpiY);

        var isVisible = false;
        foreach (var screen in screens)
        {
            // Check if at least the top-left corner of the window is on this screen (in physical pixels)
            if (screen.WorkingArea.Contains(physLeft + (int)(20 * dpiX), physTop + (int)(20 * dpiY)))
            {
                isVisible = true;
                break;
            }
        }

        if (!isVisible && screens.Length > 0)
        {
            // Reset to primary screen — convert physical WorkingArea back to DIP
            var primary = System.Windows.Forms.Screen.PrimaryScreen?.WorkingArea
                ?? screens[0].WorkingArea;
            Left = primary.Left / dpiX + 100 / dpiX;
            Top = primary.Top / dpiY + 100 / dpiY;
        }
    }

    // Hover show/hide
    protected override void OnMouseEnter(System.Windows.Input.MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        ShowContent();
    }

    protected override void OnMouseLeave(System.Windows.Input.MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        if (!_isLocked) HideContent();
    }

    private void ShowContent()
    {
        if (_isContentVisible) return;
        _isContentVisible = true;
        ApplyVisibleBackground();
        ReadingText.Opacity = 1;
        StatusBar.Opacity = 1;
    }

    private void ApplyVisibleBackground()
    {
        try
        {
            var bg = TryFindResource("BackgroundBrush") as System.Windows.Media.SolidColorBrush;
            if (bg is not null)
            {
                FrameBorder.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(200, bg.Color.R, bg.Color.G, bg.Color.B));
                FrameBorder.BorderBrush = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(80, 255, 255, 255));
            }
            else
            {
                FrameBorder.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(200, 0, 0, 0));
                FrameBorder.BorderBrush = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(80, 255, 255, 255));
            }
        }
        catch
        {
            FrameBorder.Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(200, 0, 0, 0));
            FrameBorder.BorderBrush = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(80, 255, 255, 255));
        }
        FrameBorder.BorderThickness = new Thickness(1);
    }

    private void HideContent()
    {
        if (!_isContentVisible) return;
        _isContentVisible = false;
        ApplyHiddenBackground();
        ReadingText.Opacity = 0;
        StatusBar.Opacity = 0;
    }

    private void ApplyHiddenBackground()
    {
        try
        {
            var themeBg = TryFindResource("BackgroundBrush") as System.Windows.Media.SolidColorBrush;
            var alpha = (byte)Math.Clamp(_vm.TextOpacity * 255, 0, 255);
            var bgColor = themeBg?.Color ?? System.Windows.Media.Color.FromRgb(0, 0, 0);
            FrameBorder.Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(alpha, bgColor.R, bgColor.G, bgColor.B));
            FrameBorder.BorderBrush = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(alpha, bgColor.R, bgColor.G, bgColor.B));
        }
        catch
        {
            var alpha = (byte)Math.Clamp(_vm.TextOpacity * 255, 0, 255);
            FrameBorder.Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(alpha, 0, 0, 0));
            FrameBorder.BorderBrush = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(alpha, 0, 0, 0));
        }
        FrameBorder.BorderThickness = new Thickness(1);
    }

    // Window drag
    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        // Restore scroll position after the window is fully laid out.
        // This handles the case where LoadNovel was called before the window was shown
        // (e.g. on startup with StartWithLastNovel), when ScrollableHeight was 0.
        if (_vm.PendingScrollRestore && _vm.SavedScrollRatio > 0.01)
        {
            _isAdjustingScroll = true;
            _pendingScrollRatio = _vm.SavedScrollRatio;
            _vm.SavedScrollRatio = 0;
            _vm.PendingScrollRestore = false;
            CompositionTarget.Rendering += OnRestoreScrollAfterLayout;
        }
    }

    // Window drag
    protected override void OnMouseLeftButtonDown(System.Windows.Input.MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (e.OriginalSource is not System.Windows.Controls.Primitives.Thumb)
        {
            try { DragMove(); } catch (InvalidOperationException) { }
        }
    }

    // === PROGRESSIVE CHAPTER LOADING ===
    // Only keep a window of chapters in the TextBlock to avoid WPF layout freeze on large novels.

    private static readonly string ChapterSeparator = "\n\n————————————————————\n\n";

    private void BuildFullBookContent()
    {
        var chapters = _vm.CurrentNovel?.Chapters;
        if (chapters is null || chapters.Count == 0)
        {
            ReadingText.Text = "拖拽 .txt 或 .epub 文件到此处开始阅读";
            _chapterCharOffsets.Clear();
            _loadedChapterStart = -1;
            _loadedChapterEnd = -1;
            return;
        }

        // If CurrentChapter is not set yet, clear content and defer loading
        // to OnViewModelPropertyChanged when CurrentChapter is set by SwitchToChapter.
        // This prevents loading chapter 0 unnecessarily and avoids scroll position conflicts.
        if (_vm.CurrentChapter is null)
        {
            _isAdjustingScroll = true; // block UpdateCurrentChapterFromScroll until chapter is set
            ReadingText.Text = "";
            _chapterCharOffsets.Clear();
            _loadedChapterStart = -1;
            _loadedChapterEnd = -1;
            return;
        }

        var startIdx = _vm.CurrentChapter.Index;
        var loadStart = Math.Max(0, startIdx - ChapterBufferSize);
        var loadEnd = Math.Min(chapters.Count - 1, startIdx + ChapterBufferSize);
        LoadChapterRange(loadStart, loadEnd);
    }

    private void LoadChapterRange(int startIdx, int endIdx)
    {
        var chapters = _vm.CurrentNovel?.Chapters;
        if (chapters is null) return;

        startIdx = Math.Max(0, startIdx);
        endIdx = Math.Min(chapters.Count - 1, endIdx);
        if (startIdx > endIdx) return;

        var sb = new System.Text.StringBuilder();
        _chapterCharOffsets.Clear();

        for (int i = startIdx; i <= endIdx; i++)
        {
            _chapterCharOffsets.Add(sb.Length);
            sb.Append(ApplyParagraphSpacing(chapters[i].Content));
            if (i < endIdx)
                sb.Append(ChapterSeparator);
        }

        _loadedChapterStart = startIdx;
        _loadedChapterEnd = endIdx;
        _isBuildingContent = true;
        ReadingText.Text = sb.ToString();
        _isBuildingContent = false;
        _contentLoadTime = DateTime.Now;
    }

    private void AppendNextChapter()
    {
        var chapters = _vm.CurrentNovel?.Chapters;
        if (chapters is null || _loadedChapterEnd >= chapters.Count - 1) return;

        var nextIdx = _loadedChapterEnd + 1;
        var newContent = ChapterSeparator + ApplyParagraphSpacing(chapters[nextIdx].Content);

        _chapterCharOffsets.Add(ReadingText.Text.Length + ChapterSeparator.Length);
        _isBuildingContent = true;
        ReadingText.Text += newContent;
        _isBuildingContent = false;
        _loadedChapterEnd = nextIdx;
    }

    private void PrependPreviousChapter()
    {
        var chapters = _vm.CurrentNovel?.Chapters;
        if (chapters is null || _loadedChapterStart <= 0) return;

        var prevIdx = _loadedChapterStart - 1;
        var newContent = ApplyParagraphSpacing(chapters[prevIdx].Content) + ChapterSeparator;
        var addedLength = newContent.Length;

        // Update existing offsets (they all shift by addedLength)
        for (int i = 0; i < _chapterCharOffsets.Count; i++)
            _chapterCharOffsets[i] += addedLength;

        _chapterCharOffsets.Insert(0, 0);
        _loadedChapterStart = prevIdx;

        // Preserve scroll position
        var prevScrollable = ReadingScrollViewer.ScrollableHeight;
        var prevOffset = ReadingScrollViewer.VerticalOffset;

        _isBuildingContent = true;
        _isAdjustingScroll = true;
        ReadingText.Text = newContent + ReadingText.Text;
        _isBuildingContent = false;

        // After prepending, restore scroll position so the same content stays in view
        Dispatcher.BeginInvoke(() =>
        {
            var newScrollable = ReadingScrollViewer.ScrollableHeight;
            if (prevScrollable > 0)
            {
                var addedScroll = newScrollable - prevScrollable;
                ReadingScrollViewer.ScrollToVerticalOffset(prevOffset + addedScroll);
            }
            _isAdjustingScroll = false;
        }, System.Windows.Threading.DispatcherPriority.Render);
    }

    private void TrimOldChapters()
    {
        // Remove chapters that are far from the current viewport to keep TextBlock size manageable.
        // Only trim at most one chapter per call to prevent cascade trimming that causes scroll jumps.
        var chapters = _vm.CurrentNovel?.Chapters;
        if (chapters is null || _chapterCharOffsets.Count <= ChapterBufferSize * 2 + 1) return;

        var sv = ReadingScrollViewer;
        if (sv.ScrollableHeight <= 0) return;

        // Determine which local chapter index is at the viewport center
        var scrollRatio = sv.VerticalOffset / sv.ScrollableHeight;
        var totalChars = ReadingText.Text.Length;
        var currentChar = (int)(scrollRatio * totalChars);

        var currentLocalIdx = 0;
        for (int i = _chapterCharOffsets.Count - 1; i >= 0; i--)
        {
            if (currentChar >= _chapterCharOffsets[i])
            {
                currentLocalIdx = i;
                break;
            }
        }

        // Trim at most one chapter from the beginning if too many before current
        if (currentLocalIdx > ChapterBufferSize + 1 && _chapterCharOffsets.Count > ChapterBufferSize * 2 + 1)
        {
            RemoveFirstChapter();
        }

        // Trim at most one chapter from the end if too many after current
        if (_chapterCharOffsets.Count - 1 - currentLocalIdx > ChapterBufferSize + 1 && _chapterCharOffsets.Count > ChapterBufferSize * 2 + 1)
        {
            RemoveLastChapter();
        }
    }

    private void RemoveFirstChapter()
    {
        if (_chapterCharOffsets.Count < 2) return;

        // Record the current scroll ratio before removing content
        var sv = ReadingScrollViewer;
        var prevScrollRatio = sv.ScrollableHeight > 0 ? sv.VerticalOffset / sv.ScrollableHeight : 0;

        // Find where the separator before chapter 1 starts, to remove it cleanly
        var chapter1Start = _chapterCharOffsets[1];
        var text = ReadingText.Text;
        var sepIdx = text.LastIndexOf(ChapterSeparator, chapter1Start, chapter1Start);
        var cutPoint = sepIdx >= 0 ? sepIdx : chapter1Start;
        var removedLen = cutPoint;

        _chapterCharOffsets.RemoveAt(0);
        for (int i = 0; i < _chapterCharOffsets.Count; i++)
            _chapterCharOffsets[i] -= removedLen;

        _isBuildingContent = true;
        _isAdjustingScroll = true;
        ReadingText.Text = text[cutPoint..];
        _isBuildingContent = false;
        _loadedChapterStart++;

        // Restore scroll position by ratio
        Dispatcher.BeginInvoke(() =>
        {
            if (sv.ScrollableHeight > 0)
                sv.ScrollToVerticalOffset(sv.ScrollableHeight * prevScrollRatio);
            _isAdjustingScroll = false;
        }, System.Windows.Threading.DispatcherPriority.Render);
    }

    private void RemoveLastChapter()
    {
        if (_chapterCharOffsets.Count < 2) return;

        var lastOffset = _chapterCharOffsets[^1];
        // Also remove the separator before the last chapter
        var text = ReadingText.Text;
        var sepIdx = text.LastIndexOf(ChapterSeparator, lastOffset);
        var cutPoint = sepIdx >= 0 ? sepIdx : lastOffset;

        _chapterCharOffsets.RemoveAt(_chapterCharOffsets.Count - 1);

        _isBuildingContent = true;
        ReadingText.Text = text[..cutPoint];
        _isBuildingContent = false;
        _loadedChapterEnd--;
    }

    private void ScrollToCharOffset(int charOffset)
    {
        if (charOffset <= 0) { ReadingScrollViewer.ScrollToTop(); return; }
        // For TextBlock-based content, character-to-pixel ratio is approximately linear
        // for CJK text (which is the primary use case). Apply a two-pass approach:
        // 1. Estimate position via ratio, then 2. Refine using actual rendered scroll.
        var totalChars = ReadingText.Text.Length;
        if (totalChars <= 0) return;
        var ratio = (double)charOffset / totalChars;
        var targetOffset = ReadingScrollViewer.ScrollableHeight * ratio;
        ReadingScrollViewer.ScrollToVerticalOffset(targetOffset);
    }

    /// <summary>
    /// Scroll to a chapter by its scroll ratio (0.0-1.0) within the loaded content.
    /// More reliable than char-offset for CJK text.
    /// </summary>
    private void ScrollToRatio(double ratio)
    {
        if (ratio <= 0) { ReadingScrollViewer.ScrollToTop(); return; }
        if (ratio >= 1.0) { ReadingScrollViewer.ScrollToBottom(); return; }
        ReadingScrollViewer.ScrollToVerticalOffset(ReadingScrollViewer.ScrollableHeight * ratio);
    }

    /// <summary>
    /// Expands paragraph breaks (\n\n) with extra newlines based on ParagraphSpacing setting.
    /// </summary>
    private string ApplyParagraphSpacing(string content)
    {
        var spacing = _vm.ParagraphSpacing;
        if (spacing <= 0) return content.Replace("\r\n", "\n");
        // Each unit of ParagraphSpacing adds one extra blank line between paragraphs
        var extraNewlines = new string('\n', (int)Math.Round(spacing / 4.0));
        // Normalize line endings first, then expand paragraph breaks
        var normalized = content.Replace("\r\n", "\n");
        return normalized.Replace("\n\n", "\n" + extraNewlines + "\n");
    }

    private void OnRestoreScrollAfterLayout(object? sender, EventArgs e)
    {
        CompositionTarget.Rendering -= OnRestoreScrollAfterLayout;
        var ratio = _pendingScrollRatio;
        _pendingScrollRatio = 0;

        if (ratio > 0 && ReadingScrollViewer.ScrollableHeight > 0)
        {
            var target = ReadingScrollViewer.ScrollableHeight * ratio;
            ReadingScrollViewer.ScrollToVerticalOffset(Math.Max(0, target));
        }
        _isAdjustingScroll = false;
    }

    private void UpdateCurrentChapterFromScroll()
    {
        if (_chapterCharOffsets.Count == 0 || _vm.CurrentNovel is null) return;
        if (_isBuildingContent || _isAdjustingScroll || _isAppendingOrTrimming) return;

        var sv = ReadingScrollViewer;

        // Update chapter tracking even when there is nothing to scroll
        if (sv.ScrollableHeight <= 0)
        {
            // Short chapter that fits entirely in the viewport — still save progress periodically
            if ((DateTime.Now - _lastProgressSave).TotalSeconds > 3)
            {
                _lastProgressSave = DateTime.Now;
                _vm.UpdateReadingProgress(0, 0);
            }
            return;
        }

        var scrollRatio = sv.VerticalOffset / sv.ScrollableHeight;
        var totalChars = ReadingText.Text.Length;
        var currentChar = (int)(scrollRatio * totalChars);

        // Find which local chapter this character belongs to
        var localIdx = 0;
        for (int i = _chapterCharOffsets.Count - 1; i >= 0; i--)
        {
            if (currentChar >= _chapterCharOffsets[i])
            {
                localIdx = i;
                break;
            }
        }

        // Map local index to global chapter index
        var globalIdx = _loadedChapterStart + localIdx;

        // Update ViewModel without triggering scroll-to-chapter
        if (_vm.CurrentChapter is null || _vm.CurrentChapter.Index != globalIdx)
        {
            try
            {
                _isUpdatingFromScroll = true;
                _vm.SwitchToChapter(globalIdx);
            }
            finally
            {
                _isUpdatingFromScroll = false;
            }
        }

        // Periodically save progress with chapter ratio (debounced to every 3 seconds)
        if ((DateTime.Now - _lastProgressSave).TotalSeconds > 3)
        {
            _lastProgressSave = DateTime.Now;
            var chapterRatio = sv.ScrollableHeight > 0 ? sv.VerticalOffset / sv.ScrollableHeight : 0;
            _vm.UpdateReadingProgress((int)sv.VerticalOffset, chapterRatio);
        }

        // Progressive loading: add chapters when scrolling near boundaries
        // Skip during the first 2 seconds after content load to prevent premature triggering during restore
        if ((DateTime.Now - _contentLoadTime).TotalSeconds < 2) return;
        if (_isAppendingOrTrimming) return;

        var nearBottom = sv.VerticalOffset > sv.ScrollableHeight * 0.92;
        var nearTop = sv.VerticalOffset < sv.ScrollableHeight * 0.08;

        if (nearBottom || nearTop)
        {
            _isAppendingOrTrimming = true;
            try
            {
                if (nearBottom)
                {
                    AppendNextChapter();
                    TrimOldChapters();
                }
                else if (nearTop)
                {
                    PrependPreviousChapter();
                    TrimOldChapters();
                }
            }
            finally
            {
                _isAppendingOrTrimming = false;
            }
        }
    }

    // === MOUSE WHEEL ===

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (int)wParam == WM_MOUSEWHEEL && IsVisible)
        {
            var hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            var delta = (short)((hookStruct.mouseData >> 16) & 0xFFFF);

            var hwndUnderMouse = WindowFromPoint(hookStruct.pt);
            var rootHwnd = GetAncestor(hwndUnderMouse, GA_ROOT);
            var ourHwnd = new WindowInteropHelper(this).Handle;

            if (rootHwnd == ourHwnd)
            {
                Dispatcher.BeginInvoke(() => HandleMouseWheel(delta));
                return (IntPtr)1;
            }
        }
        return CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
    }

    private void HandleMouseWheel(int delta)
    {
        if (!_isContentVisible) ShowContent();
        var sv = ReadingScrollViewer;
        var speedMultiplier = _vm.ScrollSpeed;
        var scrollStep = delta * speedMultiplier / 20.0;
        var newOffset = sv.VerticalOffset - scrollStep;
        sv.ScrollToVerticalOffset(Math.Clamp(newOffset, 0, sv.ScrollableHeight));
    }

    private void ReadingScrollViewer_ScrollChanged(object sender, System.Windows.Controls.ScrollChangedEventArgs e)
    {
        var sv = ReadingScrollViewer;
        _vm.UpdateScrollState(sv.ViewportHeight, sv.ScrollableHeight, sv.VerticalOffset);
        UpdateCurrentChapterFromScroll();
    }

    // Mouse passthrough
    public void SetClickThrough(bool enable)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        var style = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
        if (enable) SetWindowLongPtr(hwnd, GWL_EXSTYLE, (IntPtr)(style | WS_EX_TRANSPARENT | WS_EX_LAYERED));
        else SetWindowLongPtr(hwnd, GWL_EXSTYLE, (IntPtr)(style & ~WS_EX_TRANSPARENT));

        PassthroughIndicator.Visibility = enable ? Visibility.Visible : Visibility.Collapsed;
    }

    // Toast notification
    private void ShowToast(string message)
    {
        ToastText.Text = message;
        ToastOverlay.Visibility = Visibility.Visible;
        if (_toastTimer is null)
        {
            _toastTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _toastTimer.Tick += (_, _) =>
            {
                ToastOverlay.Visibility = Visibility.Collapsed;
                _toastTimer.Stop();
            };
        }
        else
        {
            _toastTimer.Stop();
        }
        _toastTimer.Start();
    }

    // Drag & drop
    private void OnDragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            e.Effects = System.Windows.DragDropEffects.Copy;
        else
            e.Effects = System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private void OnFileDrop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            var files = (string[]?)e.Data.GetData(System.Windows.DataFormats.FileDrop);
            if (files?.Length > 0)
            {
                var ext = Path.GetExtension(files[0]).ToLowerInvariant();
                if (ext is ".txt" or ".epub") { SaveCurrentProgress(); _vm.LoadNovel(files[0]); }
            }
        }
    }

    // Child windows
    private void ApplySettingsFromDisk()
    {
        _settings = _dataStore!.LoadSettings();
        _vm.ReloadSettings();
        ReloadHotkeys();
        ApplyTheme(_settings.Theme);
        if (!_isContentVisible) ApplyHiddenBackground();
        ReadingText.FontSize = _vm.FontSize;
        ReadingText.LineHeight = _vm.FontSize * _vm.LineSpacing;
    }

    private void OpenSettings()
    {
        try
        {
            if (_dataStore is null)
            {
                System.Windows.MessageBox.Show("服务未初始化", "错误");
                return;
            }
            var settingsVm = new SettingsViewModel(_dataStore, _bookmarkService, _vm.CurrentNovel?.Id);

            // Populate data — use CurrentNovel.Chapters directly instead of the ObservableCollection
            // which may be out of sync
            var novel = _vm.CurrentNovel;
            if (novel is not null && novel.Chapters.Count > 0)
            {
                settingsVm.LoadChapters(novel.Chapters, _vm.CurrentChapter?.Index ?? 0);
                settingsVm.LoadBookmarks(_vm.GetBookmarksForCurrentNovel());
            }

            var settingsWin = new SettingsWindow(settingsVm);

            settingsVm.OnFileImported += filePath =>
            {
                SaveCurrentProgress();
                settingsWin.Close();
                _vm.LoadNovel(filePath);
                Show();
                Activate();
            };

            settingsVm.OnChapterSelected += chapterIndex =>
            {
                _vm.SwitchToChapter(chapterIndex);
                Show();
                Activate();
                settingsWin.Close();
            };

            settingsVm.OnBookmarkSelected += (chapterIndex, scrollRatio) =>
            {
                _vm.SavedScrollRatio = scrollRatio;
                _vm.SwitchToChapter(chapterIndex);
                Show();
                Activate();
                settingsWin.Close();
            };

            settingsVm.OnSettingsApplied += ApplySettingsFromDisk;

            settingsWin.ShowDialog();
            // Re-apply after dialog closes to ensure MainWindow (which was behind modal) renders correctly
            ApplySettingsFromDisk();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"打开设置失败: {ex.Message}", "错误");
        }
    }

    private void AddBookmark()
    {
        if (_vm.CurrentNovel is null) return;
        _vm.AddBookmarkCommand.Execute(null);
    }

    private void OpenBookshelf()
    {
        try
        {
            if (_bookshelfService is null || _bookmarkService is null || _parserFactory is null || _dataStore is null)
            {
                System.Windows.MessageBox.Show("服务未初始化", "错误");
                return;
            }
            var bookshelfVm = new BookshelfViewModel(_bookshelfService, _bookmarkService, _parserFactory);
            var bookshelfWin = new BookshelfWindow(bookshelfVm);
            bookshelfVm.OnNovelSelected += novelId =>
            {
                var novel = _bookshelfService.GetNovel(novelId);
                if (novel is not null) { _bookshelfService.MarkAsRead(novelId); SaveCurrentProgress(); _vm.LoadNovel(novel.FilePath); Show(); Activate(); }
                else { System.Windows.MessageBox.Show("该书籍文件已丢失或被移动，请重新导入或从书架移除。", "文件缺失", MessageBoxButton.OK, MessageBoxImage.Warning); }
                bookshelfWin.Close();
            };
            bookshelfWin.ShowDialog();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"打开书架失败: {ex.Message}", "错误");
        }
    }

    // Theme switching
    private void ApplyTheme(string themeName)
    {
        try
        {
            var dict = new ResourceDictionary
            {
                Source = new Uri($"Resources/Themes/{themeName}Theme.xaml", UriKind.Relative)
            };
            var merged = Application.Current.Resources.MergedDictionaries;
            // Remove only existing theme dictionaries, preserve other merged dictionaries
            var existingThemes = merged.Where(d => d.Source?.OriginalString.Contains("Theme.xaml") == true).ToList();
            foreach (var t in existingThemes)
                merged.Remove(t);
            merged.Add(dict);

            // Re-apply visual state with new theme colors
            if (_isContentVisible) ApplyVisibleBackground(); else ApplyHiddenBackground();
        }
        catch { }
    }

    // Lifecycle
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_isReallyClosing)
        {
            e.Cancel = true;
            // Stop auto-scroll when hiding to prevent background scrolling
            if (_vm.IsAutoScrolling) _vm.ToggleAutoScrollCommand.Execute(null);
            Hide();
            return; // Don't save when merely hiding
        }

        // Save reading progress with chapter ratio (only on real close)
        var sv = ReadingScrollViewer;
        var scrollPos = (int)sv.VerticalOffset;
        var chapterRatio = sv.ScrollableHeight > 0 ? sv.VerticalOffset / sv.ScrollableHeight : 0;
        _vm.UpdateReadingProgress(scrollPos, chapterRatio);

        // Reload settings from disk to get the latest state (may have been updated by SettingsWindow),
        // then update with current window state before saving
        _settings = _dataStore?.LoadSettings() ?? _settings;
        _settings.LastNovelId = _vm.CurrentNovel?.Id;
        _settings.WindowLeft = Left;
        _settings.WindowTop = Top;
        _settings.WindowWidth = Width;
        _settings.WindowHeight = Height;
        _dataStore?.SaveSettings(_settings);

        // Clean up resources when really closing
        if (_isReallyClosing)
        {
            _vm.PropertyChanged -= OnViewModelPropertyChanged;
            if (_mouseHookHandle != IntPtr.Zero)
                UnhookWindowsHookEx(_mouseHookHandle);
            _hotkeyService.Dispose();
        }
    }

    public void RequestClose()
    {
        _isReallyClosing = true;
        Close();
    }

    /// <summary>
    /// Called from App's tray menu to open settings with proper data context.
    /// </summary>
    public void OpenSettingsFromTray() => OpenSettings();
}
