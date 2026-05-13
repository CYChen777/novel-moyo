using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Win32;
using NovelMoyo.Models;
using NovelMoyo.Services;
using NovelMoyo.Services.NovelParser;

namespace NovelMoyo.ViewModels;

public class BookshelfViewModel : INotifyPropertyChanged
{
    private readonly BookshelfService _bookshelfService;
    private readonly BookmarkService _bookmarkService;
    private readonly NovelParserFactory _parserFactory;
    private BookshelfNovelItem? _selectedNovel;

    public BookshelfViewModel(BookshelfService bookshelfService, BookmarkService bookmarkService, NovelParserFactory parserFactory)
    {
        _bookshelfService = bookshelfService;
        _bookmarkService = bookmarkService;
        _parserFactory = parserFactory;

        Novels = new ObservableCollection<BookshelfNovelItem>();

        ImportFileCommand = new RelayCommand(ImportFile);
        RemoveNovelCommand = new RelayCommand(RemoveSelected, () => SelectedNovel is not null);
        OpenNovelCommand = new RelayCommand(OpenSelected, () => SelectedNovel is not null);

        RefreshList();
    }

    public ObservableCollection<BookshelfNovelItem> Novels { get; }

    public BookshelfNovelItem? SelectedNovel
    {
        get => _selectedNovel;
        set
        {
            _selectedNovel = value;
            OnPropertyChanged();
            ((RelayCommand)OpenNovelCommand).RaiseCanExecuteChanged();
            ((RelayCommand)RemoveNovelCommand).RaiseCanExecuteChanged();
        }
    }

    public ICommand ImportFileCommand { get; }
    public ICommand RemoveNovelCommand { get; }
    public ICommand OpenNovelCommand { get; }

    public event Action<string>? OnNovelSelected;

    private void ImportFile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "小说文件 (*.txt;*.epub)|*.txt;*.epub|文本文件 (*.txt)|*.txt|EPUB文件 (*.epub)|*.epub",
            Title = "导入小说"
        };

        if (dialog.ShowDialog() == true)
        {
            ImportFileFromPath(dialog.FileName);
        }
    }

    public void ImportFileFromPath(string filePath)
    {
        try
        {
            var parser = _parserFactory.GetParser(filePath);
            var novel = parser.Parse(filePath);
            _bookshelfService.AddNovel(novel);

            RefreshList();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"导入失败: {ex.Message}", "错误",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private void RemoveSelected()
    {
        if (SelectedNovel is null) return;

        var result = System.Windows.MessageBox.Show(
            $"确定要从书架移除《{SelectedNovel.Title}》吗？",
            "确认", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);

        if (result == System.Windows.MessageBoxResult.Yes)
        {
            _bookshelfService.RemoveNovel(SelectedNovel.NovelId);
            RefreshList();
        }
    }

    private void OpenSelected()
    {
        if (SelectedNovel is null) return;
        OnNovelSelected?.Invoke(SelectedNovel.NovelId);
    }

    public void RefreshList()
    {
        // M18: Build list data (with I/O) then update collection on UI thread
        var entries = _bookshelfService.GetAll();
        var items = new List<BookshelfNovelItem>();

        foreach (var entry in entries)
        {
            var fileMissing = !System.IO.File.Exists(entry.FilePath);
            var progress = _bookmarkService.LoadProgress(entry.NovelId);
            string progressText;
            if (fileMissing)
            {
                progressText = "⚠ 文件缺失";
            }
            else if (progress is not null && progress.TotalChapters > 0)
            {
                var chapterIdx = progress.ChapterIndex + 1;
                var overallPercent = progress.TotalChapters > 0
                    ? (double)(progress.ChapterIndex + 1) / progress.TotalChapters * 100
                    : 0;
                progressText = $"第{chapterIdx}章 / 共{progress.TotalChapters}章 {overallPercent:F0}%";
            }
            else if (progress is not null)
            {
                progressText = $"第{progress.ChapterIndex + 1}章";
            }
            else
            {
                progressText = "暂无进度";
            }

            items.Add(new BookshelfNovelItem
            {
                NovelId = entry.NovelId,
                Title = entry.Title,
                Format = entry.Format,
                LastReadAt = entry.LastReadAt,
                ProgressText = progressText,
                FileMissing = fileMissing
            });
        }

        // Update ObservableCollection on UI thread (single Clear + batch Add)
        Novels.Clear();
        foreach (var item in items)
            Novels.Add(item);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
