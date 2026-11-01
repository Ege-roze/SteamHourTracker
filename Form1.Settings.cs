using System.Globalization;

namespace SteamHourTracker;

public partial class Form1
{
    private readonly SettingsStore settingsStore;
    private AppSettings preferences = new();
    private System.Windows.Forms.Timer settingsTimer = null!;
    private bool settingsLoaded;
    private bool settingsWritable;
    private bool restoringSettings;
    private string? settingsAccount;
    private string? settingsWarning;
    private string? settingsWriteError;

    private void InitializeSettingsControls()
    {
        settingsTimer = new System.Windows.Forms.Timer(components!) { Interval = 300 };
        settingsTimer.Tick += (_, _) => FlushSettings();
        multiGameBox.CheckedChanged += (_, _) => QueueSettingsSave();
        invisibleModeBox.CheckedChanged += (_, _) => QueueSettingsSave();
        autoStopBox.CheckedChanged += (_, _) => QueueSettingsSave();
        minutesBox.ValueChanged += (_, _) => QueueSettingsSave();
        sourceBox.SelectedIndexChanged += (_, _) => QueueSettingsSave();
        appIdBox.TextChanged += (_, _) => QueueSettingsSave();
        appIdBox.TextChanged += (_, _) => UpdateSelectionButtons();
        gameNameBox.TextChanged += (_, _) => QueueSettingsSave();
    }

    private void LoadSettings()
    {
        var result = settingsStore.Load();
        preferences = result.Settings;
        settingsWritable = result.CanSave;
        settingsWarning = result.Warning;
        UiText.SetLanguage(preferences.Language);
        restoringSettings = true;
        try
        {
            multiGameBox.Checked = preferences.MultiGame;
            invisibleModeBox.Checked = preferences.Invisible;
            autoStopBox.Checked = preferences.AutoStop;
            minutesBox.Value = preferences.Minutes;
        }
        finally { restoringSettings = false; settingsLoaded = true; }
        ApplyLanguage();
        if (settingsWarning != null) SetStatus(UiText.Get("Ayarlar yüklendi."));
    }

    private void QueueSettingsSave()
    {
        if (!settingsLoaded || restoringSettings || changingLanguage || closing || !settingsWritable) return;
        settingsTimer.Stop();
        settingsTimer.Start();
    }

    private void FlushSettings()
    {
        settingsTimer.Stop();
        if (!settingsLoaded || restoringSettings || !settingsWritable) return;
        CaptureTargetProgress();
        foreach (var selection in preferences.Accounts.Values) selection.Weekly.EnsureWeek(DateTimeOffset.UtcNow);
        progressSaveClock.Restart();
        preferences.MultiGame = multiGameBox.Checked;
        preferences.Invisible = invisibleModeBox.Checked;
        preferences.AutoStop = autoStopBox.Checked;
        preferences.Minutes = (int)minutesBox.Value;
        if (settingsAccount != null && !libraryLoading)
        {
            preferences.Accounts[settingsAccount] = new GameSelectionSettings
            {
                SourceIndex = sourceBox.SelectedIndex,
                CurrentGame = AppIdInput.TryParse(appIdBox.Text, out uint id)
                    ? new SavedGame(id, gameNameBox.Text) : null,
                SelectedGames = selectedGames.ToList(),
                TargetProgress = AccountSelection(settingsAccount).TargetProgress,
                CardExcludedAppIds = AccountSelection(settingsAccount).CardExcludedAppIds,
                Weekly = AccountSelection(settingsAccount).Weekly
            };
        }
        try
        {
            settingsStore.Save(preferences);
            if (settingsWriteError != null)
            {
                settingsWriteError = null;
                SetStatus(UiText.Get("Ayarlar kaydedildi."));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            settingsWriteError = UiText.Get("Ayarlar kaydedilemedi: ") + ex.Message;
            SetStatus(UiText.Get("Tercihlerin bu açılışta kullanılmaya devam edecek."), true);
        }
    }

    private void RestoreGameSelection(ulong? steamId)
    {
        sessions.ClearFinished();
        cardFarm.ClearStatus();
        settingsAccount = steamId?.ToString(CultureInfo.InvariantCulture) ?? SettingsStore.LocalAccount;
        ApplyCardExclusions();
        cardWindow?.RefreshRows();
        preferences.Accounts.TryGetValue(settingsAccount, out var selection);
        restoringSettings = true;
        try
        {
            searchBox.Clear();
            sourceBox.SelectedIndex = selection?.SourceIndex ?? 0;
            selectedGames.Clear();
            if (selection != null) selectedGames.AddRange(selection.SelectedGames);
            RefreshGames(selection?.CurrentGame?.AppId);
            if (selection?.CurrentGame is { } game)
            {
                // A manually entered App ID need not be present in Steam's cache.
                appIdBox.Text = game.AppId.ToString(CultureInfo.InvariantCulture);
                gameNameBox.Text = game.Name;
            }
            RefreshSelectedGames();
        }
        finally { restoringSettings = false; }
    }
}
