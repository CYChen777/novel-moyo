using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace NovelMoyo.Services;

public class HotkeyService : IDisposable
{
    private const int WM_HOTKEY = 0x0312;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private readonly Dictionary<int, Action> _callbacks = [];
    private HwndSource? _hwndSource;
    private IntPtr _hwnd;
    private bool _disposed;
    private Window? _pendingWindow;
    private EventHandler? _pendingSourceInitializedHandler;

    public void Init(IntPtr hwnd)
    {
        _hwnd = hwnd;
        _hwndSource = HwndSource.FromHwnd(hwnd);
        _hwndSource?.AddHook(WndProc);
    }

    public void Init(Window window)
    {
        _pendingWindow = window;
        var helper = new WindowInteropHelper(window);
        var hwnd = helper.Handle;

        if (hwnd == IntPtr.Zero)
        {
            _pendingSourceInitializedHandler = (_, _) =>
            {
                var h = new WindowInteropHelper(window).Handle;
                Init(h);
            };
            window.SourceInitialized += _pendingSourceInitializedHandler;
        }
        else
        {
            Init(hwnd);
        }
    }

    public bool RegisterHotkey(int id, ModifierKeys modifiers, Key key, Action callback)
    {
        var mod = (uint)modifiers;
        var vk = (uint)KeyInterop.VirtualKeyFromKey(key);

        if (!RegisterHotKey(_hwnd, id, mod, vk))
            return false;

        _callbacks[id] = callback;
        return true;
    }

    public void UnregisterHotkey(int id)
    {
        if (_callbacks.ContainsKey(id))
        {
            UnregisterHotKey(_hwnd, id);
            _callbacks.Remove(id);
        }
    }

    public void UnregisterAll()
    {
        foreach (var id in _callbacks.Keys.ToList())
        {
            UnregisterHotKey(_hwnd, id);
        }
        _callbacks.Clear();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            var id = wParam.ToInt32();
            if (_callbacks.TryGetValue(id, out var callback))
            {
                callback.Invoke();
                handled = true;
            }
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Unsubscribe the pending SourceInitialized handler if it was never triggered
        if (_pendingSourceInitializedHandler is not null && _pendingWindow is not null)
            _pendingWindow.SourceInitialized -= _pendingSourceInitializedHandler;

        UnregisterAll();
        _hwndSource?.RemoveHook(WndProc);
        GC.SuppressFinalize(this);
    }
}
