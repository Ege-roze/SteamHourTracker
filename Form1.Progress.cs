using System.Diagnostics;

namespace SteamHourTracker;

public partial class Form1
{
    private string? progressAccount;
    private int progressRevision = -1;
    private readonly HashSet<GameSessionRun> recordedFinalRuns = [];
    private readonly Stopwatch progressSaveClock = Stopwatch.StartNew();

    private Dictionary<uint, GameTargetProgress> CurrentProgress => settingsAccount == null
        ? [] : AccountSelection(settingsAccount).TargetProgress;

    private GameSelectionSettings AccountSelection(string account)
    {
        if (!preferences.Accounts.TryGetValue(account, out var selection))
            preferences.Accounts[account] = selection = new();
        return selection;
    }

    private void CaptureTargetProgress()
    {
        if (progressAccount == null || sessions.Revision != progressRevision) return;
        var progress = AccountSelection(progressAccount).TargetProgress;
        foreach (var run in sessions.Runs)
        {
            string account = progressAccount;
            run.RecordWorkedTime ??= (from, to, seconds) => AccountSelection(account).Weekly.Add(run.Game, from, to, seconds);
            run.CaptureWorkedTime();
            if (run.TargetMinutes == 0 || (!run.IsActive && !recordedFinalRuns.Add(run))) continue;
            progress[run.Game.AppId] = new(run.TargetMinutes, run.CompletedSeconds, run.Game.TargetMinutes == null);
        }
    }

    private void SaveProgressPeriodically()
    {
        if (sessions.IsBusy && (uiTimer.Interval >= 5000 || progressSaveClock.Elapsed.TotalSeconds >= 5)) FlushSettings();
    }

    private void GeneralTargetChanged()
    {
        if (settingsLoaded && !restoringSettings && !sessions.IsBusy && !coordinator.IsBusy)
        {
            foreach (var selection in preferences.Accounts.Values)
                foreach (uint id in selection.TargetProgress.Where(p => p.Value.UsesGeneral).Select(p => p.Key).ToArray())
                    selection.TargetProgress.Remove(id);
            sessions.ClearFinished();
        }
        UpdateSelectedRows();
        UpdateSelectionButtons();
    }

    private void ResetSelectedProgress()
    {
        if (sessions.IsBusy || coordinator.IsBusy || libraryLoading || closing) return;
        uint[] ids = multiGameBox.Checked ? SelectedAppIds() :
            AppIdInput.TryParse(appIdBox.Text, out uint id) ? [id] : [];
        foreach (uint gameId in ids) CurrentProgress.Remove(gameId);
        sessions.ClearFinished();
        FlushSettings();
        UpdateTime();
        SetStatus(UiText.Get("Seçilen hedefin ilerlemesi sıfırlandı. Başlat'a basınca hedefin tamamı sayılacak."));
    }

    private WeeklyPlaytime ReadWeeklySummary()
    {
        CaptureTargetProgress();
        FlushSettings();
        return (settingsAccount == null ? new WeeklyPlaytime() : AccountSelection(settingsAccount).Weekly).Normalize(DateTimeOffset.UtcNow);
    }

    private void ShowWeeklySummary()
    {
        if (closing || libraryLoading || settingsAccount == null) return;
        using var dialog = new WeeklySummaryDialog(library?.AccountName ?? UiText.Get("Yerel liste"), ReadWeeklySummary);
        dialog.ShowDialog(this);
    }
}
