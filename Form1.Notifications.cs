namespace SteamHourTracker;

public partial class Form1
{
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal Action<string, string>? NotificationSink { get; set; }
    private int notifiedRevision = -1;
    private readonly HashSet<uint> notifiedTargets = [];

    private void NotifyCompletedTargets()
    {
        if (notifiedRevision != sessions.Revision)
        {
            notifiedRevision = sessions.Revision;
            notifiedTargets.Clear();
        }
        if (closing || cardFarm.IsBusy || cardStarting) return;
        var finished = sessions.Runs.Where(r => r.TargetReached && !r.StopRequested &&
            r.Status == GameSessionStatus.Stopped && notifiedTargets.Add(r.Game.AppId)).ToArray();
        if (finished.Length == 0) return;
        string message = finished.Length == 1
            ? UiText.Format($"{finished[0].Game.Name}: {finished[0].TargetMinutes} dakikalık süre hedefi tamamlandı.")
            : UiText.Format($"{finished.Length} oyunun süre hedefi tamamlandı: ") + string.Join(", ", finished.Select(r => r.Game.Name));
        ShowCompletionNotification(UiText.Get("Süre hedefi tamamlandı"), message);
    }

    private void ShowCompletionNotification(string title, string message)
    {
        if (closing || IsDisposed || Disposing) return;
        if (NotificationSink != null) { NotificationSink(title, message); return; }
        // Reuse the existing tray icon; no background service, polling or dependency.
        try
        {
            trayIcon.Visible = true;
            trayIcon.ShowBalloonTip(6000, title, message.Length > 240 ? message[..237] + "…" : message, ToolTipIcon.Info);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // A suppressed/unavailable Windows notification must not interrupt session cleanup.
        }
    }
}
