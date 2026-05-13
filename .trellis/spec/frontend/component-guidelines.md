# Component Guidelines

> Project: NovelMoyo — WPF XAML component patterns

---

## Window Pattern

All windows follow this pattern:

```csharp
public partial class XxxWindow : Window
{
    private readonly XxxViewModel _vm;

    public XxxWindow(/* dependencies */)
    {
        InitializeComponent();
        _vm = new XxxViewModel(/* ... */);
        DataContext = _vm;
    }
}
```

- **DataContext** set in code-behind constructor, not in XAML.
- **No `StartupUri`** — windows created manually via `new XxxWindow().Show()` or `.ShowDialog()`.
- Child windows (Bookshelf, Settings) get **fresh ViewModel instances** each time they open.

---

## MainWindow Special Pattern

MainWindow uses **hybrid MVVM** — most visual state is managed imperatively in code-behind:

- Subscribes to `_vm.PropertyChanged` with a switch on property name.
- Applies changes directly: `_vm.FontSize`, `_vm.BackgroundColor`, etc.
- Status bar uses XAML bindings (`{Binding ChapterDisplay}`, `{Binding ReadingPercent}`).
- This is intentional — WPF bindings are too slow for the hover-show/hide performance requirements.

---

## Custom UserControls

Use DependencyProperties for configurable properties:

```csharp
public static readonly DependencyProperty ChapterTitleProperty =
    DependencyProperty.Register(nameof(ChapterTitle), typeof(string), typeof(ReadingStatusBar));

public string ChapterTitle
{
    get => (string)GetValue(ChapterTitleProperty);
    set => SetValue(ChapterTitleProperty, value);
}
```

XAML binds via `RelativeSource`:
```xml
Text="{Binding ChapterTitle, RelativeSource={RelativeSource AncestorType=UserControl}}"
```

---

## Rules

1. **Do not set DataContext in XAML** — always in code-behind constructor.
2. **Use `DynamicResource`** for theme brushes (allows runtime theme switching).
3. **Use `StaticResource`** for converters (registered in App.xaml).
4. **WindowStyle="None"** + **AllowsTransparency="True"** for borderless windows.
5. **No third-party UI libraries** — all controls are built-in WPF or hand-rolled.
