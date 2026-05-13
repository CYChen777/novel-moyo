# Data Persistence Guidelines

> Project: NovelMoyo — no database, JSON file-based persistence

---

## Storage Architecture

All data is stored as JSON files under `%APPDATA%/StealthReader/` (legacy app name).

```
%APPDATA%/StealthReader/
├── settings.json              # AppSettings POCO
├── bookshelf.json             # List<BookshelfEntry>
└── progress/
    └── {novelId}.json         # ReadingProgress per novel
```

---

## DataStore Pattern

**File:** `src/NovelMoyo/Services/DataStore.cs`

- Uses `System.Text.Json` with `WriteIndented = true`.
- **Atomic writes**: writes to `.tmp` file first, then `File.Move` with overwrite. Cleans up temp on failure.
- All load methods **never throw** — return defaults on failure (empty lists, null, new `AppSettings`).
- `DeleteProgress` uses best-effort silent catch.

```csharp
// Example: atomic write pattern
private void WriteJsonAtomically<T>(string path, T data)
{
    var tmp = path + ".tmp";
    File.WriteAllText(tmp, JsonSerializer.Serialize(data, _jsonOptions));
    File.Move(tmp, path, overwrite: true);
}
```

---

## Model Conventions

- All string properties default to `string.Empty` (never null).
- IDs generated as `Guid.NewGuid().ToString("N")[..8]` (8-char hex, C# 12 range indexer).
- Timestamps use `DateTime.Now`.
- `Format` field is a raw string (`"txt"` or `"epub"`), not an enum.

---

## Rules

1. **Never throw from DataStore** — always return safe defaults.
2. **Use atomic writes** for all file persistence (write-to-temp + move).
3. **No async I/O** — all file operations are synchronous on the UI thread (current state; improvement opportunity).
4. **Per-novel progress** stored in separate files keyed by novel ID.
