# Event & Communication Guidelines

> Project: NovelMoyo — ViewModel/View communication patterns (no custom hooks)

---

## This is a WPF project, not React. This file documents event/callback patterns instead.

---

## ViewModel → View Communication

### Pattern 1: PropertyChanged subscription (MainWindow)

```csharp
_vm.PropertyChanged += OnViewModelPropertyChanged;

private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
{
    switch (e.PropertyName)
    {
        case nameof(MainViewModel.FontSize):
            ReadingText.FontSize = _vm.FontSize;
            break;
        // ...
    }
}
```

Used when: imperative code-behind control is needed (performance, WPF limitations).

### Pattern 2: XAML Binding

```xml
<TextBlock Text="{Binding ChapterDisplay}" />
```

Used when: simple display binding, no performance concerns.

### Pattern 3: Direct property access

```csharp
var fontSize = _vm.FontSize;
```

Used when: code-behind needs to read ViewModel state during event handling.

---

## View → ViewModel Communication

### Pattern 1: Commands

```xml
<Button Command="{Binding ImportFileCommand}" />
```

### Pattern 2: Event callbacks

```csharp
vm.OnNovelSelected += novelId => LoadNovel(novelId);
vm.OnFileImported += filePath => _vm.ImportFile(filePath);
```

Used for: cross-window communication (Bookshelf → App, Settings → MainWindow).

### Pattern 3: Direct ViewModel method call

```csharp
_vm.UpdateReadingProgress();
```

Used for: imperative operations triggered by code-behind events (scroll, close).

---

## Rules

1. **Do not use an EventAggregator or Messenger** — direct events and callbacks are sufficient.
2. **Use `Action<T>` events**, not `EventHandler<T>` — simpler syntax, matches existing pattern.
3. **Event names** use `On` prefix: `OnScrollTick`, `OnNovelSelected`, `OnFileImported`.
4. **Child window events** are subscribed in the parent (App.xaml.cs or MainWindow.xaml.cs) after window creation.
