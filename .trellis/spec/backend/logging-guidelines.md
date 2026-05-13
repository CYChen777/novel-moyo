# Logging Guidelines

> Project: NovelMoyo — no logging framework in use

---

## Current State

The project has **no logging framework**. There is no `ILogger`, no Serilog, no NLog, no `Debug.WriteLine` calls in production code.

Error information is surfaced via:
- `MessageBox.Show()` for user-visible errors
- Silent catch blocks for non-critical failures
- `DispatcherUnhandledException` for unhandled crashes

---

## Rules

1. **Do not add a logging framework** unless the developer explicitly requests it.
2. If temporary debug output is needed, use `System.Diagnostics.Debug.WriteLine()` — it compiles out in Release builds.
3. **Do not add `Console.WriteLine`** — this is a GUI app with no console.
4. Error messages shown to users should be in **Chinese** to match the existing UI language.
