using System.Windows.Threading;

namespace NovelMoyo.Services;

public class AutoScrollService : IDisposable
{
    private static readonly Dictionary<int, int> SpeedToInterval = new()
    {
        [1] = 2000,
        [2] = 1500,
        [3] = 1000,
        [4] = 500,
        [5] = 200
    };

    private const int MinSpeed = 1;
    private const int MaxSpeed = 5;

    private readonly DispatcherTimer _timer;
    private int _speedLevel = 3;
    private bool _disposed;

    public event Action? OnScrollTick;
    public event Action<int>? OnSpeedChanged;

    public int SpeedLevel
    {
        get => _speedLevel;
        set
        {
            var clamped = Math.Clamp(value, MinSpeed, MaxSpeed);
            if (_speedLevel == clamped) return;
            _speedLevel = clamped;
            UpdateInterval();
            OnSpeedChanged?.Invoke(_speedLevel);
        }
    }

    public bool IsRunning => _timer.IsEnabled;

    public AutoScrollService()
    {
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(SpeedToInterval[_speedLevel])
        };
        _timer.Tick += (_, _) => OnScrollTick?.Invoke();
    }

    public void Start() => _timer.Start();
    public void Stop() => _timer.Stop();

    public void Toggle()
    {
        if (_timer.IsEnabled) Stop();
        else Start();
    }

    public void SpeedUp() => SpeedLevel = SpeedLevel + 1;
    public void SpeedDown() => SpeedLevel = SpeedLevel - 1;

    private void UpdateInterval()
    {
        _timer.Interval = TimeSpan.FromMilliseconds(SpeedToInterval[_speedLevel]);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        GC.SuppressFinalize(this);
    }
}
