# Cross-Layer Thinking Guide

> Project: NovelMoyo — data flow across layers

---

## Layer Map

```
XAML (Views/MainWindow.xaml)
  ↕ binding / imperative
Code-behind (Views/MainWindow.xaml.cs)
  ↕ direct access / events
ViewModel (ViewModels/MainViewModel.cs)
  ↕ method calls
Services (Services/BookshelfService.cs, etc.)
  ↕ read/write
DataStore (Services/DataStore.cs)
  ↕ File I/O
JSON files (%APPDATA%/StealthReader/)
```

---

## Common Cross-Layer Scenarios

### Loading a Novel

```
User drops file → MainWindow.xaml.cs (DragDrop handler)
  → _vm.LoadNovel(filePath)
    → NovelParserFactory.GetParser(ext).Parse(filePath) → Novel
    → BookshelfService.AddNovel(entry)
    → DataStore.SaveBookshelf()
    → DataStore.LoadProgress(novelId) → ReadingProgress
  → MainWindow reads _vm.SavedScrollRatio, _vm.Chapters
  → MainWindow progressive loading (code-behind logic)
```

**Key insight:** The ViewModel orchestrates, but MainWindow.xaml.cs does the heavy UI work.

### Saving Reading Progress

```
ScrollViewer.ScrollChanged → MainWindow.xaml.cs
  → debounced 3s → calculate chapter index + scroll ratio
  → _vm.UpdateReadingProgress(chapterIndex, charOffset, scrollRatio)
    → BookmarkService.SaveProgressWithBookmarks()
      → DataStore.SaveProgress()
```

**Key insight:** Progress calculation happens in code-behind (it needs ScrollViewer access), but persistence goes through ViewModel → Service → DataStore.

### Theme Switching

```
SettingsViewModel.Save() → DataStore.SaveSettings()
  → MainWindow receives PropertyChanged notification
  → Load new ResourceDictionary
  → Application.Current.Resources.MergedDictionaries[0] = newTheme
```

**Key insight:** SettingsViewModel doesn't know about themes. MainWindow watches for setting changes and applies them imperatively.

---

## Rules

1. **ViewModels never reference Views** — no `Window`, `Control`, or `UIElement` types.
2. **Services never reference ViewModels** — one-directional dependency.
3. **DataStore never throws** — always returns safe defaults.
4. **Code-behind can access ViewModel freely** — this is hybrid MVVM by design.
5. **Cross-window communication** uses `Action<T>` events, not shared state.
