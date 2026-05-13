namespace NovelMoyo.Models;

public class Chapter
{
    public int Index { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int CharOffset { get; set; } // character offset from start of novel

    /// <summary>
    /// 1-based display index for UI.
    /// </summary>
    public int DisplayIndex => Index + 1;
}
