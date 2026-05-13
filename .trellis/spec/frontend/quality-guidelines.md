# Frontend Quality Guidelines

> Project: NovelMoyo — XAML and ViewModel quality standards

---

## XAML Conventions

### Naming
- `x:Name` uses PascalCase: `ReadingScrollViewer`, `FrameBorder`, `StatusBar`.
- Resource keys use PascalCase: `BackgroundBrush`, `StatusBarTextBrush`.

### Binding Patterns
```xml
<!-- Simple binding -->
<TextBlock Text="{Binding ChapterDisplay}" />

<!-- Theme resource (DynamicResource for runtime switching) -->
<Border Background="{DynamicResource BackgroundBrush}" />

<!-- Converter (StaticResource — registered in App.xaml) -->
<TextBlock Text="{Binding ReadingPercent, Converter={StaticResource PercentToString}}" />

<!-- Converter with parameter -->
<Button Visibility="{Binding IsVisible, Converter={StaticResource BoolToVisibility}, ConverterParameter=Invert}" />

<!-- StringFormat -->
<TextBlock Text="{Binding LastReadAt, StringFormat='yyyy-MM-dd HH:mm'}" />

<!-- RelativeSource for UserControls -->
Text="{Binding ChapterTitle, RelativeSource={RelativeSource AncestorType=UserControl}}"
```

### Resource Dictionaries
- All themes define the same set of resource keys.
- Theme switching: `Application.Current.Resources.MergedDictionaries[0] = newTheme`.
- Converters registered as `Application.Resources` in `App.xaml` (StaticResource).

---

## ViewModel Conventions

### INotifyPropertyChanged
- Implement manually — no shared base class.
- Use `[CallerMemberName]` on `OnPropertyChanged()`.

```csharp
public event PropertyChangedEventHandler? PropertyChanged;

private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
{
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
```

### Commands
- Use hand-rolled `RelayCommand` (C# 12 primary constructor).
- Create as `new RelayCommand(execute, canExecute)` with lambda expressions.
- `RaiseCanExecuteChanged()` exists but is never called (dead code).

### Constructor Injection
- ViewModels receive services via constructor parameters.
- No DI container — all wiring manual in `App.OnStartup`.

```csharp
public MainViewModel(DataStore dataStore, BookshelfService bookshelf, ...)
{
    _dataStore = dataStore;
    _bookshelf = bookshelf;
    // ...
}
```

---

## Rules

1. **Use `{DynamicResource}`** for all theme-dependent brushes.
2. **Use `{StaticResource}`** for converters (they don't change at runtime).
3. **No `x:Name` on elements that only use bindings** — `x:Name` is for imperative code-behind access.
4. **No third-party UI libraries** — build with standard WPF controls.
5. **Keep MainWindow.xaml.cs hybrid** — do not try to make it pure MVVM. The imperative approach is intentional for performance.
6. **Child windows** can use more standard MVVM with XAML bindings.
