using Microsoft.Win32;

namespace WallP.Services;

public sealed class StartupRegistrationService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "WallP";

    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(ValueName) is string;
        }
    }

    public void Enable()
    {
        var path = Environment.ProcessPath
            ?? throw new InvalidOperationException("Could not determine the current executable path.");
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? throw new InvalidOperationException("Could not open the Run registry key.");
        key.SetValue(ValueName, $"\"{path}\"", RegistryValueKind.String);
    }

    public void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        if (key?.GetValue(ValueName) is null) return;
        key.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    public void Apply(bool enabled)
    {
        if (enabled) Enable(); else Disable();
    }
}
