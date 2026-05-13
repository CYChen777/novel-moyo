using System;

namespace NovelMoyo.Models;

public class BookshelfEntry
{
    public string NovelId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public DateTime AddedAt { get; set; }
    public DateTime LastReadAt { get; set; }
}
