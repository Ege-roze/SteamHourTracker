using System.Diagnostics;
using System.Text.Json;

namespace SteamHourTracker;

public partial class Form1 : Form
{
    private readonly Stopwatch elapsed = new();
    private readonly List<SavedGame> games = [];
    private readonly SteamSessionGroup sessions = new();
    private readonly SteamSessionCoordinator coordinator;
    private readonly List<SavedGame> selectedGames = [];
    private bool closing;
    private bool mayClose;
    private bool exitRequested;
    private SteamLibrarySnapshot? library;
    private CancellationTokenSource? libraryCancellation;
    private Task? libraryTask;
    private bool libraryLoading;
    private readonly string gamesPath;

    public Form1() : this(new SteamPresenceProcess()) { }

    internal Form1(ISteamPresence presence, string? dataDirectory = null)
    {
        dataDirectory ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SteamHourTracker");
        gamesPath = Path.Combine(dataDirectory, "games.json");
        settingsStore = new SettingsStore(dataDirectory);
        coordinator = new SteamSessionCoordinator(sessions, presence);
        cardFarm = new CardFarm(sessions, coordinator);
        InitializeComponent();
        InitializeLanguageControls();
        InitializeMultiGameControls();
        sessions.Changed += UpdateSessionState;
        coordinator.Changed += UpdateSessionState;
        cardFarm.Changed += UpdateSessionState;
        cardFarm.BeforeStage += PrepareCardStage;
        cardFarm.Completed += () => ShowCompletionNotification(UiText.Get("Kart modu sona erdi"), cardFarm.Message);
        sourceBox.SelectedIndex = 0;
        trayIcon.Icon = SystemIcons.Application;
        Icon = SystemIcons.Application;
        startButton.Click += async (_, _) => await StartSessionAsync();
        stopButton.Click += async (_, _) => await StopSessionAsync();
        resetProgressButton.Click += (_, _) => ResetSelectedProgress();
        weeklySummaryButton.Click += (_, _) => ShowWeeklySummary();
        cardModeButton.Click += async (_, _) => await ConfigureCardModeAsync();
        saveButton.Click += (_, _) => SaveGame();
        removeButton.Click += (_, _) => RemoveGame();
        gamesBox.SelectedIndexChanged += (_, _) => SelectGame();
        sourceBox.SelectedIndexChanged += (_, _) =>
        {
            if (changingLanguage) return;
            appIdBox.Clear();
            gameNameBox.Clear();
            RefreshGames();
            SetBusy(sessions.IsBusy);
        };
        searchBox.TextChanged += (_, _) => RefreshGames();
        refreshLibraryButton.Click += async (_, _) => await RefreshLibraryAsync();
        autoStopBox.CheckedChanged += (_, _) => minutesBox.Enabled = autoStopBox.Checked && !sessions.IsBusy;
        uiTimer.Tick += (_, _) => { UpdateTime(); SaveProgressPeriodically(); };
        trayIcon.DoubleClick += (_, _) => RestoreWindow();
        trayIcon.BalloonTipClicked += (_, _) => RestoreWindow();
        trayIcon.BalloonTipClosed += (_, _) => { if (Visible && !closing) trayIcon.Visible = false; };
        showMenuItem.Click += (_, _) => RestoreWindow();
        stopMenuItem.Click += async (_, _) => await StopSessionAsync();
        exitMenuItem.Click += (_, _) => ExitApplication();
        Resize += (_, _) =>
        {
            if (WindowState == FormWindowState.Minimized && minimizeToTrayBox.Checked)
            {
                HideToTray();
            }
        };
        FormClosing += OnFormClosing;
        InitializeSettingsControls();
        Load += (_, _) => { LoadGames(); LoadSettings(); };
        Shown += async (_, _) => await RefreshLibraryAsync();
    }

    private async Task StartSessionAsync()
    {
        if (coordinator.IsBusy || cardFarm.IsBusy || cardStarting || sessions.IsBusy || closing || libraryLoading) return;
        SavedGame[] targets;
        if (multiGameBox.Checked)
        {
            if (selectedGames.Count == 0)
            {
                SetStatus(UiText.Get("Birden fazla oyun için önce oyunları Ekle düğmesiyle listeye ekle."), true);
                return;
            }
            targets = selectedGames.ToArray();
        }
        else
        {
            SavedGame? game = ReadCurrentGame();
            if (game == null) return;
            targets = [game];
        }
        FlushSettings();
        var progress = CurrentProgress.ToDictionary(p => p.Key, p => p.Value);
        targets = targets.Where(g => (g.TargetMinutes ?? GeneralTargetMinutes) == 0 ||
            GameTargetProgress.Read(progress, g, g.TargetMinutes ?? GeneralTargetMinutes) < (g.TargetMinutes ?? GeneralTargetMinutes) * 60d).ToArray();
        if (targets.Length == 0)
        {
            SetStatus(UiText.Get("Hedef tamamlandı. Yeniden çalıştırmak için Hedefi sıfırla düğmesini kullan."));
            return;
        }
        progressAccount = settingsAccount;
        cardFarm.ClearStatus();
        progressRevision = sessions.Revision + 1;
        recordedFinalRuns.Clear();
        elapsed.Reset();
        UpdateTime();
        uiTimer.Start();
        SetStatus(UiText.Get("Steam'e bağlanılıyor…"));
        try
        {
            await coordinator.StartAsync(targets, GeneralTargetMinutes, invisibleModeBox.Checked, progress, library?.SteamId ?? 0);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, true);
        }
        UpdateSessionState();
    }

    private SavedGame? ReadCurrentGame()
    {
        if (!AppIdInput.TryParse(appIdBox.Text, out uint appId))
        {
            SetStatus(UiText.Get("App ID, sıfırdan büyük bir tam sayı olmalı."), true);
            appIdBox.Focus();
            return null;
        }
        string name = string.IsNullOrWhiteSpace(gameNameBox.Text) ? $"App ID {appId}" : gameNameBox.Text.Trim();
        return new SavedGame(appId, name);
    }

    private Task StopSessionAsync() => cardFarm.IsBusy ? cardFarm.StopAsync() : coordinator.StopAllAsync();

    private void UpdateSessionState()
    {
        if (IsDisposed) return;
        CaptureTargetProgress();
        if (sessions.Revision == progressRevision)
        {
            // Coalesce bursts from multiple workers; the final state is still saved immediately.
            if (sessions.IsBusy) QueueSettingsSave(); else FlushSettings();
        }
        int running = sessions.Runs.Count(r => r.Status == GameSessionStatus.Running);
        int starting = sessions.Runs.Count(r => r.Status == GameSessionStatus.Starting);
        int reconnecting = sessions.Runs.Count(r => r.Status == GameSessionStatus.Reconnecting);
        int failed = sessions.Runs.Count(r => r.Status == GameSessionStatus.Failed);
        if (running > 0)
        {
            if (!elapsed.IsRunning) elapsed.Start();
            uiTimer.Start();
        }
        if (running == 0) elapsed.Stop();
        UpdateUiTimer();
        trayIcon.Text = coordinator.IsBusy ? UiText.Get("SteamHourTracker • oturum açık") : UiText.Get("SteamHourTracker • durduruldu");
        NotifyCompletedTargets();
        SetBusy(sessions.IsBusy);
        if (!Visible && cardWindow is not { Visible: true }) return;
        if (sessions.Runs.Count > 0)
        {
            activeGameLabel.Text = sessions.Runs.Count == 1 ? sessions.Runs[0].Game.Name
                : UiText.Format($"{running} çalışıyor • {starting} bağlanıyor • {reconnecting} yeniden bağlanacak • {sessions.Runs.Count(r => !r.IsActive)} tamamlandı");
            string summary = sessions.IsBusy
                ? UiText.Format($"{running} oyun çalışıyor, {starting} bağlanıyor, {reconnecting} yeniden bağlanacak, {failed} hata.")
                : UiText.Format($"Oturumlar sona erdi. {failed} hata.");
            SetStatus(sessions.Runs.Count == 1 ? sessions.Runs[0].Message
                : summary + string.Concat(sessions.Runs.Where(r => r.Status is GameSessionStatus.Failed or GameSessionStatus.Reconnecting)
                    .Select(r => Environment.NewLine + r.Game.Name + ": " + r.Message)), failed > 0);
        }
        if (coordinator.IsPreparing)
            SetStatus(invisibleModeBox.Checked ? UiText.Get("Steam görünmez moda geçiriliyor…") : UiText.Get("Steam'e bağlanılıyor…"));
        if (coordinator.IsRestoring) SetStatus(UiText.Get("Oyunlar durdu. Steam çevrimiçi moda geçiriliyor…"));
        if (!coordinator.IsBusy && coordinator.Error != null) SetStatus(coordinator.Error, true);
        if (!coordinator.IsBusy && cardFarm.Phase == CardFarmPhase.Finished)
            SetStatus(cardFarm.Message, cardFarm.Error != null);
        UpdateTime();
    }

    private async void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (mayClose) return;
        FlushSettings();
        bool systemClosing = e.CloseReason is CloseReason.WindowsShutDown or CloseReason.TaskManagerClosing;
        if (closing) { e.Cancel = !systemClosing; return; }
        if (e.CloseReason == CloseReason.UserClosing && !exitRequested)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }
        closing = true;
        libraryCancellation?.Cancel();
        if (systemClosing)
        {
            // Do not cancel Windows shutdown. Closing stdin asks every worker to stop;
            // parent-process exit also closes the pipes if shutdown cuts cleanup short.
            trayIcon.Visible = false;
            _ = StopSessionAsync();
            return;
        }
        if (!coordinator.IsBusy && !cardFarm.IsBusy && !sessions.IsBusy && libraryTask?.IsCompleted != false)
        {
            trayIcon.Visible = false;
            return;
        }
        e.Cancel = true;
        await StopSessionAsync();
        if (libraryTask != null) await libraryTask;
        if (IsDisposed) return;
        trayIcon.Visible = false;
        mayClose = true;
        Close();
    }

    private void SetBusy(bool busy)
    {
        languageButton.Enabled = !busy && !coordinator.IsBusy && !cardFarm.IsBusy && !cardStarting && !closing && !libraryLoading;
        weeklySummaryButton.Enabled = !closing && !libraryLoading && settingsAccount != null;
        busy |= coordinator.IsBusy || cardFarm.IsBusy || cardStarting;
        cardModeButton.Enabled = (!busy || cardFarm.IsBusy) && !closing && !libraryLoading;
        cardModeButton.Text = UiText.Get("Kart modu…");
        startButton.Enabled = !busy && !closing && !libraryLoading;
        stopButton.Enabled = stopMenuItem.Enabled = !closing && !coordinator.IsRestoring &&
            (cardFarm.IsBusy || coordinator.IsPreparing || sessions.Runs.Any(r =>
            r.Status is GameSessionStatus.Starting or GameSessionStatus.Running or GameSessionStatus.Reconnecting));
        gamesBox.Enabled = appIdBox.Enabled = gameNameBox.Enabled = saveButton.Enabled = !busy && !libraryLoading;
        sourceBox.Enabled = searchBox.Enabled = !busy;
        refreshLibraryButton.Enabled = !busy && !libraryLoading;
        removeButton.Enabled = !busy && !libraryLoading && sourceBox.SelectedIndex == 1;
        autoStopBox.Enabled = !busy;
        minutesBox.Enabled = !busy && autoStopBox.Checked;
        multiGameBox.Enabled = !busy && !closing && !libraryLoading;
        invisibleModeBox.Enabled = !busy && !closing && !libraryLoading;
        addSelectedButton.Enabled = !busy && !closing && !libraryLoading;
        UpdateSelectionButtons();
    }

    private void UpdateUiTimer()
    {
        if (IsDisposed || Disposing) return;
        int interval = Visible || cardWindow is { Visible: true } ? 1000 : 5000;
        if (uiTimer.Interval != interval) uiTimer.Interval = interval;
        uiTimer.Enabled = sessions.IsBusy || cardFarm.IsBusy;
    }

    private void UpdateTime()
    {
        if (!Visible && cardWindow is not { Visible: true }) return;
        if (Visible)
        {
        TimeSpan time = elapsed.Elapsed;
        elapsedLabel.Text = $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}";
        if (multiLayout.Visible) UpdateSelectedRows();
        if (!cardFarm.IsBusy && !multiGameBox.Checked && AppIdInput.TryParse(appIdBox.Text, out uint id) && GeneralTargetMinutes > 0)
        {
            var run = sessions.Runs.FirstOrDefault(r => r.Game.AppId == id);
            double remaining = Math.Max(0, GeneralTargetMinutes * 60d -
                (run?.IsActive == true ? run.CompletedSeconds : GameTargetProgress.Read(CurrentProgress, new(id, gameNameBox.Text), GeneralTargetMinutes)));
            activeGameLabel.Text = gameNameBox.Text + UiText.Get(" • kalan ") + ClockText(TimeSpan.FromSeconds(Math.Ceiling(remaining)));
        }
        UpdateCardStatus();
        }
        if (cardWindow is { Visible: true }) cardWindow.RefreshRows();
    }

    private void SetStatus(string text, bool error = false)
    {
        string value = (error ? UiText.Get("Hata: ") + text : text) +
            (settingsWarning == null ? "" : Environment.NewLine + settingsWarning) +
            (settingsWriteError == null ? "" : Environment.NewLine + settingsWriteError);
        if (statusLabel.Text != value) statusLabel.Text = value;
        statusLabel.ForeColor = SystemColors.ControlText;
    }

    private void RestoreWindow()
    {
        if (closing || IsDisposed) return;
        Show();
        WindowState = FormWindowState.Normal;
        if (cardWindowHiddenToTray && cardWindow is { IsDisposed: false }) cardWindow.Show();
        cardWindowHiddenToTray = false;
        Activate();
        trayIcon.Visible = false;
        UpdateSessionState();
    }

    private void HideToTray()
    {
        trayIcon.Visible = true;
        cardWindowHiddenToTray = cardWindow is { Visible: true };
        if (cardWindowHiddenToTray) cardWindow!.Hide();
        Hide();
        UpdateUiTimer();
    }

    private void ExitApplication()
    {
        if (closing || IsDisposed) return;
        exitRequested = true;
        Close();
    }

    private void LoadGames()
    {
        if (!File.Exists(gamesPath)) return;
        try
        {
            var loaded = JsonSerializer.Deserialize<List<SavedGame>>(File.ReadAllText(gamesPath));
            if (loaded != null)
                games.AddRange(loaded.Where(g => g != null && g.AppId > 0 && !string.IsNullOrWhiteSpace(g.Name)).DistinctBy(g => g.AppId));
            RefreshGames();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            SetStatus(UiText.Get("Kayıtlı oyunlar okunamadı. App ID'yi elle girebilirsin."), true);
        }
    }

    private void SaveGame()
    {
        if (!AppIdInput.TryParse(appIdBox.Text, out uint id))
        {
            SetStatus(UiText.Get("Kaydetmek için geçerli bir App ID gir."), true);
            return;
        }
        string name = string.IsNullOrWhiteSpace(gameNameBox.Text) ? $"App ID {id}" : gameNameBox.Text.Trim();
        var updated = games.Where(g => g.AppId != id).Append(new SavedGame(id, name)).OrderBy(g => g.Name).ToList();
        if (!PersistGames(updated)) return;
        games.Clear();
        games.AddRange(updated);
        sourceBox.SelectedIndex = 1;
        RefreshGames(id);
        SetStatus(UiText.Get("Oyun bu bilgisayara kaydedildi."));
    }

    private void RemoveGame()
    {
        if (sourceBox.SelectedIndex != 1 || gamesBox.SelectedItem is not GameChoice selected) return;
        var updated = games.Where(g => g.AppId != selected.AppId).ToList();
        if (!PersistGames(updated)) return;
        games.Clear();
        games.AddRange(updated);
        RefreshGames();
        SetStatus(UiText.Get("Oyun kayıt listesinden kaldırıldı."));
    }

    private bool PersistGames(List<SavedGame> updated)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(gamesPath)!);
            string temporary = gamesPath + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(updated, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, gamesPath, true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SetStatus(UiText.Get("Oyun listesi kaydedilemedi: ") + ex.Message, true);
            return false;
        }
    }

    private void RefreshGames(uint? selectedId = null)
    {
        selectedId ??= (gamesBox.SelectedItem as GameChoice)?.AppId;
        IEnumerable<GameChoice> choices = sourceBox.SelectedIndex == 1
            ? games.Select(g => new GameChoice(g.AppId, g.Name, null))
            : library?.Games.Select(g => new GameChoice(g.AppId, g.Name, g.Installed)) ?? [];
        string query = searchBox.Text.Trim();
        var filtered = choices.Where(g => query.Length == 0 ||
            g.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
            g.AppId.ToString(System.Globalization.CultureInfo.InvariantCulture).Contains(query)).ToList();
        gamesBox.BeginUpdate();
        gamesBox.Items.Clear();
        foreach (GameChoice game in filtered) gamesBox.Items.Add(game);
        if (selectedId.HasValue) gamesBox.SelectedItem = filtered.Find(g => g.AppId == selectedId);
        if (gamesBox.SelectedIndex < 0 && gamesBox.Items.Count > 0) gamesBox.SelectedIndex = 0;
        if (gamesBox.SelectedIndex < 0)
        {
            appIdBox.Clear();
            gameNameBox.Clear();
        }
        gamesBox.EndUpdate();
    }

    private void SelectGame()
    {
        if (changingLanguage) return;
        if (gamesBox.SelectedItem is not GameChoice game) return;
        appIdBox.Text = game.AppId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        gameNameBox.Text = game.Name;
    }

    private Task RefreshLibraryAsync()
    {
        if (closing || coordinator.IsBusy || cardFarm.IsBusy || sessions.IsBusy || libraryTask?.IsCompleted == false) return Task.CompletedTask;
        return libraryTask = LoadLibraryAsync();
    }

    private async Task LoadLibraryAsync()
    {
        // Save the old account before replacing controls with loading placeholders.
        FlushSettings();
        libraryLoading = true;
        settingsAccount = null;
        selectedGames.Clear();
        RefreshSelectedGames();
        libraryCancellation = new CancellationTokenSource();
        library = null;
        if (sourceBox.SelectedIndex == 0)
        {
            appIdBox.Clear();
            gameNameBox.Clear();
        }
        RefreshGames();
        SetBusy(false);
        subtitleLabel.Text = UiText.Get("Açık Steam hesabının kütüphanesi okunuyor…");
        SetStatus(UiText.Get("Steam kütüphanesi yükleniyor…"));
        try
        {
            var result = await SteamLibraryProcess.ReadAsync(libraryCancellation.Token);
            if (closing) return;
            library = result;
            subtitleLabel.Text = UiText.Format($"{result.AccountName}  •  {result.Games.Count} oyun  •  {result.Games.Count(g => g.Installed)} kurulu");
            RestoreGameSelection(result.SteamId);
            SetStatus(result.Games.Count > 0
                ? UiText.Get("Kütüphane hazır. Bir oyun seçip Başlat'a bas.")
                : UiText.Get("Oyun bulunamadı. Steam'de Kütüphane'yi açıp Yenile'ye basabilir veya App ID'yi elle girebilirsin."));
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (!closing)
            {
                RestoreGameSelection(null);
                subtitleLabel.Text = UiText.Get("Steam kütüphanesi yüklenemedi. Elle App ID girebilirsin.");
                SetStatus(ex.Message, true);
            }
        }
        finally
        {
            libraryLoading = false;
            libraryCancellation.Dispose();
            libraryCancellation = null;
            if (!closing)
            {
                SetBusy(sessions.IsBusy);
                FlushSettings();
            }
        }
    }
}

internal sealed record GameChoice(uint AppId, string Name, bool? Installed)
{
    public override string ToString() => $"{Name}  ·  {AppId}" + (Installed == true ? UiText.Get("  ·  Kurulu") : "");
}

public sealed record SavedGame(uint AppId, string Name, int? TargetMinutes = null)
{
    public override string ToString() => $"{Name}  ·  {AppId}";
}
