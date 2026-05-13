using System;

namespace NovelMoyo.ViewModels;

public class BookshelfNovelItem
{
    public required string NovelId { get; init; }
    public required string Title { get; init; }
    public required string Format { get; init; }
    public required DateTime LastReadAt { get; init; }
    public string ProgressText { get; init; } = "暂无进度";
    public bool FileMissing { get; init; }
}
