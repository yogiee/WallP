namespace WallP.Services;

public sealed class UpdaterService
{
    public Task CheckForUpdatesAsync(bool userInitiated, CancellationToken ct = default)
        => throw new NotImplementedException();

    public bool AutomaticChecksEnabled
    {
        get => throw new NotImplementedException();
        set => throw new NotImplementedException();
    }
}
