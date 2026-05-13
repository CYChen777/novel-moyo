using System;

namespace NovelMoyo.Models;

public class Bookmark
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public int ChapterIndex { get; set; }
    public int CharOffsetInChapter { get; set; }
    public double ScrollRatio { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
