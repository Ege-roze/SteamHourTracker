namespace SteamHourTracker;

internal sealed class CardQueueDialog : Form
{
    private readonly CardFarm farm;
    private readonly Func<CardFarmOptions> options;
    private readonly Func<bool> canStart;
    private readonly Func<bool> canEditExclusions;
    private readonly Label summary;
    private readonly ListView view;
    private readonly CheckBox activeOnly;
    private readonly Button start, configure, stop, skip, exclusions;
    private readonly Dictionary<uint, ListViewItem> items = [];
    private string? notice;

    internal CardQueueDialog(CardFarm farm, Func<CardFarmOptions>? options = null, Func<bool>? canStart = null,
        Func<Task>? startWork = null, Action? configureWork = null, Action? editExclusions = null, Func<bool>? canEditExclusions = null)
    {
        this.farm = farm; this.options = options ?? (() => new()); this.canStart = canStart ?? (() => false);
        this.canEditExclusions = canEditExclusions ?? (() => false);
        Text = UiText.Get("Kart modu"); Font = SystemFonts.MessageBoxFont;
        AutoScaleDimensions = new SizeF(7, 15); AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(880, 460); FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false; StartPosition = FormStartPosition.CenterParent; ShowInTaskbar = false;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(10) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        summary = new Label { Name = "cardQueueSummary", AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(3, 3, 3, 8) };
        activeOnly = new CheckBox { Name = "activeCardsOnlyBox", Text = UiText.Get("Yalnızca kart hakkı kalan oyunlar"), Checked = true, AutoSize = true, Margin = new Padding(3, 0, 3, 8) };
        view = new ListView { Name = "cardQueueView", View = View.Details, FullRowSelect = true, Dock = DockStyle.Fill, HideSelection = false, ShowItemToolTips = true };
        foreach (var text in new[] { UiText.Get("Oyun"), UiText.Get("Steam saati"), UiText.Get("Toplam süre"), UiText.Get("Hazırlığa kalan"), UiText.Get("Kalan kart"), UiText.Get("Durum") }) view.Columns.Add(text);
        var note = new Label { Name = "cardQueueNote", AutoSize = true, Dock = DockStyle.Fill,
            Text = UiText.Get("Toplam süre: Steam saati ve bu çalışmadaki bağlı süreyle hesaplanır. Hazırlığa kalan canlı güncellenir.\nBu, kartın düşmesine kalan süre değildir; kartın ne zaman düşeceği bilinmez.\nSeçileni atla yalnızca bu çalışmayı etkiler. Kalıcı hariç listesini düzenlemek için kart modunu durdur."), Margin = new Padding(3, 6, 3, 6) };
        var actions = new FlowLayoutPanel { Name = "cardQueueActions", AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
        start = new Button { Text = UiText.Get("Başlat"), AutoSize = true, Name = "startCardQueueButton", Visible = startWork != null };
        configure = new Button { Text = UiText.Get("Ayarlar"), AutoSize = true, Name = "configureCardQueueButton", Visible = configureWork != null };
        stop = new Button { Text = UiText.Get("Durdur"), AutoSize = true, Name = "stopCardQueueButton" };
        var refresh = new Button { Text = UiText.Get("Yenile"), AutoSize = true, Name = "refreshCardQueueButton" };
        skip = new Button { Text = UiText.Get("Seçileni atla"), AutoSize = true, Name = "skipCardQueueButton" };
        exclusions = new Button { Text = UiText.Get("Hariç tutulanlar…"), AutoSize = true, Name = "cardExclusionsButton", Visible = editExclusions != null };
        var close = new Button { Text = UiText.Get("Kapat"), AutoSize = true, Name = "closeCardQueueButton" };
        actions.Controls.AddRange([start, configure, stop, refresh, skip, exclusions, close]);
        layout.Controls.Add(summary, 0, 0); layout.Controls.Add(activeOnly, 0, 1); layout.Controls.Add(view, 0, 2);
        layout.Controls.Add(note, 0, 3); layout.Controls.Add(actions, 0, 4);
        Controls.Add(layout); CancelButton = close;
        close.Click += (_, _) => Close();
        refresh.Click += (_, _) => RefreshRows();
        activeOnly.CheckedChanged += (_, _) => RefreshRows();
        view.SelectedIndexChanged += (_, _) => UpdateButtons();
        configure.Click += (_, _) => { configureWork?.Invoke(); RefreshRows(); };
        exclusions.Click += (_, _) => { if (this.canEditExclusions()) editExclusions?.Invoke(); RefreshRows(); };
        start.Click += async (_, _) =>
        {
            if (startWork == null || !this.canStart()) return;
            notice = null; start.Enabled = false;
            await startWork();
            if (!IsDisposed) RefreshRows();
        };
        stop.Click += async (_, _) => { stop.Enabled = false; await farm.StopAsync(); if (!IsDisposed) RefreshRows(); };
        skip.Click += async (_, _) =>
        {
            var ids = view.SelectedItems.Cast<ListViewItem>().Select(i => (uint)i.Tag!).ToArray();
            if (ids.Length == 0 || !farm.IsBusy) return;
            skip.Enabled = false; await farm.SkipAsync(ids); if (!IsDisposed) RefreshRows();
        };
        Shown += (_, _) => { ResizeColumns(); RefreshRows(); };
        view.SizeChanged += (_, _) => ResizeColumns();
        FontChanged += (_, _) => ResizeColumns();
    }

    internal void ShowNotice(string text) { notice = text; RefreshRows(); }

    internal void ApplyLanguage()
    {
        if (IsDisposed || Disposing) return;
        SuspendLayout();
        Text = UiText.Get("Kart modu");
        activeOnly.Text = UiText.Get("Yalnızca kart hakkı kalan oyunlar");
        start.Text = UiText.Get("Başlat");
        configure.Text = UiText.Get("Ayarlar");
        stop.Text = UiText.Get("Durdur");
        skip.Text = UiText.Get("Seçileni atla");
        exclusions.Text = UiText.Get("Hariç tutulanlar…");
        Controls.Find("refreshCardQueueButton", true)[0].Text = UiText.Get("Yenile");
        Controls.Find("closeCardQueueButton", true)[0].Text = UiText.Get("Kapat");
        Controls.Find("cardQueueNote", true)[0].Text = UiText.Get("Toplam süre: Steam saati ve bu çalışmadaki bağlı süreyle hesaplanır. Hazırlığa kalan canlı güncellenir.\nBu, kartın düşmesine kalan süre değildir; kartın ne zaman düşeceği bilinmez.\nSeçileni atla yalnızca bu çalışmayı etkiler. Kalıcı hariç listesini düzenlemek için kart modunu durdur.");
        string[] titles = ["Oyun", "Steam saati", "Toplam süre", "Hazırlığa kalan", "Kalan kart", "Durum"];
        for (int i = 0; i < titles.Length; i++) view.Columns[i].Text = UiText.Get(titles[i]);
        ResizeColumns();
        RefreshRows();
        ResumeLayout(true);
    }

    internal void RefreshRows()
    {
        if (IsDisposed || Disposing) return;
        var mode = options();
        string message = farm.Message.Length == 0 ? UiText.Get("Başlat'a basınca oyunlar Steam'den okunup burada listelenir.") : farm.Message;
        summary.Text = (mode.Automatic ? UiText.Format($"Hazırlık eşiği: {mode.HoursBeforeDrops} saat • {farm.GamesRemaining} oyun / {farm.CardsRemaining} kart\n") : UiText.Get("Manuel kart modu\n")) +
            (notice != null && !farm.IsBusy ? notice : message);
        var rows = farm.QueueRows.Where(r => !activeOnly.Checked || r.Remaining == null && !farm.IsAutomatic || r.Remaining > 0).ToArray();
        var ids = rows.Select(r => r.AppId).ToHashSet();
        view.BeginUpdate();
        foreach (uint id in items.Keys.Where(id => !ids.Contains(id)).ToArray()) { view.Items.Remove(items[id]); items.Remove(id); }
        foreach (var row in rows)
        {
            string[] values = [row.Name, row.SteamHours.HasValue ? row.SteamHours.Value.ToString("0.0", UiText.Culture) + UiText.Get(" sa") : "—",
                row.Hours.HasValue ? Clock(TimeSpan.FromHours(row.Hours.Value)) : "—",
                row.PreparationRemaining.HasValue ? row.PreparationRemaining.Value > TimeSpan.Zero ? Clock(row.PreparationRemaining.Value, true) : UiText.Get("Hazır") : "—",
                row.Remaining?.ToString() ?? "—", row.Status];
            if (!items.TryGetValue(row.AppId, out var item))
            {
                item = new ListViewItem(values) { Tag = row.AppId }; items.Add(row.AppId, item); view.Items.Add(item);
            }
            else for (int i = 0; i < values.Length; i++) if (item.SubItems[i].Text != values[i]) item.SubItems[i].Text = values[i];
            item.ToolTipText = row.Name + " • App ID " + row.AppId + " • " + row.Status;
        }
        view.EndUpdate(); UpdateButtons();
    }

    private void UpdateButtons()
    {
        start.Enabled = configure.Enabled = canStart();
        exclusions.Enabled = canEditExclusions();
        stop.Enabled = farm.IsBusy && farm.Phase != CardFarmPhase.Stopping;
        skip.Enabled = farm.IsBusy && view.SelectedItems.Cast<ListViewItem>().Any(i => !farm.IsExcluded((uint)i.Tag!));
    }

    private void ResizeColumns()
    {
        if (view == null || view.Columns.Count != 6) return;
        string[] samples = ["", UiText.Get("Steam saati"), "0000:00:00", UiText.Get("Hazırlığa kalan"), UiText.Get("Kalan kart"), UiText.Get("Kart düşmesi bekleniyor")];
        for (int i = 1; i < samples.Length; i++) view.Columns[i].Width = TextRenderer.MeasureText(samples[i], view.Font).Width + 18;
        view.Columns[0].Width = Math.Max(120, view.ClientSize.Width - view.Columns.Cast<ColumnHeader>().Skip(1).Sum(c => c.Width) - SystemInformation.VerticalScrollBarWidth - 6);
    }

    private static string Clock(TimeSpan time, bool roundUp = false)
    {
        long seconds = (long)(roundUp ? Math.Ceiling(time.TotalSeconds) : Math.Floor(time.TotalSeconds));
        return $"{seconds / 3600:00}:{seconds / 60 % 60:00}:{seconds % 60:00}";
    }
}
