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

1. **Models** (`Models/`): Plain POCOs, no `INotifyPropertyChanged`. Data transfer objects only. All use file-scoped namespaces (`namespace NovelMoyo.Models;`).
2. **Services** (`Services/`): Business logic, persistence, system integration. No interfaces except for parsers (`INovelParser`). Manually instantiated in `App.OnStartup`.
3. **Parsers** (`Services/NovelParser/`): File format parsing. Interface `INovelParser` with factory pattern `NovelParserFactory`.
4. **No DI container** — all wiring is manual in `App.xaml.cs`.
5. **File-per-class** — one class per `.cs` file, filename matches class name.
6. **No `#region` directives** anywhere.
