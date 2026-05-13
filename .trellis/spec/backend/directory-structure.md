# Backend Directory Structure

> Project: NovelMoyo — WPF .NET 8.0 desktop novel reader
> Scope: Models, Services, DataStore, Parsers (non-UI logic)

---

## Project Root

```
D:/projects/novel-moyo/
├── NovelMoyo.sln
├── README.md                  # Bilingual README (Chinese + English)
├── .gitignore                 # Excludes bin/, obj/, publish/, etc.
├── installer.iss              # Inno Setup installer script
├── publish/                   # Build output (git-ignored)
│   ├── fdd/                   # Framework-dependent build
│   └── scd/                   # Self-contained build
└── src/NovelMoyo/             # Single project
    ├── NovelMoyo.csproj
    ├── App.xaml / App.xaml.cs  # Composition root
    ├── Models/                 # POCO data models
    ├── Services/               # Business logic + persistence
    │   ├── BookSource/         # Book source config + search + parsing
    │   ├── Download/           # Download queue + chapter fetching
    │   └── NovelParser/        # Parser interface + implementations
    ├── ViewModels/             # MVVM ViewModels (shared with frontend)
    ├── Views/                  # XAML windows + controls
    │   └── Controls/           # Custom UserControls
    ├── Converters/             # IValueConverter implementations
    └── Resources/
        ├── Icons/
        └── Themes/             # XAML resource dictionaries
```

---

## Rules

1. **Models** (`Models/`): Data transfer objects. Most are plain POCOs. Models that are directly data-bound in WPF views (e.g. `SearchResult` in DataGrid, `DownloadTaskInfo` in ListView) may implement `INotifyPropertyChanged` — this is an exception to the POCO rule.
2. **Services** (`Services/`): Business logic, persistence, system integration. No interfaces except for parsers (`INovelParser`). Manually instantiated in `App.OnStartup`. Services holding `HttpClient` or unmanaged resources must implement `IDisposable`.
3. **Parsers** (`Services/NovelParser/`): File format parsing. Interface `INovelParser` with factory pattern `NovelParserFactory`.
4. **No DI container** — all wiring is manual in `App.xaml.cs`.
5. **File-per-class** — one class per `.cs` file, filename matches class name. Exceptions: (a) tightly coupled config DTO groups in `Models/` may share a file (e.g. `BookSource.cs` with 9 config classes), (b) generic + non-generic variants of the same type (e.g. `RelayCommand` + `RelayCommand<T>`).
6. **No `#region` directives** anywhere.
