namespace SteamHourTracker;

internal sealed class CardFarmDialog : Form
{
    public CardFarmOptions Options { get; private set; }

    internal CardFarmDialog(int gameCount, CardFarmOptions options, bool startOnAccept = true)
    {
        Options = options.Normalize();
        Text = UiText.Get("Kart düşürme modu");
        Font = SystemFonts.MessageBoxFont;
        AutoScaleDimensions = new SizeF(7, 15);
        AutoScaleMode = AutoScaleMode.Font;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        var layout = new TableLayoutPanel
        {
            Name = "cardSettingsLayout", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2, RowCount = 9, Dock = DockStyle.Fill, Padding = new Padding(12)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        for (int i = 0; i < 9; i++) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var title = new Label { Name = "cardSettingsTitle", Text = UiText.Format($"{gameCount} oyun • Birlikte çalıştır → bekle → tek tek aç/kapa"), AutoSize = true, Margin = new Padding(3, 3, 3, 12) };
        layout.Controls.Add(title, 0, 0); layout.SetColumnSpan(title, 2);
        var automatic = new CheckBox { Name = "automaticCardsBox", Text = UiText.Get("Tüm kütüphaneyi otomatik tara ve kartları tamamla"), AutoSize = true, Checked = Options.Automatic, Margin = new Padding(3, 3, 3, 10) };
        layout.Controls.Add(automatic, 0, 1); layout.SetColumnSpan(automatic, 2);
        NumericUpDown Field(int row, string caption, string name, int min, int max, int value)
        {
            layout.Controls.Add(new Label { Text = caption, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 8, 12, 8) }, 0, row);
            var input = new NumericUpDown { Name = name, Minimum = min, Maximum = max, Value = value, Width = 115, Anchor = AnchorStyles.Left, Margin = new Padding(3, 5, 3, 5) };
            layout.Controls.Add(input, 1, row);
            return input;
        }
        var hours = Field(2, UiText.Get("Hazırlık eşiği (saat, 0 = hazırlama yok):"), "cardPreparationHours", 0, 10, Options.HoursBeforeDrops);
        var together = Field(3, UiText.Get("Birlikte çalışma / tarama aralığı (dakika):"), "cardTogetherMinutes", 1, 180, Options.TogetherMinutes);
        var single = Field(4, UiText.Get("Tekli aç/kapa (saniye):"), "cardSingleSeconds", 5, 120, Options.SingleSeconds);
        var pause = Field(5, UiText.Get("Birlikte çalışmadan sonra ara (saniye):"), "cardPauseSeconds", 5, 120, Options.PauseSeconds);
        var rounds = Field(6, UiText.Get("Manuel tur sayısı (0 = durdurana kadar):"), "cardRounds", 0, 100, Options.Rounds);
        var note = new Label
        {
            Name = "cardSettingsNote", AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(3, 12, 3, 12),
            Text = UiText.Get("Kart hakkı kalan oyunları seç. Kart sayısı otomatik okunmaz;\nkartlar bitince kendiliğinden durmaz. Düşüş hızını Steam belirler.\nBu mod normal süre hedeflerini değiştirmez.\nKalan kartları Steam'deki Rozetler sayfandan kontrol edebilirsin.")
        };
        void UpdateMode()
        {
            title.Text = automatic.Checked ? UiText.Get("Kart hakkı kalan oyunlar otomatik seçilir; en fazla 30'u aynı anda çalışır.") : UiText.Format($"{gameCount} seçili oyun • Birlikte → ara → tekli aç/kapa");
            hours.Enabled = automatic.Checked; rounds.Enabled = !automatic.Checked;
            note.Text = automatic.Checked
                ? UiText.Get("Steam'e bir kez web girişini yap. Kartı biten oyunlar atlanır;\nhepsi bitince durur. Az saatli oyunların hazırlığı otomatik yapılır.\nSaat eşiği Steam'den kesin okunamaz; 2 saat varsayılan bir ayardır.\nDüşüş hızını Steam belirler. Normal süre hedeflerin korunur.")
                : UiText.Get("Yalnızca mevcut seçili oyunlar kullanılır; kart sayısı okunmaz.\nKartlar bitince kendiliğinden durmaz. Düşüş hızını Steam belirler.\nNormal süre hedeflerin korunur.");
        }
        automatic.CheckedChanged += (_, _) => UpdateMode(); UpdateMode();
        layout.Controls.Add(note, 0, 7); layout.SetColumnSpan(note, 2);
        var actions = new FlowLayoutPanel { Name = "cardSettingsActions", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Anchor = AnchorStyles.Right, WrapContents = false };
        var start = new Button { Name = "startCardModeButton", Text = startOnAccept ? UiText.Get("Kart modunu başlat") : UiText.Get("Ayarları kaydet"), AutoSize = true, MinimumSize = new Size(130, 32), UseVisualStyleBackColor = true };
        var cancel = new Button { Name = "cancelCardModeButton", Text = UiText.Get("Vazgeç"), AutoSize = true, MinimumSize = new Size(85, 32), DialogResult = DialogResult.Cancel, UseVisualStyleBackColor = true };
        start.Click += (_, _) =>
        {
            Options = new((int)together.Value, (int)single.Value, (int)pause.Value, (int)rounds.Value, automatic.Checked, (int)hours.Value);
            DialogResult = DialogResult.OK;
        };
        actions.Controls.AddRange([start, cancel]);
        layout.Controls.Add(actions, 0, 8); layout.SetColumnSpan(actions, 2);
        Controls.Add(layout);
        AcceptButton = start; CancelButton = cancel;
    }
}
