# Frontend Directory Structure

> Project: NovelMoyo — WPF XAML UI layer
> Scope: Views, ViewModels, Converters, Themes, Controls

---

## Layout

```
src/NovelMoyo/
├── Views/
│   ├── MainWindow.xaml / .xaml.cs          # Main reading overlay (734 lines)
│   ├── BookshelfWindow.xaml / .xaml.cs     # Novel library management
│   ├── SettingsWindow.xaml / .xaml.cs      # Settings with 5 tabs
│   └── Controls/
│       └── ReadingStatusBar.xaml / .xaml.cs # Custom UserControl with DependencyProperties
├── ViewModels/
│   ├── MainViewModel.cs                    # Main reading state + commands
│   ├── BookshelfViewModel.cs               # Novel library list + operations
│   ├── SettingsViewModel.cs                # Settings editing + save/reset
│   └── RelayCommand.cs                     # ICommand implementation (C# 12 primary constructor)
├── Converters/
│   ├── BoolToVisibilityConverter.cs        # Supports "Invert" parameter
│   └── PercentToStringConverter.cs         # double → "42%"
└── Resources/
    └── Themes/
        ├── DarkTheme.xaml                  # Default theme
        ├── GreenTheme.xaml
        └── WarmTheme.xaml
```

---

## Rules

1. **Windows** go in `Views/`, filename matches class name.
2. **Custom UserControls** go in `Views/Controls/`.
3. **ViewModels** go in `ViewModels/`, one per window (not per view).
4. **Converters** go in `Converters/`, registered as app-level resources in `App.xaml`.
5. **Themes** go in `Resources/Themes/`, loaded as merged resource dictionaries.
6. **No StartupUri** — windows are created manually in code-behind.
