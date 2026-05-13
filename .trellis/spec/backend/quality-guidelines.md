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
- **IDisposable:** Implement on services that hold unmanaged resources (`AutoScrollService`, `HotkeyService`, `SearchService`, `DownloadService`).

---

## HttpClient Lifecycle Pattern

Services using `HttpClient` must:
1. Use `SocketsHttpHandler` with explicit TLS: `SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13`
2. Set `HttpClient.Timeout = InfiniteTimeSpan` — rely on `CancellationToken` for timeout
3. Implement `IDisposable`, disposing both `_httpClient` and `_handler`
4. Register disposal in `App.OnExit`

```csharp
private readonly SocketsHttpHandler _handler = new()
{
    SslOptions = new SslClientAuthenticationOptions
    {
        EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
    }
};
private readonly HttpClient _httpClient;
// ctor: _httpClient = new HttpClient(_handler) { Timeout = Timeout.InfiniteTimeSpan };
// Dispose: _httpClient.Dispose(); _handler.Dispose();
```

---

## Concurrency Pattern (SemaphoreSlim)

For parallel downloads with bounded concurrency:
- Use `SemaphoreSlim(maxCount, maxCount)` — one per concern (e.g. `_chapterSemaphore` for chapter I/O)
- Never share a semaphore between outer task scheduling and inner work
- Use `Task.WhenAll` for parallel execution, not fire-and-forget
- Track active tasks in `ConcurrentDictionary<string, Task>` for graceful shutdown

---

## Async Command Pattern

Use `AsyncRelayCommand` (defined in `OnlineBookStoreViewModel.cs`) for async operations:
- Implements `ICommand` with `_isExecuting` guard to prevent re-entry
- Uses `async void Execute` (WPF ICommand requirement) with top-level try/catch
- Reports errors via ViewModel status properties, never `MessageBox.Show` in commands

---

## Anti-Patterns to Avoid

1. **Do not add a DI container** — the project is small enough for manual wiring.
2. **Do not add an MVVM framework** — hand-rolled `RelayCommand` and `INotifyPropertyChanged` are sufficient.
3. **Do not add async/await** for local file I/O unless the developer requests it (current pattern is synchronous). Network I/O (`HttpClient`) uses async/await with `CancellationToken`.
4. **Do not add interfaces on services** — only parsers need abstraction (`INovelParser`).
5. **Do not add unit tests** unless the developer requests them.
6. **Keep bare catch blocks** for non-critical DataStore operations — this is intentional.

---

## Build & Release

### Build Commands

```bash
# Debug build
dotnet build src/NovelMoyo/NovelMoyo.csproj

# Run
dotnet run --project src/NovelMoyo/NovelMoyo.csproj

# Publish: framework-dependent (~2.5MB, needs .NET 8 Runtime)
dotnet publish src/NovelMoyo/NovelMoyo.csproj -c Release -r win-x64 -o publish/fdd

# Publish: self-contained (~155MB, no extra install)
dotnet publish src/NovelMoyo/NovelMoyo.csproj -c Release -r win-x64 --self-contained -o publish/scd
```

### Release Convention

- **GitHub repo:** `https://github.com/CYChen777/novel-moyo`
- **Release format:** `v{major}.{minor}.{patch}` (e.g., `v1.0.0`)
- **Release assets:** Three files per release:
  - `NovelMoyo-Setup.exe` — Inno Setup installer (~2.7MB)
  - `NovelMoyo-v{version}-fdd.zip` — framework-dependent (~962KB zipped)
  - `NovelMoyo-v{version}-scd.zip` — self-contained (~63MB zipped)
- **Git user:** `Yuchen <1596877162@qq.com>`

### Gotcha: Separate Output Directories

> **Warning:** FDD and SCD builds MUST use different `-o` output directories (`publish/fdd` vs `publish/scd`). If built to the same default directory, the second build overwrites the first.

### .gitignore

The following are excluded from version control:
- `bin/`, `obj/` — build intermediates
- `publish/` — build output (zip files for release)
- `*.exe`, `*.dll`, `*.pdb` — binaries
