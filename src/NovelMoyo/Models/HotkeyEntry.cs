namespace NovelMoyo.Models;

/// <summary>
/// Represents a hotkey binding for display in the settings UI.
/// </summary>
public class HotkeyEntry
{
    public string ActionName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string KeyCombination { get; set; } = string.Empty;
}
