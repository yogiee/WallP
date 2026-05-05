using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using WallP.Models;

namespace WallP.Services;

[Flags]
public enum PauseReason
{
    None = 0,
    FullscreenApp = 1 << 0,
    OnBattery = 1 << 1,
}

/// <summary>
/// Periodically polls system state (fullscreen-app presence, AC vs battery)
/// and surfaces a combined "should be paused" reason mask. Consumers (App.OnStartup
/// wires this to the rotator) react to <see cref="PauseReasonsChanged"/> to
/// pause/resume without touching <see cref="AppSettings.IsPaused"/>.
/// </summary>
public sealed class PauseConditionMonitor : INotifyPropertyChanged, IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    private readonly AppSettings _settings;
    private Timer? _timer;
    private PauseReason _reasons;

    public PauseReason CurrentReasons
    {
        get => _reasons;
        private set
        {
            if (_reasons == value) return;
            _reasons = value;
            OnPropertyChanged(nameof(CurrentReasons));
            OnPropertyChanged(nameof(ShouldPause));
            PauseReasonsChanged?.Invoke(this, value);
        }
    }

    public bool ShouldPause => CurrentReasons != PauseReason.None;

    public event EventHandler<PauseReason>? PauseReasonsChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    public PauseConditionMonitor(AppSettings settings)
    {
        _settings = settings;
        _settings.PropertyChanged += OnSettingsChanged;
    }

    public void Start()
    {
        _timer?.Dispose();
        _timer = new Timer(static state =>
        {
            try { ((PauseConditionMonitor)state!).Poll(); }
            catch (Exception ex) { Debug.WriteLine($"[WallP][PauseMon] poll error: {ex.Message}"); }
        }, this, TimeSpan.Zero, PollInterval);
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    public void Dispose()
    {
        Stop();
        _settings.PropertyChanged -= OnSettingsChanged;
    }

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Re-evaluate immediately when the user toggles a relevant setting.
        if (e.PropertyName is nameof(AppSettings.PauseOnFullscreen)
                            or nameof(AppSettings.PauseOnBattery))
        {
            Poll();
        }
    }

    private void Poll()
    {
        var reasons = PauseReason.None;
        if (_settings.PauseOnFullscreen && IsFullscreenAppPresent())
        {
            reasons |= PauseReason.FullscreenApp;
        }
        if (_settings.PauseOnBattery && IsOnBattery())
        {
            reasons |= PauseReason.OnBattery;
        }
        CurrentReasons = reasons;
    }

    private static bool IsFullscreenAppPresent()
    {
        if (SHQueryUserNotificationState(out var state) != 0) return false;
        return state switch
        {
            QUNS_BUSY or QUNS_RUNNING_D3D_FULL_SCREEN or QUNS_PRESENTATION_MODE or QUNS_APP => true,
            _ => false,
        };
    }

    private static bool IsOnBattery()
    {
        return GetSystemPowerStatus(out var status) && status.ACLineStatus == 0;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    // SHQueryUserNotificationState constants
    private const int QUNS_BUSY = 2;
    private const int QUNS_RUNNING_D3D_FULL_SCREEN = 3;
    private const int QUNS_PRESENTATION_MODE = 4;
    private const int QUNS_APP = 7;

    [DllImport("shell32.dll")]
    private static extern int SHQueryUserNotificationState(out int state);

    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEM_POWER_STATUS
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public uint BatteryLifeTime;
        public uint BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS status);
}
