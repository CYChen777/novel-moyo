# Backend Quality Guidelines

> Project: NovelMoyo — code standards and patterns

---

## Language Features

- **Target:** .NET 8.0 (net8.0-windows)
- **Nullable reference types:** Enabled project-wide. Use `?` for nullable types.
- **Implicit usings:** Enabled. Only add explicit `using` for non-default namespaces.
- **File-scoped namespaces:** Use `namespace NovelMoyo.X;` (not block-scoped).
- **C# 12 features used:** Primary constructors, collection expressions (`[]`), range indexer (`[..8]`), `[GeneratedRegex]`.

---

## Naming Conventions

| Element | Convention | Example |
|---------|-----------|---------|
| Namespace | PascalCase, file-scoped | `namespace NovelMoyo.Services;` |
| Class | PascalCase | `BookshelfService` |
| Interface | `I` prefix + PascalCase | `INovelParser` |
| Private field | `_camelCase` | `_entries`, `_disposed` |
| Local variable | camelCase | `novelId`, `parser` |
| Property | PascalCase | `FontSize`, `Chapters` |
| Method | PascalCase | `LoadNovel()`, `GetParser()` |
| Parameter | camelCase | `filePath`, `settings` |
| Constant | PascalCase or SCREAMING_CASE (Win32) | `MinSpeed`, `WM_HOTKEY` |
| Event | `On` prefix | `OnScrollTick`, `OnNovelSelected` |

---

## Code Organization

- **No `#region` directives** — the codebase does not use them.
- **File-per-class** — one class per file, filename matches class name.
- **Partial classes** only for source-generated code (`[GeneratedRegex]`).
- **No shared ViewModel base class** — each ViewModel implements `INotifyPropertyChanged` manually.

---

## Service Patterns

- **No DI container** — all services manually created in `App.OnStartup`.
- **Service lifetime:** Singleton (created once, live for app lifetime).
- **Child window ViewModels:** Created fresh each time the window opens.
- **IDisposable:** Implement on services that hold unmanaged resources (`AutoScrollService`, `HotkeyService`).

---

## Anti-Patterns to Avoid

1. **Do not add a DI container** — the project is small enough for manual wiring.
2. **Do not add an MVVM framework** — hand-rolled `RelayCommand` and `INotifyPropertyChanged` are sufficient.
3. **Do not add async/await** for file I/O unless the developer requests it (current pattern is synchronous).
4. **Do not add interfaces on services** — only parsers need abstraction (`INovelParser`).
5. **Do not add unit tests** unless the developer requests them.
6. **Keep bare catch blocks** for non-critical DataStore operations — this is intentional.
