namespace SteamHourTracker;

public partial class Form1
{
    private void InitializeMultiGameControls()
    {
        multiGameBox.CheckedChanged += (_, _) =>
        {
            multiLayout.Visible = multiGameBox.Checked;
            startButton.Text = multiGameBox.Checked ? UiText.Get("Seçilenleri başlat") : UiText.Get("Başlat");
            stopButton.Text = multiGameBox.Checked ? UiText.Get("Hepsini durdur") : UiText.Get("Durdur");
            SetBusy(sessions.IsBusy);
            ResizeSelectedColumns();
        };
        addSelectedButton.Click += (_, _) => AddSelectedGame();
        removeSelectedButton.Click += (_, _) =>
        {
            if (sessions.IsBusy || coordinator.IsBusy) return;
            var ids = SelectedAppIds();
            selectedGames.RemoveAll(g => ids.Contains(g.AppId));
            RefreshSelectedGames();
            QueueSettingsSave();
        };
        clearSelectedButton.Click += (_, _) =>
        {
            if (sessions.IsBusy || coordinator.IsBusy) return;
            selectedGames.Clear();
            RefreshSelectedGames();
            QueueSettingsSave();
        };
        stopSelectedButton.Click += async (_, _) =>
        {
            if (cardFarm.IsBusy) await cardFarm.SkipAsync(SelectedAppIds());
            else await coordinator.StopGamesAsync(SelectedAppIds());
        };
        targetButton.Click += (_, _) => EditSelectedTargets();
        selectedGamesView.DoubleClick += (_, _) => EditSelectedTargets();
        autoStopBox.CheckedChanged += (_, _) => GeneralTargetChanged();
        minutesBox.ValueChanged += (_, _) => GeneralTargetChanged();
        selectedGamesView.SelectedIndexChanged += (_, _) => UpdateSelectionButtons();
        selectedGamesView.SizeChanged += (_, _) => ResizeSelectedColumns();
        selectedGamesView.FontChanged += (_, _) => ResizeSelectedColumns();
    }

    private void AddSelectedGame()
    {
        if (sessions.IsBusy || coordinator.IsBusy || libraryLoading || closing) return;
        SavedGame? game = ReadCurrentGame();
        if (game == null) return;
        if (selectedGames.Any(g => g.AppId == game.AppId))
        {
            SetStatus(UiText.Get("Bu oyun zaten seçilenler listesinde."));
            return;
        }
        selectedGames.Add(game);
        RefreshSelectedGames();
        QueueSettingsSave();
        SetStatus(UiText.Format($"{selectedGames.Count} oyun seçildi. Seçilenleri başlat düğmesine bas."));
    }

    private uint[] SelectedAppIds() => selectedGamesView.SelectedItems.Cast<ListViewItem>()
        .Select(item => (uint)item.Tag!).ToArray();

    private void EditSelectedTargets()
    {
        if (sessions.IsBusy || coordinator.IsBusy || libraryLoading || closing) return;
        var ids = SelectedAppIds();
        var gamesToEdit = selectedGames.Where(g => ids.Contains(g.AppId)).ToArray();
        if (gamesToEdit.Length == 0) return;
        using var dialog = new GameTargetDialog(gamesToEdit, GeneralTargetMinutes);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        for (int i = 0; i < selectedGames.Count; i++)
            if (ids.Contains(selectedGames[i].AppId))
            {
                if (selectedGames[i].TargetMinutes != dialog.TargetMinutes) CurrentProgress.Remove(selectedGames[i].AppId);
                selectedGames[i] = selectedGames[i] with { TargetMinutes = dialog.TargetMinutes };
            }
        sessions.ClearFinished();
        RefreshSelectedGames();
        foreach (ListViewItem item in selectedGamesView.Items)
            item.Selected = ids.Contains((uint)item.Tag!);
        QueueSettingsSave();
        SetStatus(UiText.Get("Süre hedefi kaydedildi. Bir sonraki Başlat'ta uygulanacak."));
    }

    private int GeneralTargetMinutes => autoStopBox.Checked ? (int)minutesBox.Value : 0;
    private static string ClockText(TimeSpan time) => $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}";
    private static string TargetText(int minutes, bool inherited) => minutes == 0 ? UiText.Get("Sınırsız") :
        (inherited ? UiText.Get("Genel ") : "") + $"{minutes / 60:00}:{minutes % 60:00}";

    private void RefreshSelectedGames()
    {
        selectedGamesView.BeginUpdate();
        selectedGamesView.Items.Clear();
        foreach (var game in selectedGames)
        {
            var item = new ListViewItem([game.Name, game.AppId.ToString(), UiText.Get("Bekliyor"), "00:00:00", "", ""])
            {
                Tag = game.AppId,
                ToolTipText = game.Name
            };
            selectedGamesView.Items.Add(item);
        }
        selectedGamesView.EndUpdate();
        UpdateSelectedRows();
        UpdateSelectionButtons();
    }

    private void UpdateSelectedRows()
    {
        var gamesById = selectedGames.ToDictionary(g => g.AppId);
        var runsById = sessions.Runs.ToDictionary(r => r.Game.AppId);
        var progress = CurrentProgress;
        foreach (ListViewItem item in selectedGamesView.Items)
        {
            uint id = (uint)item.Tag!;
            if (!gamesById.TryGetValue(id, out var game)) continue;
            runsById.TryGetValue(id, out var run);
            int target = cardFarm.IsBusy ? 0 : (sessions.IsBusy || coordinator.IsBusy) && run != null
                ? run.TargetMinutes : game.TargetMinutes ?? GeneralTargetMinutes;
            string targetText = TargetText(target, game.TargetMinutes == null);
            if (item.SubItems[4].Text != targetText) item.SubItems[4].Text = targetText;
            double completed = run?.IsActive == true ? run.CompletedSeconds : GameTargetProgress.Read(progress, game, target);
            string remaining = target == 0 ? "—" : ClockText(TimeSpan.FromSeconds(Math.Ceiling(Math.Max(0, target * 60d - completed))));
            if (item.SubItems[5].Text != remaining) item.SubItems[5].Text = remaining;
            string status = run?.Status switch
            {
                null => UiText.Get("Bekliyor"),
                GameSessionStatus.Starting => UiText.Get("Bağlanıyor…"),
                GameSessionStatus.Running => UiText.Get("Çalışıyor"),
                GameSessionStatus.Reconnecting => UiText.Get("Tekrar bağlantı"),
                GameSessionStatus.Stopping => UiText.Get("Durduruluyor…"),
                GameSessionStatus.Failed => UiText.Get("Hata"),
                _ => UiText.Get("Durduruldu")
            };
            if (run?.IsActive != true && target > 0 && completed > 0 && run?.Status != GameSessionStatus.Failed)
                status = completed >= target * 60d ? UiText.Get("Hedef bitti") : UiText.Get("Devam edebilir");
            string duration = ClockText(run?.Elapsed.Elapsed ?? TimeSpan.Zero);
            if (cardFarm.IsBusy && cardFarm.IsExcluded(id)) status = UiText.Get("Turdan çıkarıldı");
            if (item.SubItems[2].Text != status) item.SubItems[2].Text = status;
            if (item.SubItems[3].Text != duration) item.SubItems[3].Text = duration;
            string tooltip = game.Name + Environment.NewLine +
                (game.TargetMinutes == null ? UiText.Get("Genel süre sınırı") : UiText.Get("Özel süre hedefi")) + ": " +
                (target == 0 ? UiText.Get("Sınırsız") : UiText.Format($"{target / 60} saat {target % 60} dakika")) +
                (run == null ? "" : Environment.NewLine + run.Message);
            if (item.ToolTipText != tooltip) item.ToolTipText = tooltip;
        }
    }

    private void UpdateSelectionButtons()
    {
        var ids = SelectedAppIds();
        resetProgressButton.Enabled = !closing && !libraryLoading && !coordinator.IsBusy && !sessions.IsBusy &&
            (multiGameBox.Checked ? ids.Length > 0 : AppIdInput.TryParse(appIdBox.Text, out _));
        targetButton.Enabled = !closing && !libraryLoading && !coordinator.IsBusy && !sessions.IsBusy && ids.Length > 0;
        removeSelectedButton.Enabled = !closing && !coordinator.IsBusy && !sessions.IsBusy && ids.Length > 0;
        clearSelectedButton.Enabled = !closing && !coordinator.IsBusy && !sessions.IsBusy && selectedGames.Count > 0;
        stopSelectedButton.Enabled = !closing && (cardFarm.IsBusy ? ids.Any(id => !cardFarm.IsExcluded(id)) : sessions.Runs.Any(r =>
            ids.Contains(r.Game.AppId) && r.Status is GameSessionStatus.Starting or GameSessionStatus.Running or GameSessionStatus.Reconnecting));
    }

    private void ResizeSelectedColumns()
    {
        // Keep a five-row viewport, including when the hidden panel is DPI/font-scaled.
        int viewportHeight = TextRenderer.MeasureText("Ag", selectedGamesView.Font).Height * 6 + 12;
        if (selectedGamesView.Height != viewportHeight) selectedGamesView.Height = viewportHeight;
        multiLayout.RowStyles[1].Height = viewportHeight + selectedGamesView.Margin.Vertical;
        int Measure(string text) => TextRenderer.MeasureText(text, selectedGamesView.Font).Width + 20;
        selectedIdColumn.Width = Measure("0000000000");
        selectedStatusColumn.Width = Math.Max(Measure(UiText.Get("Durduruluyor…")), Measure(UiText.Get("Tekrar bağlantı")));
        selectedTimeColumn.Width = Measure("000:00:00");
        selectedTargetColumn.Width = Measure(UiText.Get("Genel 168:00"));
        selectedRemainingColumn.Width = Math.Max(Measure(UiText.Get("Kalan süre")), Measure("000:00:00"));
        selectedNameColumn.Width = Math.Max(80, selectedGamesView.ClientSize.Width
            - selectedIdColumn.Width - selectedStatusColumn.Width - selectedTimeColumn.Width
            - selectedTargetColumn.Width - selectedRemainingColumn.Width
            - SystemInformation.VerticalScrollBarWidth - 4);
    }
}
