using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace NovelMoyo.Models;

public class AppSettings
{
    // Appearance
    public double Opacity { get; set; } = 0.3;
    public int FontSize { get; set; } = 16;
    public string FontColor { get; set; } = "#FFFFFF";
    public string BackgroundColor { get; set; } = "#000000";
    public double LineSpacing { get; set; } = 1.5;
    public double ParagraphSpacing { get; set; } = 8;
    public string Theme { get; set; } = "Dark";

    // Reading
    public int AutoScrollSpeed { get; set; } = 3; // 1-5
    public double ScrollSpeed { get; set; } = 1.0; // 0.5-4.5, multiplier for mouse wheel
    public bool StartWithLastNovel { get; set; } = true;
    public string? LastNovelId { get; set; }

    // System
    public bool AutoStart { get; set; } = false;
    public bool StartMinimizedToTray { get; set; } = false;

    // Hotkeys
    public Dictionary<string, string> Hotkeys { get; set; } = new()
    {
        ["ToggleVisibility"] = "Ctrl+Alt+H",
        ["ToggleLock"] = "Ctrl+Alt+L",
        ["ToggleAutoScroll"] = "Ctrl+Alt+S",
        ["SpeedUp"] = "Ctrl+Alt+Up",
        ["SpeedDown"] = "Ctrl+Alt+Down",
        ["PrevChapter"] = "Ctrl+Alt+Left",
        ["NextChapter"] = "Ctrl+Alt+Right",
        ["TogglePassthrough"] = "Ctrl+Alt+P",
        ["ToggleTopmost"] = "Ctrl+Alt+T",
        ["OpenSettings"] = "Ctrl+Alt+OemComma",
        ["OpenBookshelf"] = "Ctrl+Alt+B",
        ["AddBookmark"] = "Ctrl+Alt+M"
    };

    // Window state
    public double WindowLeft { get; set; } = 100;
    public double WindowTop { get; set; } = 100;
    public double WindowWidth { get; set; } = 500;
    public double WindowHeight { get; set; } = 400;
}
