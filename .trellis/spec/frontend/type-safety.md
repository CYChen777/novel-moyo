# Type Safety Guidelines

> Project: NovelMoyo — C# type conventions

---

## Nullable Reference Types

Nullable reference types are enabled project-wide. Follow these patterns:

```csharp
// Non-nullable: initialize with default
public string Title { get; set; } = string.Empty;

// Nullable: use ? for genuinely optional values
public string? LastNovelId { get; set; }

// Method returns: use ? for "not found"
public Novel? GetNovel(string filePath) // returns null on failure
```

---

## String Defaults

All string properties on Models default to `string.Empty`, not `null`:

```csharp
// CORRECT
public string FilePath { get; set; } = string.Empty;

// WRONG — don't do this
public string FilePath { get; set; } = null!;
```

---

## ID Generation

Use 8-char hex GUID with C# 12 range indexer:

```csharp
public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
```

---

## Format Strings

File format is a raw string, not an enum:

```csharp
public string Format { get; set; } = "txt"; // or "epub"
```

This is a known tech debt item — do not refactor to enum unless the developer requests it.

---

## Win32 Interop Types

For P/Invoke declarations, use `IntPtr` and `nint` interchangeably. Current codebase uses `IntPtr`:

```csharp
[DllImport("user32.dll")]
private static extern bool DestroyIcon(IntPtr handle);
```

---

## Rules

1. **Never use `null!`** — either use `= string.Empty` or make the type nullable with `?`.
2. **Use `is not null`** for null checks (matches codebase style).
3. **Use C# 12 features** where they simplify code: primary constructors, collection expressions, range indexer.
4. **`[GeneratedRegex]`** for any new regex patterns (performance, compile-time validation).
5. **`Math.Clamp()`** for numeric validation in ViewModel setters.
