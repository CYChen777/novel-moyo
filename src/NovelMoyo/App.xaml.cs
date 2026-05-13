using System.Drawing;
using System.IO;
using System.Drawing.Drawing2D;
using System.Text;
using System.Windows;
using System.Windows.Forms;
using NovelMoyo.Models;
using NovelMoyo.Services;
using NovelMoyo.Services.BookSource;
using NovelMoyo.Services.Download;
using NovelMoyo.Services.NovelParser;
using NovelMoyo.ViewModels;
using NovelMoyo.Views;
using Application = System.Windows.Application;

namespace NovelMoyo;

public partial class App : Application
{
    private NotifyIcon? _trayIcon;
    private MainWindow? _mainWindow;
    private DataStore? _dataStore;
    private BookshelfService? _bookshelfService;
    private BookmarkService? _bookmarkService;
    private NovelParserFactory? _parserFactory;
    private BookSourceService? _bookSourceService;
    private SearchService? _searchService;
    private DownloadService? _downloadService;
    private AppSettings _settings = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Register GBK/GB2312 encoding support once at startup
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        // Global exception handler
        DispatcherUnhandledException += (_, args) =>
        {
            System.Windows.MessageBox.Show($"未处理的异常: {args.Exception}", "NovelMoyo 错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        try
        {
            _dataStore = new DataStore();
            _settings = _dataStore.LoadSettings();

            CreateTrayIcon();

            // Create services
            _parserFactory = new NovelParserFactory();
            _bookshelfService = new BookshelfService(_dataStore, _parserFactory);
            _bookmarkService = new BookmarkService(_dataStore);
            var autoScrollService = new AutoScrollService();

            // Download services
            _bookSourceService = new BookSourceService();
            _searchService = new SearchService(_bookSourceService);
            _downloadService = new DownloadService(_searchService, _dataStore);

            // Create MainWindow with ViewModel
            var mainVm = new MainViewModel(_dataStore, _bookshelfService, _bookmarkService, autoScrollService, _parserFactory);
            _mainWindow = new MainWindow(mainVm, _settings);
            _mainWindow.InjectServices(_dataStore, _bookshelfService, _parserFactory, _bookmarkService);

            // Auto-load last novel if setting is enabled
            if (_settings.StartWithLastNovel && !string.IsNullOrEmpty(_settings.LastNovelId))
            {
                var lastNovel = _bookshelfService.GetNovel(_settings.LastNovelId);
                if (lastNovel is not null && File.Exists(lastNovel.FilePath))
                {
                    _bookshelfService.MarkAsRead(_settings.LastNovelId);
                    mainVm.LoadNovel(lastNovel.FilePath);
                }
            }

            // Always show window on startup; user hides with hotkey when ready
            // Unless StartMinimizedToTray is enabled — then start silently in tray
            if (!_settings.StartMinimizedToTray)
            {
                _mainWindow.Show();
                _mainWindow.Activate();
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"启动失败: {ex}", "NovelMoyo 错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }

        // Save last novel ID — reading progress is already saved in MainWindow.OnClosing
        if (_dataStore is not null)
        {
            var latestSettings = _dataStore.LoadSettings();
            latestSettings.LastNovelId = _mainWindow?.CurrentNovelId;
            _dataStore.SaveSettings(latestSettings);
        }

        base.OnExit(e);
    }

    private void CreateTrayIcon()
    {
        _trayIcon = new NotifyIcon
        {
            Icon = CreateTrayIconGraphic(),
            Text = "NovelMoyo",
            Visible = true
        };

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add("打开书架", null, (_, _) => Dispatcher.Invoke(OpenBookshelf));
        contextMenu.Items.Add("设置", null, (_, _) => Dispatcher.Invoke(OpenSettings));
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("退出", null, (_, _) =>
        {
            Dispatcher.Invoke(() =>
            {
                _mainWindow?.RequestClose();
                Shutdown();
            });
        });

        _trayIcon.ContextMenuStrip = contextMenu;
        _trayIcon.MouseClick += (_, args) =>
        {
            if (args.Button == MouseButtons.Left)
                ToggleMainWindow();
        };
    }

    private void ToggleMainWindow()
    {
        if (_mainWindow is null) return;
        if (_mainWindow.IsVisible) _mainWindow.Hide();
        else { _mainWindow.Show(); _mainWindow.Activate(); }
    }

    private void OpenBookshelf()
    {
        if (_dataStore is null || _bookshelfService is null || _bookmarkService is null || _parserFactory is null) return;
        var vm = new BookshelfViewModel(_bookshelfService, _bookmarkService, _parserFactory);
        var win = new BookshelfWindow(vm);
        vm.OnNovelSelected += novelId =>
        {
            var novel = _bookshelfService.GetNovel(novelId);
            if (novel is not null && _mainWindow?.DataContext is MainViewModel mainVm)
            {
                _bookshelfService.MarkAsRead(novelId);
                _mainWindow.SaveCurrentProgress();
                mainVm.LoadNovel(novel.FilePath);
                _mainWindow.Show();
                _mainWindow.Activate();
            }
            else
            {
                System.Windows.MessageBox.Show("该书籍文件已丢失或被移动，请重新导入或从书架移除。", "文件缺失",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
            win.Close();
        };
        win.ShowDialog();
    }

    private void OpenSettings()
    {
        if (_mainWindow is null) return;
        // Delegate to MainWindow which has the correct ViewModel context and data
        _mainWindow.Dispatcher.Invoke(() =>
        {
            _mainWindow.Show();
            _mainWindow.Activate();
            _mainWindow.OpenSettingsFromTray();
        });
    }

    internal void OpenOnlineBookStore()
    {
        if (_searchService is null || _downloadService is null) return;
        var vm = new OnlineBookStoreViewModel(_searchService, _downloadService);
        var win = new OnlineBookStoreWindow(vm);
        win.ShowDialog();
    }

    private static Icon CreateTrayIconGraphic()
    {
        const int size = 16;
        using var bitmap = new Bitmap(size, size);
        using var g = Graphics.FromImage(bitmap);

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        using var bgBrush = new SolidBrush(Color.FromArgb(200, 30, 30, 30));
        g.FillRectangle(bgBrush, 0, 0, size, size);

        using var textBrush = new SolidBrush(Color.FromArgb(255, 100, 200, 100));
        using var font = new System.Drawing.Font("Segoe UI", 9f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Pixel);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
        g.DrawString("N", font, textBrush, new RectangleF(0, 0, size, size), format);

        var hIcon = bitmap.GetHicon();
        var icon = Icon.FromHandle(hIcon);
        var clonedIcon = (Icon)icon.Clone();
        DestroyIcon(hIcon);
        return clonedIcon;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
