namespace NovelMoyo.Views.Controls;

public partial class ReadingStatusBar : System.Windows.Controls.UserControl
{
    public static readonly System.Windows.DependencyProperty ChapterTitleProperty =
        System.Windows.DependencyProperty.Register(
            nameof(ChapterTitle),
            typeof(string),
            typeof(ReadingStatusBar),
            new System.Windows.PropertyMetadata(string.Empty));

    public static readonly System.Windows.DependencyProperty ChapterInfoProperty =
        System.Windows.DependencyProperty.Register(
            nameof(ChapterInfo),
            typeof(string),
            typeof(ReadingStatusBar),
            new System.Windows.PropertyMetadata(string.Empty));

    public string ChapterTitle
    {
        get => (string)GetValue(ChapterTitleProperty);
        set => SetValue(ChapterTitleProperty, value);
    }

    public string ChapterInfo
    {
        get => (string)GetValue(ChapterInfoProperty);
        set => SetValue(ChapterInfoProperty, value);
    }

    public ReadingStatusBar()
    {
        InitializeComponent();
    }
}
