using System.Diagnostics;
using Microsoft.Toolkit.Uwp.Notifications;

namespace WallP.Services;

/// <summary>
/// Thin wrapper around Microsoft.Toolkit.Uwp.Notifications. The first call to
/// <see cref="ShowSyncComplete"/> implicitly registers a Start-Menu shortcut + AUMID
/// via <c>ToastNotificationManagerCompat</c>, so toasts attribute correctly to "WallP"
/// instead of falling back to the host process name.
/// </summary>
public sealed class NotificationService
{
    public void ShowSyncComplete(int newImageCount)
    {
        // Don't bother the user when there was nothing new to sync.
        if (newImageCount <= 0) return;

        var body = newImageCount == 1
            ? "Synced 1 new wallpaper."
            : $"Synced {newImageCount} new wallpapers.";

        try
        {
            new ToastContentBuilder()
                .AddText("WallP")
                .AddText(body)
                .Show();
        }
        catch (Exception ex)
        {
            // Notification registration can fail on systems with toasts disabled at
            // group-policy level or in restricted user contexts. Failure is non-fatal.
            Debug.WriteLine($"[WallP][Notify] Toast failed: {ex.Message}");
        }
    }

    public void ShowSyncFailed(string error)
    {
        if (string.IsNullOrWhiteSpace(error)) return;
        try
        {
            new ToastContentBuilder()
                .AddText("WallP — sync failed")
                .AddText(error)
                .Show();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WallP][Notify] Toast failed: {ex.Message}");
        }
    }
}
