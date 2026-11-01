namespace SteamHourTracker;

internal sealed class CardExclusionsDialog : Form
{
    private readonly HashSet<uint> excluded;
    private readonly SavedGame[] games;
    private readonly CheckedListBox list;
    private readonly TextBox search;
    private readonly Label count;
    private bool refreshing;

    internal HashSet<uint> ExcludedAppIds => new(excluded);

    internal CardExclusionsDialog(IEnumerable<SavedGame> availableGames, IEnumerable<uint> excludedIds)
    {
        excluded = excludedIds.Where(id => id > 0).ToHashSet();
        games = availableGames.Concat(excluded.Select(id => new SavedGame(id, $"App ID {id}")))
            .Where(g => g.AppId > 0).DistinctBy(g => g.AppId).OrderBy(g => g.Name).ToArray();
        Text = UiText.Get("Kart modunda hariç tutulanlar");
        Font = SystemFonts.MessageBoxFont;
        AutoScaleDimensions = new SizeF(7, 15); AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(590, 430); FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false; ShowInTaskbar = false; StartPosition = FormStartPosition.CenterParent;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(10), ColumnCount = 1, RowCount = 5 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var note = new Label { AutoSize = true, Dock = DockStyle.Fill,
            Text = UiText.Get("İşaretli oyunlar bu hesapta kart modunda çalıştırılmaz.\nYeniden dahil etmek için işaretini kaldırıp Kaydet'e bas. Normal saat modu etkilenmez.") };
        search = new TextBox { Name = "cardExclusionsSearch", Dock = DockStyle.Fill, PlaceholderText = UiText.Get("Oyun adı veya App ID ile ara") };
        list = new CheckedListBox { Name = "cardExclusionsList", Dock = DockStyle.Fill, CheckOnClick = true,
            IntegralHeight = false, HorizontalScrollbar = true };
        count = new Label { Name = "cardExclusionsCount", AutoSize = true, Dock = DockStyle.Fill };
        var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var save = new Button { Name = "saveCardExclusionsButton", Text = UiText.Get("Kaydet"), AutoSize = true, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = UiText.Get("Vazgeç"), AutoSize = true, DialogResult = DialogResult.Cancel };
        actions.Controls.AddRange([save, cancel]);
        layout.Controls.Add(note, 0, 0); layout.Controls.Add(search, 0, 1); layout.Controls.Add(list, 0, 2);
        layout.Controls.Add(count, 0, 3); layout.Controls.Add(actions, 0, 4);
        Controls.Add(layout); AcceptButton = save; CancelButton = cancel;
        search.TextChanged += (_, _) => RefreshGames();
        list.ItemCheck += (_, e) =>
        {
            if (refreshing) return;
            uint id = ((SavedGame)list.Items[e.Index]).AppId;
            if (e.NewValue == CheckState.Checked) excluded.Add(id); else excluded.Remove(id);
            UpdateCount();
        };
        RefreshGames();
    }

    private void RefreshGames()
    {
        refreshing = true;
        list.BeginUpdate();
        try
        {
            list.Items.Clear();
            string query = search.Text.Trim();
            foreach (var game in games.Where(g => query.Length == 0 || g.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                g.AppId.ToString(System.Globalization.CultureInfo.InvariantCulture).Contains(query)))
                list.Items.Add(game, excluded.Contains(game.AppId));
        }
        finally { list.EndUpdate(); refreshing = false; }
        UpdateCount();
    }

    private void UpdateCount() => count.Text = UiText.Format($"{excluded.Count} oyun kalıcı olarak hariç tutuluyor. Arama diğer seçimleri korur.");
}
