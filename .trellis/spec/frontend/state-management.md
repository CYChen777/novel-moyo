# State Management Guidelines

> Project: NovelMoyo — state management patterns

---

## State Categories

### 1. App Settings (`AppSettings` POCO)
- **Location:** `%APPDATA%/StealthReader/settings.json`
- **Loaded:** Once at startup in `DataStore` constructor.
- **Saved:** On explicit save (Settings window) and on app exit.
- **In-memory:** Held by `DataStore`, accessed by ViewModels via constructor injection.

### 2. Reading Progress (`ReadingProgress` per novel)
- **Location:** `%APPDATA%/StealthReader/progress/{novelId}.json`
- **Loaded:** When a novel is opened via `BookshelfService.GetNovel()`.
- **Saved:** Debounced every 3 seconds during scroll, and on window close.
- **In-memory:** Held by `MainViewModel` as current state.

### 3. Bookshelf (`List<BookshelfEntry>`)
- **Location:** `%APPDATA%/StealthReader/bookshelf.json`
- **Loaded:** Once at startup in `BookshelfService` constructor.
- **In-memory:** Held by `BookshelfService._entries` list, exposed via `BookshelfViewModel.Novels` (ObservableCollection).

### 4. Transient UI State
- **ViewModels** hold current session state (chapters, font size, selected novel, etc.).
- **MainWindow.xaml.cs** holds progressive loading state (`_loadedChapterStart`, `_loadedChapterEnd`, `_chapterCharOffsets`).
- **Not persisted** — lost on app restart.

---

## ViewModel State Pattern

```csharp
private int _fontSize = 16;
public int FontSize
{
    get => _fontSize;
    set { _fontSize = value; OnPropertyChanged(); }
}
```

- Private backing field with inline default.
- Setter calls `OnPropertyChanged()` with `[CallerMemberName]`.
- `OnPropertyChanged()` uses `nameof()` implicitly via `CallerMemberName`.

---

## Rules

1. **Settings are POCOs** — no `INotifyPropertyChanged` on model classes.
2. **ViewModels are bindable** — implement `INotifyPropertyChanged` manually.
3. **ObservableCollection** for lists bound to UI (chapters, novels, hotkey entries).
4. **No global state** — each ViewModel holds its own state, injected services provide shared data.
5. **Debounced saves** for frequently-changing state (reading progress: 3-second debounce).
6. **Atomic writes** for all persistence (write-to-temp + `File.Move`).
