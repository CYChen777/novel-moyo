# Error Handling Guidelines

> Project: NovelMoyo — current patterns (document reality, not ideals)

---

## Current Patterns

### 1. Bare catch blocks (most common)

```csharp
// DataStore: silent fallback to defaults
catch { return new AppSettings(); }
catch { return []; }
catch { /* best effort */ }
```

### 2. User-facing errors via MessageBox

```csharp
// App.xaml.cs — top-level startup error
catch (Exception ex)
{
    MessageBox.Show($"启动失败：{ex.Message}", "NovelMoyo", MessageBoxButton.OK, MessageBoxImage.Error);
    Shutdown();
}

// DispatcherUnhandledException handler
MessageBox.Show($"发生未处理的异常：{ex.Message}", "NovelMoyo", MessageBoxButton.OK, MessageBoxImage.Error);
args.Handled = true;
```

### 3. Silent null returns

```csharp
// BookshelfService.GetNovel()
catch { return null; }
```

---

## Rules

1. **DataStore load methods** must never throw — return defaults on any failure.
2. **User-facing errors** shown via `System.Windows.MessageBox.Show()` with Chinese messages.
3. **No logging framework** is in use. Debug output via `System.Diagnostics.Debug.WriteLine()` if needed.
4. **DispatcherUnhandledException** is the global safety net — shows MessageBox, marks as handled.
5. **Bare `catch { }`** is acceptable for non-critical operations (tray icon cleanup, progress save on exit).
6. **Do NOT add try/catch around every method** — only catch where failure is expected or recovery is possible.
7. **Async command errors** — report via ViewModel status properties (e.g. `StatusMessage`), never `MessageBox.Show`. All non-silent catch blocks must include `Debug.WriteLine`.
8. **User-facing error messages** must be in Chinese. Wrap raw exception messages: `$"内部错误: {ex.Message}"` instead of passing `ex.Message` directly.
