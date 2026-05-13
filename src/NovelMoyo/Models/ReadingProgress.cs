using System;
using System.Collections.Generic;

namespace NovelMoyo.Models;

public class ReadingProgress
{
    public string NovelId { get; set; } = string.Empty;
    public int ChapterIndex { get; set; }
    public int TotalChapters { get; set; }
    public int ScrollOffset { get; set; }
    /// <summary>
    /// How far through the current chapter the user was (0.0-1.0).
    /// Used for accurate scroll restoration since absolute pixel offset is unreliable.
    /// </summary>
    public double ChapterScrollRatio { get; set; }
    public List<Bookmark> Bookmarks { get; set; } = [];
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
