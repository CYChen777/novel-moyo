using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using NovelMoyo.Models;
using NovelMoyo.ViewModels;


namespace NovelMoyo.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _vm;
    private HotkeyEntry? _selectedHotkey;
    private ListView? _chapterListView;
    private ListView? _bookmarkListView;

    public SettingsWindow(SettingsViewModel vm)
    {
        _vm = vm;
        InitializeComponent();
        DataContext = _vm;

        // Build all dynamic tab contents immediately in the constructor.
        BuildChapterTab();
        BuildBookmarkTab();

        Loaded += (_, _) => BuildColorSwatches();
    }

    private void BuildChapterTab()
    {
        var grid = new Grid { Margin = new Thickness(12) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var hint = new TextBlock
        {
            Text = "双击章节跳转阅读",
            FontSize = 11,
            Foreground = System.Windows.SystemColors.GrayTextBrush,
            Margin = new Thickness(0, 0, 0, 8)
        };
        Grid.SetRow(hint, 0);
        grid.Children.Add(hint);

        _chapterListView = new ListView
        {
            ItemsSource = _vm.Chapters,
            SelectedIndex = _vm.CurrentChapterIndex
        };
        Grid.SetRow(_chapterListView, 1);

        var itemStyle = new Style(typeof(ListViewItem));
        itemStyle.Setters.Add(new Setter(ListViewItem.PaddingProperty, new Thickness(4, 2, 4, 2)));
        _chapterListView.ItemContainerStyle = itemStyle;

        var gridView = new GridView();
        gridView.Columns.Add(new GridViewColumn { Header = "#", Width = 50, DisplayMemberBinding = new Binding("DisplayIndex") });
        gridView.Columns.Add(new GridViewColumn { Header = "章节标题", Width = 350, DisplayMemberBinding = new Binding("Title") });
        _chapterListView.View = gridView;

        _chapterListView.MouseDoubleClick += (_, _) => SelectChapter();
        _chapterListView.KeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Enter) { SelectChapter(); e.Handled = true; }
        };

        grid.Children.Add(_chapterListView);
        ChapterTab.Content = grid;
    }

    private void BuildBookmarkTab()
    {
        var grid = new Grid { Margin = new Thickness(12) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var hint = new TextBlock
        {
            Text = "双击书签跳转 · 快捷键 Ctrl+Alt+M 添加书签",
            FontSize = 11,
            Foreground = System.Windows.SystemColors.GrayTextBrush,
            Margin = new Thickness(0, 0, 0, 8)
        };
        Grid.SetRow(hint, 0);
        grid.Children.Add(hint);

        _bookmarkListView = new ListView
        {
            ItemsSource = _vm.Bookmarks
        };
        Grid.SetRow(_bookmarkListView, 1);

        var itemStyle = new Style(typeof(ListViewItem));
        itemStyle.Setters.Add(new Setter(ListViewItem.PaddingProperty, new Thickness(4, 2, 4, 2)));
        _bookmarkListView.ItemContainerStyle = itemStyle;

        var gridView = new GridView();
        gridView.Columns.Add(new GridViewColumn { Header = "章节", Width = 200, DisplayMemberBinding = new Binding("Note") });
        gridView.Columns.Add(new GridViewColumn
        {
            Header = "创建时间",
            Width = 180,
            DisplayMemberBinding = new Binding("CreatedAt") { StringFormat = "{}{0:yyyy-MM-dd HH:mm}" }
        });
        _bookmarkListView.View = gridView;

        _bookmarkListView.SelectionChanged += (_, _) =>
        {
            if (_bookmarkListView.SelectedItem is Bookmark bm)
                _vm.SelectedBookmark = bm;
        };
        _bookmarkListView.MouseDoubleClick += (_, _) => SelectBookmark();
        _bookmarkListView.KeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Enter) { SelectBookmark(); e.Handled = true; }
        };

        grid.Children.Add(_bookmarkListView);

        var deleteBtn = new Button
        {
            Content = "删除选中书签",
            Command = _vm.DeleteBookmarkCommand,
            Width = 100,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 8, 0, 0)
        };
        Grid.SetRow(deleteBtn, 2);
        grid.Children.Add(deleteBtn);

        BookmarkTab.Content = grid;
    }

    private void SelectChapter()
    {
        if (_chapterListView?.SelectedItem is Chapter chapter)
            _vm.SelectChapter(chapter.Index);
    }

    private void SelectBookmark()
    {
        if (_bookmarkListView?.SelectedItem is Bookmark bookmark)
            _vm.SelectBookmark(bookmark);
    }

    private void BuildColorSwatches()
    {
        var fontColors = new[] { "#FFFFFF", "#CCCCCC", "#AAAAAA", "#FFCC00", "#66FF66", "#66CCFF", "#FF9966", "#CC99FF" };
        var bgColors = new[] { "#000000", "#1A1A1A", "#333333", "#003300", "#332200", "#002233", "#220033", "#330000" };

        foreach (var color in fontColors)
        {
            var rect = new System.Windows.Shapes.Rectangle
            {
                Width = 24, Height = 24,
                Fill = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color)),
                Margin = new Thickness(2),
                Stroke = System.Windows.SystemColors.ControlDarkBrush,
                StrokeThickness = 1,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            rect.MouseLeftButtonDown += (_, _) => _vm.FontColor = color;
            FontColorPanel.Children.Add(rect);
        }

        var fontCustomBtn = new System.Windows.Controls.Button
        {
            Content = "自定义...",
            FontSize = 11,
            Padding = new Thickness(4, 1, 4, 1),
            Margin = new Thickness(4, 2, 0, 0),
            Cursor = System.Windows.Input.Cursors.Hand
        };
        fontCustomBtn.Click += (_, _) => PickCustomColor(c => _vm.FontColor = c, _vm.FontColor);
        FontColorPanel.Children.Add(fontCustomBtn);

        foreach (var color in bgColors)
        {
            var rect = new System.Windows.Shapes.Rectangle
            {
                Width = 24, Height = 24,
                Fill = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color)),
                Margin = new Thickness(2),
                Stroke = System.Windows.SystemColors.ControlDarkBrush,
                StrokeThickness = 1,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            rect.MouseLeftButtonDown += (_, _) => _vm.BackgroundColor = color;
            BgColorPanel.Children.Add(rect);
        }

        var bgCustomBtn = new System.Windows.Controls.Button
        {
            Content = "自定义...",
            FontSize = 11,
            Padding = new Thickness(4, 1, 4, 1),
            Margin = new Thickness(4, 2, 0, 0),
            Cursor = System.Windows.Input.Cursors.Hand
        };
        bgCustomBtn.Click += (_, _) => PickCustomColor(c => _vm.BackgroundColor = c, _vm.BackgroundColor);
        BgColorPanel.Children.Add(bgCustomBtn);
    }

    private void PickCustomColor(Action<string> applyColor, string currentColor)
    {
        using var dialog = new System.Windows.Forms.ColorDialog();
        dialog.FullOpen = true;
        try
        {
            var c = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(currentColor);
            dialog.Color = System.Drawing.Color.FromArgb(c.R, c.G, c.B);
        }
        catch { }

        var win32Owner = new Win32WindowOwner(this);
        if (dialog.ShowDialog(win32Owner) == System.Windows.Forms.DialogResult.OK)
        {
            var hex = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
            applyColor(hex);
        }
    }

    private class Win32WindowOwner(System.Windows.Window owner) : System.Windows.Forms.IWin32Window
    {
        public IntPtr Handle => new System.Windows.Interop.WindowInteropHelper(owner).Handle;
    }

    private void HotkeyListView_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (HotkeyListView.SelectedItem is HotkeyEntry entry)
        {
            _selectedHotkey = entry;
            HotkeyInput.Text = entry.KeyCombination;
            HotkeyHintText.Text = $"当前: {entry.DisplayName}";
        }
    }

    private void HotkeyInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (_selectedHotkey is null) return;
        e.Handled = true;

        var modifiers = System.Windows.Input.Keyboard.Modifiers;
        var key = e.Key == System.Windows.Input.Key.System ? e.SystemKey : e.Key;

        if (key is System.Windows.Input.Key.LeftShift or System.Windows.Input.Key.RightShift
            or System.Windows.Input.Key.LeftCtrl or System.Windows.Input.Key.RightCtrl
            or System.Windows.Input.Key.LeftAlt or System.Windows.Input.Key.RightAlt
            or System.Windows.Input.Key.LWin or System.Windows.Input.Key.RWin)
            return;

        var combo = FormatKeyCombo(modifiers, key);

        var conflict = _vm.HotkeyEntries.FirstOrDefault(h =>
            h.KeyCombination == combo && h.ActionName != _selectedHotkey.ActionName);
        if (conflict is not null)
        {
            HotkeyHintText.Text = $"⚠ 冲突: {conflict.DisplayName} 也使用了 {combo}";
            HotkeyHintText.Foreground = System.Windows.Media.Brushes.OrangeRed;
            return;
        }

        _selectedHotkey.KeyCombination = combo;
        HotkeyInput.Text = combo;
        HotkeyHintText.Text = $"当前: {_selectedHotkey.DisplayName}";
        HotkeyHintText.Foreground = System.Windows.Media.Brushes.Gray;
        HotkeyListView.Items.Refresh();
    }

    private static string FormatKeyCombo(System.Windows.Input.ModifierKeys modifiers, System.Windows.Input.Key key)
    {
        var parts = new List<string>();
        if (modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(System.Windows.Input.ModifierKeys.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(System.Windows.Input.ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(key.ToString());
        return string.Join("+", parts);
    }

    private void HotkeyInput_GotFocus(object sender, RoutedEventArgs e)
    {
        // Only show placeholder if no key has been pressed yet for this selection
        if (_selectedHotkey is not null && HotkeyInput.Text == _selectedHotkey.KeyCombination)
            HotkeyInput.Text = "按下新快捷键...";
    }

    private void HotkeyInput_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_selectedHotkey is not null)
            HotkeyInput.Text = _selectedHotkey.KeyCombination;
    }
}
