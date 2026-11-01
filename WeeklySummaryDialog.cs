using System.Globalization;

namespace SteamHourTracker;

internal sealed class WeeklySummaryDialog : Form
{
    private readonly Label weekLabel = new() { Name = "weekLabel", AutoSize = true, Dock = DockStyle.Fill };
    private readonly Label totalLabel = new() { Name = "weeklyTotalLabel", AutoSize = true, Dock = DockStyle.Fill };
    private readonly ListView gamesView = new()
    {
        Name = "weeklyGamesView", View = View.Details, FullRowSelect = true, HideSelection = false,
        ShowItemToolTips = true, Dock = DockStyle.Fill, MultiSelect = false
    };

    internal WeeklySummaryDialog(string accountName, Func<WeeklyPlaytime> read)
    {
        Text = UiText.Get("Haftalık özet");
        Font = SystemFonts.MessageBoxFont;
        AutoScaleDimensions = new SizeF(7, 15);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(540, 380);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        var layout = new TableLayoutPanel
        {
            Name = "weeklyLayout", Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Padding = new Padding(12)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 6; i++) layout.RowStyles.Add(new RowStyle(i == 2 ? SizeType.Percent : SizeType.AutoSize, i == 2 ? 100 : 0));
        layout.Controls.Add(new Label { Text = accountName, Name = "weeklyAccountLabel", AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(3, 3, 3, 8) }, 0, 0);
        weekLabel.Margin = new Padding(3, 0, 3, 8);
        layout.Controls.Add(weekLabel, 0, 1);
        gamesView.Columns.Add(UiText.Get("Oyun"));
        gamesView.Columns.Add("App ID");
        gamesView.Columns.Add(UiText.Get("Bu hafta"));
        layout.Controls.Add(gamesView, 0, 2);
        totalLabel.Margin = new Padding(3, 8, 3, 4);
        layout.Controls.Add(totalLabel, 0, 3);
        layout.Controls.Add(new Label
        {
            AutoSize = true, Dock = DockStyle.Fill, Name = "weeklyNoteLabel", Margin = new Padding(3, 3, 3, 8),
            Text = UiText.Get("Bu uygulamada ölçülen sürelerdir. Aynı anda çalışan oyunların süreleri ayrı sayılır. Yeni hafta pazartesi başlar; önceki hafta saklanmaz.")
        }, 0, 4);
        var actions = new FlowLayoutPanel
        {
            Name = "weeklyActions", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.Right, WrapContents = false
        };
        var refresh = new Button { Name = "refreshWeeklyButton", Text = UiText.Get("Yenile"), AutoSize = true, MinimumSize = new Size(85, 30), UseVisualStyleBackColor = true };
        var close = new Button { Name = "closeWeeklyButton", Text = UiText.Get("Kapat"), AutoSize = true, MinimumSize = new Size(85, 30), UseVisualStyleBackColor = true, DialogResult = DialogResult.Cancel };
        actions.Controls.AddRange([refresh, close]);
        layout.Controls.Add(actions, 0, 5);
        Controls.Add(layout);
        CancelButton = close;
        void ResizeColumns()
        {
            int Measure(string text) => TextRenderer.MeasureText(text, gamesView.Font).Width + 20;
            gamesView.Columns[1].Width = Measure("0000000000");
            gamesView.Columns[2].Width = Measure(UiText.Get("168 saat 59 dk"));
            gamesView.Columns[0].Width = Math.Max(100, gamesView.ClientSize.Width - gamesView.Columns[1].Width - gamesView.Columns[2].Width - SystemInformation.VerticalScrollBarWidth - 4);
        }
        gamesView.SizeChanged += (_, _) => ResizeColumns();
        gamesView.FontChanged += (_, _) => ResizeColumns();
        void RefreshSummary()
        {
            var snapshot = read();
            DateOnly start = DateOnly.ParseExact(snapshot.WeekStart, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            weekLabel.Text = UiText.Format($"{start:dd.MM.yyyy} – {start.AddDays(6):dd.MM.yyyy} • Pazartesi–Pazar");
            gamesView.BeginUpdate();
            try
            {
                gamesView.Items.Clear();
                foreach (var pair in snapshot.Games.OrderByDescending(p => p.Value.Seconds).ThenBy(p => p.Value.Name))
                    gamesView.Items.Add(new ListViewItem([pair.Value.Name, pair.Key.ToString(), WeeklyPlaytime.Duration(pair.Value.Seconds)])
                    { Tag = pair.Key, ToolTipText = pair.Value.Name + " • " + WeeklyPlaytime.Duration(pair.Value.Seconds) });
                totalLabel.Text = snapshot.Games.Count == 0 ? UiText.Get("Bu hafta henüz kayıt yok.") :
                    UiText.Get("Toplam oyun süresi: ") + WeeklyPlaytime.Duration(snapshot.Games.Values.Sum(g => g.Seconds));
            }
            finally { gamesView.EndUpdate(); }
            ResizeColumns();
        }
        refresh.Click += (_, _) => RefreshSummary();
        Shown += (_, _) => RefreshSummary();
    }
}
