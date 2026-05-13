namespace NovelMoyo.Models;

public class SearchResult
{
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string LatestChapter { get; set; } = string.Empty;
    public string SourceName { get; set; } = string.Empty;
    public int TotalChapters { get; set; }
    public BookSource Source { get; set; } = new();
}
