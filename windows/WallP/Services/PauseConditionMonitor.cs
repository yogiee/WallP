using WallP.Models;

namespace WallP.Services;

[Flags]
public enum PauseReason
{
    None = 0,
    FullscreenApp = 1 << 0,
    OnBattery = 1 << 1,
    MeteredNetwork = 1 << 2,
    SystemSleep = 1 << 3,
    SessionLocked = 1 << 4,
    UserPaused = 1 << 5,
}

public sealed class PauseConditionMonitor
{
    private readonly AppSettings _settings;

    public PauseReason CurrentReasons { get; private set; }
    public bool ShouldPause => CurrentReasons != PauseReason.None;

    public event EventHandler<PauseReason>? PauseReasonsChanged;

    public PauseConditionMonitor(AppSettings settings)
    {
        _settings = settings;
    }

    public void Start() => throw new NotImplementedException();
    public void Stop() => throw new NotImplementedException();
}
