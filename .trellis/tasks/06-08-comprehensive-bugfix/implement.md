# Implementation Notes - v1.0.4 Comprehensive Bugfix Pass

## Scope

Fixed the high-impact issues found during the full project audit:

- Settings could be overwritten on exit by stale startup settings.
- Start-minimized-to-tray could skip HWND creation, preventing hotkey and mouse hook registration.
- Chapter pixel offsets did not include separator height, causing long-read chapter drift and restore/bookmark offset drift.
- Pending/downloading tasks were discarded on restart despite the documented recovery behavior.
- Online search waited for all chapter-count requests before showing results.
- Async command exceptions could escape to the global exception handler.
- Hotkey parse/register failures were silent.
- Installer script depended on an absolute local path.

## Code Changes

- `App.xaml.cs`
  - Calls `WindowInteropHelper.EnsureHandle()` after constructing the main window.
  - Reloads latest settings in `OnExit` before updating `LastNovelId`.

- `MainWindow.xaml.cs`
  - Tracks failed hotkeys and shows a toast when parse/register fails.
  - Adds `MeasureRenderedTextHeight()` and remeasures chapter pixel offsets with separator height included.
  - Remeasures offsets after append/prepend/trim instead of manually maintaining partially derived pixel values.

- `SearchService.cs` / `OnlineBookStoreViewModel.cs`
  - Search now returns deduped results immediately.
  - Chapter counts load in the background with a concurrency limit and dispatcher-safe UI updates.
  - Async commands now catch and report unexpected exceptions.

- `DownloadService.cs`
  - Extracts `QueueDownloadTask()`.
  - Loads all persisted tasks.
  - Automatically requeues `Pending` / `Downloading` tasks when the source is still enabled.
  - Marks tasks failed when their source no longer exists.

- `installer.iss`, `README.md`, `CHANGELOG.md`, `NovelMoyo.csproj`
  - Version bumped to `1.0.4`.
  - Installer paths changed to relative paths.
  - Documentation now describes task recovery accurately as restart requeueing, not chapter-level breakpoint resume.

## Validation

- `dotnet build D:\projects\novel-moyo\NovelMoyo.sln --no-restore`
  - 0 warnings, 0 errors
- `dotnet build D:\projects\novel-moyo\NovelMoyo.sln -c Release --no-restore`
  - 0 warnings, 0 errors
- Static confirmation:
  - No hardcoded installer source path remains in `installer.iss`.
  - Version references updated to `1.0.4`.
  - Key fix points present: `EnsureHandle`, `latestSettings`, `QueueDownloadTask`, `FindEnabledSourceByTitle`, `LoadChapterCountsAsync`, hotkey failure toast.
