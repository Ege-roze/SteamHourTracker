using Microsoft.VisualBasic.ApplicationServices;

namespace SteamHourTracker;

// Uses the Windows Desktop runtime's local single-instance channel; no polling timer.
internal class SingleInstanceApplication : WindowsFormsApplicationBase
{
    public SingleInstanceApplication()
    {
        IsSingleInstance = true;
        EnableVisualStyles = true;
        ShutdownStyle = ShutdownMode.AfterMainFormCloses;
    }

    protected override void OnCreateMainForm() => MainForm = new Form1();

    protected override void OnStartupNextInstance(StartupNextInstanceEventArgs eventArgs)
    {
        if (MainForm is Form1 form) form.RestoreWindow();
        eventArgs.BringToForeground = true;
        base.OnStartupNextInstance(eventArgs);
    }
}
