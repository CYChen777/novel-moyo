using System.IO;
using System.Windows;
using NovelMoyo.ViewModels;

namespace NovelMoyo.Views;

public partial class BookshelfWindow : Window
{
    private readonly BookshelfViewModel _vm;

    public BookshelfWindow(BookshelfViewModel vm)
    {
        _vm = vm;
        DataContext = _vm;
        InitializeComponent();

        Drop += OnFileDrop;
        DragOver += OnDragOver;

        // Auto-refresh bookshelf when a download completes
        OnlineBookStoreViewModel.BookshelfRefreshRequested += OnBookshelfRefreshRequested;
        Closed += (_, _) => OnlineBookStoreViewModel.BookshelfRefreshRequested -= OnBookshelfRefreshRequested;
    }

    private void OnBookshelfRefreshRequested(object? sender, System.EventArgs e)
    {
        _vm.RefreshList();
    }

    /// <summary>
    /// Exposes the ViewModel so callers can subscribe to events (e.g., OnNovelSelected).
    /// </summary>
    public BookshelfViewModel ViewModel => _vm;

    private void NovelList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_vm.SelectedNovel is not null)
        {
            _vm.OpenNovelCommand.Execute(null);
        }
    }

    private void NovelList_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter && _vm.SelectedNovel is not null)
        {
            _vm.OpenNovelCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnlineBookStore_Click(object sender, RoutedEventArgs e)
    {
        if (Application.Current is App app)
        {
            app.OpenOnlineBookStore();
        }
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
            e.Effects = DragDropEffects.Copy;
        else
            e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private void OnFileDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
            if (files?.Length > 0)
            {
                var ext = Path.GetExtension(files[0]).ToLowerInvariant();
                if (ext is ".txt" or ".epub")
                {
                    _vm.ImportFileFromPath(files[0]);
                }
            }
        }
    }
}
