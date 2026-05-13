using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NovelMoyo.Models;

public class SearchResult : INotifyPropertyChanged
{
    private string _title = string.Empty;
    private string _author = string.Empty;
    private string _url = string.Empty;
    private string _latestChapter = string.Empty;
    private string _sourceName = string.Empty;
    private int _totalChapters;

    public string Title
    {
        get => _title;
        set { _title = value; OnPropertyChanged(); }
    }

    public string Author
    {
        get => _author;
        set { _author = value; OnPropertyChanged(); }
    }

    public string Url
    {
        get => _url;
        set { _url = value; OnPropertyChanged(); }
    }

    public string LatestChapter
    {
        get => _latestChapter;
        set { _latestChapter = value; OnPropertyChanged(); }
    }

    public string SourceName
    {
        get => _sourceName;
        set { _sourceName = value; OnPropertyChanged(); }
    }

    public int TotalChapters
    {
        get => _totalChapters;
        set { _totalChapters = value; OnPropertyChanged(); }
    }

    public BookSource Source { get; set; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
