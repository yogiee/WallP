using Microsoft.Win32;

namespace WallP.Services;

/// <summary>
/// Surfaces session-lock and power-state events from the OS so the rotator can
/// pause/resume without changing the user-facing IsPaused flag.
/// </summary>
public sealed class SystemStateMonitor : IDisposable
{
    public event EventHandler? SessionLocked;
    public event EventHandler? SessionUnlocked;
    public event EventHandler? Suspending;
    public event EventHandler? Resumed;

    private bool _started;

    public void Start()
    {
        if (_started) return;
        _started = true;
        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }

    public void Stop()
    {
        if (!_started) return;
        _started = false;
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
    }

    public void Dispose() => Stop();

    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        switch (e.Reason)
        {
            case SessionSwitchReason.SessionLock:
            case SessionSwitchReason.RemoteDisconnect:
                SessionLocked?.Invoke(this, EventArgs.Empty);
                break;
            case SessionSwitchReason.SessionUnlock:
            case SessionSwitchReason.RemoteConnect:
                SessionUnlocked?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        switch (e.Mode)
        {
            case PowerModes.Suspend:
                Suspending?.Invoke(this, EventArgs.Empty);
                break;
            case PowerModes.Resume:
                Resumed?.Invoke(this, EventArgs.Empty);
                break;
        }
    }
}
