namespace WallP.Services;

public sealed class SystemStateMonitor
{
    public event EventHandler? SessionLocked;
    public event EventHandler? SessionUnlocked;
    public event EventHandler? Suspending;
    public event EventHandler? Resumed;

    public void Start() => throw new NotImplementedException();
    public void Stop() => throw new NotImplementedException();
}
