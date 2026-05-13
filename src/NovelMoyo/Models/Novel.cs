using System;
using System.Collections.Generic;

namespace NovelMoyo.Models;

public class Novel
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Title { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty; // "txt" or "epub"
    public List<Chapter> Chapters { get; set; } = [];
    public DateTime AddedAt { get; set; } = DateTime.Now;
    public DateTime LastReadAt { get; set; } = DateTime.Now;
}
