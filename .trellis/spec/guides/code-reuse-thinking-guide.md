# Code Reuse Thinking Guide

> Project: NovelMoyo — patterns to check before writing new code

---

## Before Creating a New Service

1. **Check existing services** — does `DataStore`, `BookshelfService`, or `BookmarkService` already handle similar logic?
2. **Check if a ViewModel method** could handle it instead of a new service.
3. **No DI container** — adding a new service means wiring it manually in `App.OnStartup`.

## Before Creating a New Model

1. **Check existing models** in `Models/` — `AppSettings`, `Bookmark`, `BookshelfEntry`, `Chapter`, `HotkeyEntry`, `Novel`, `ReadingProgress`.
2. **Can an existing model be extended?** Add a property instead of creating a new class.
3. **String defaults** — all string properties must default to `string.Empty`.

## Before Adding a New XAML Resource

1. **Check existing themes** — all three themes (`DarkTheme`, `GreenTheme`, `WarmTheme`) must define the same keys.
2. **Check `App.xaml`** for globally registered converters.
3. **New converters** go in `Converters/` and must be registered in `App.xaml`.

## Before Writing a New Regex

1. **Use `[GeneratedRegex]`** (source-generated regex) — see `TxtParser.cs` for the pattern.
2. **Check `EpubParser.cs`** — it has HTML-stripping regex that might be reusable.

## Before Adding P/Invoke

1. **Check existing declarations** — `App.xaml.cs` (DestroyIcon), `MainWindow.xaml.cs` (GetWindowLong, SetWindowLong, mouse hook), `HotkeyService.cs` (RegisterHotKey, UnregisterHotKey).
2. **Consider centralizing** — but don't refactor existing code unless asked.

## Known Duplication

- **Window creation methods** exist in both `App.xaml.cs` and `MainWindow.xaml.cs` — this is tech debt, don't add more.
- **Hotkey registration** is hardcoded in MainWindow but displayed from settings — don't fix unless asked.
