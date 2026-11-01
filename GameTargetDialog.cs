namespace SteamHourTracker;

internal sealed class GameTargetDialog : Form
{
    private readonly RadioButton generalOption = new() { Name = "generalOption", Text = UiText.Get("Genel süre sınırını kullan"), AutoSize = true };
    private readonly RadioButton customOption = new() { Name = "customOption", Text = UiText.Get("Özel süre hedefi"), AutoSize = true };
    private readonly NumericUpDown hoursBox = new() { Name = "hoursBox", Minimum = 0, Maximum = 168, Width = 70 };
    private readonly NumericUpDown targetMinutesBox = new() { Name = "targetMinutesBox", Minimum = 0, Maximum = 59, Width = 70 };
    private readonly Label errorLabel = new() { Name = "errorLabel", AutoSize = true, MaximumSize = new Size(430, 0) };
    internal int? TargetMinutes { get; private set; }

    internal GameTargetDialog(SavedGame[] games, int generalMinutes)
    {
        Text = UiText.Get("Oyun süre hedefi");
        Font = SystemFonts.MessageBoxFont;
        AutoScaleDimensions = new SizeF(7, 15);
        AutoScaleMode = AutoScaleMode.Font;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(460, 0);

        var layout = new TableLayoutPanel
        {
            Name = "targetLayout", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill, Padding = new Padding(14), ColumnCount = 1, RowCount = 8
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        for (int i = 0; i < layout.RowCount; i++) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Label Caption(string text) => new()
        {
            Text = text, AutoSize = true, MaximumSize = new Size(430, 0), Margin = new Padding(3, 4, 3, 8)
        };
        bool mixed = games.Select(g => g.TargetMinutes).Distinct().Count() > 1;
        layout.Controls.Add(Caption(games.Length == 1 ? games[0].Name :
            UiText.Format($"{games.Length} oyun seçildi. Uygula, hepsine aynı hedefi verir.") +
            (mixed ? UiText.Get(" Mevcut hedefler farklı.") : "")), 0, 0);
        layout.Controls.Add(generalOption, 0, 1);
        layout.Controls.Add(Caption(generalMinutes == 0 ? UiText.Get("Genel sınır kapalı: sınırsız.") :
            UiText.Format($"Genel sınır: {generalMinutes / 60} saat {generalMinutes % 60} dakika.")), 0, 2);
        layout.Controls.Add(customOption, 0, 3);
        var duration = new FlowLayoutPanel
        {
            Name = "durationLayout", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false, Margin = new Padding(20, 3, 3, 5)
        };
        duration.Controls.AddRange([hoursBox, Caption(UiText.Get("saat")), targetMinutesBox, Caption(UiText.Get("dakika"))]);
        layout.Controls.Add(duration, 0, 4);
        layout.Controls.Add(Caption(UiText.Get("Hedef 1 dakika–168 saat arasıdır. Başlat, kalan süreden devam eder. Hedefi değiştirirsen ilerleme sıfırlanır; Steam'deki toplam süre hedefi değildir.")), 0, 5);
        layout.Controls.Add(errorLabel, 0, 6);
        var buttons = new FlowLayoutPanel
        {
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight, WrapContents = false,
            Anchor = AnchorStyles.Right, Margin = new Padding(3, 8, 3, 3)
        };
        var applyButton = new Button
        {
            Name = "applyTargetButton", Text = UiText.Get("Uygula"), AutoSize = true,
            MinimumSize = new Size(85, 30), UseVisualStyleBackColor = true
        };
        var cancelButton = new Button
        {
            Name = "cancelTargetButton", Text = UiText.Get("Vazgeç"), AutoSize = true,
            MinimumSize = new Size(85, 30), UseVisualStyleBackColor = true, DialogResult = DialogResult.Cancel
        };
        buttons.Controls.AddRange([applyButton, cancelButton]);
        layout.Controls.Add(buttons, 0, 7);
        Controls.Add(layout);
        AcceptButton = applyButton;
        CancelButton = cancelButton;

        customOption.CheckedChanged += (_, _) =>
        {
            hoursBox.Enabled = targetMinutesBox.Enabled = customOption.Checked;
            errorLabel.Text = "";
        };
        hoursBox.ValueChanged += (_, _) => targetMinutesBox.Maximum = hoursBox.Value == 168 ? 0 : 59;
        int? initialTarget = mixed ? null : games[0].TargetMinutes;
        int initialMinutes = initialTarget ?? (generalMinutes > 0 ? generalMinutes : 60);
        hoursBox.Value = initialMinutes / 60;
        targetMinutesBox.Value = initialMinutes % 60;
        customOption.Checked = initialTarget != null;
        generalOption.Checked = initialTarget == null;
        hoursBox.Enabled = targetMinutesBox.Enabled = customOption.Checked;
        applyButton.Click += (_, _) =>
        {
            int total = (int)(hoursBox.Value * 60 + targetMinutesBox.Value);
            if (customOption.Checked && total == 0)
            {
                errorLabel.Text = UiText.Get("En az 1 dakika seç.");
                targetMinutesBox.Focus();
                return;
            }
            TargetMinutes = customOption.Checked ? total : null;
            DialogResult = DialogResult.OK;
        };
    }
}
