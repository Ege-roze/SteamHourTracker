namespace SteamHourTracker;

public partial class Form1
{
    private Button languageButton = null!;
    private ToolStripMenuItem turkishLanguageItem = null!;
    private ToolStripMenuItem englishLanguageItem = null!;
    private bool changingLanguage;

    private void InitializeLanguageControls()
    {
        var menu = new ContextMenuStrip(components!);
        turkishLanguageItem = new ToolStripMenuItem("Türkçe") { Name = "turkishLanguageItem" };
        englishLanguageItem = new ToolStripMenuItem("English") { Name = "englishLanguageItem" };
        menu.Items.AddRange([turkishLanguageItem, englishLanguageItem]);
        turkishLanguageItem.Click += (_, _) => ChangeLanguage("tr");
        englishLanguageItem.Click += (_, _) => ChangeLanguage("en");
        languageButton = new Button
        {
            Name = "languageButton", Text = "Dil / Language", AutoSize = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Right, Margin = new Padding(8, 0, 0, 8),
            ContextMenuStrip = menu, UseVisualStyleBackColor = true
        };
        languageButton.Click += (_, _) => menu.Show(languageButton, new Point(0, languageButton.Height));
        var header = new TableLayoutPanel
        {
            Name = "languageHeader", AutoSize = true, Dock = DockStyle.Fill,
            ColumnCount = 2, RowCount = 1, Margin = Padding.Empty
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        mainLayout.Controls.Remove(subtitleLabel);
        subtitleLabel.Dock = DockStyle.Fill;
        header.Controls.Add(subtitleLabel, 0, 0);
        header.SetColumnSpan(subtitleLabel, 1);
        header.Controls.Add(languageButton, 1, 0);
        mainLayout.Controls.Add(header, 0, 0);
        mainLayout.SetColumnSpan(header, 2);
    }

    private void ChangeLanguage(string language)
    {
        // Workers keep the language they were started with. Change it only while
        // idle, without stopping games or recreating controls holding user data.
        if (closing || libraryLoading || sessions.IsBusy || coordinator.IsBusy || cardFarm.IsBusy || cardStarting) return;
        language = UiText.Normalize(language);
        if (UiText.Language == language) return;
        FlushSettings();
        preferences.Language = language;
        UiText.SetLanguage(language);
        ApplyLanguage();
        FlushSettings();
        SetStatus(UiText.Get("Dil değiştirildi."));
        cardWindow?.ApplyLanguage();
        cardWindow?.ShowNotice(UiText.Get("Dil değiştirildi."));
    }

    private void ApplyLanguage()
    {
        changingLanguage = true;
        SuspendLayout();
        mainLayout.SuspendLayout();
        try
        {
            turkishLanguageItem.Checked = UiText.Language == "tr";
            englishLanguageItem.Checked = UiText.Language == "en";
            sourceLabel.Text = UiText.Get("Oyun listesi:");
            removeButton.Text = UiText.Get("Sil");
            refreshLibraryButton.Text = UiText.Get("Kütüphaneyi yenile");
            searchLabel.Text = UiText.Get("Ara:");
            searchBox.PlaceholderText = UiText.Get("Oyun adı veya App ID");
            gameLabel.Text = UiText.Get("Oyun:");
            appIdBox.PlaceholderText = UiText.Get("Örn. 730");
            nameLabel.Text = UiText.Get("Oyun adı:");
            saveButton.Text = UiText.Get("Kaydet");
            limitLabel.Text = UiText.Get("Süre sınırı:");
            autoStopBox.Text = UiText.Get("Otomatik durdur");
            minutesLabel.Text = UiText.Get("dakika sonra");
            multiGameBox.Text = UiText.Get("Birden fazla oyun");
            cardModeButton.Text = UiText.Get("Kart modu…");
            invisibleModeBox.Text = UiText.Get("Saat kasarken görünmez moda geç");
            addSelectedButton.Text = UiText.Get("Ekle");
            removeSelectedButton.Text = UiText.Get("Listeden çıkar");
            clearSelectedButton.Text = UiText.Get("Listeyi temizle");
            stopSelectedButton.Text = UiText.Get("Seçileni durdur");
            targetButton.Text = UiText.Get("Süre hedefi");
            selectedNameColumn.Text = UiText.Get("Oyun");
            selectedStatusColumn.Text = UiText.Get("Durum");
            selectedTimeColumn.Text = UiText.Get("Süre");
            selectedTargetColumn.Text = UiText.Get("Hedef");
            selectedRemainingColumn.Text = UiText.Get("Kalan süre");
            weeklySummaryButton.Text = UiText.Get("Haftalık özet");
            resetProgressButton.Text = UiText.Get("Hedefi sıfırla");
            startButton.Text = UiText.Get(multiGameBox.Checked ? "Seçilenleri başlat" : "Başlat");
            stopButton.Text = UiText.Get(multiGameBox.Checked ? "Hepsini durdur" : "Durdur");
            sessionLabel.Text = UiText.Get("Oturum:");
            elapsedCaption.Text = UiText.Get("Geçen süre:");
            timeCaption.Text = UiText.Get("Sayaç yalnızca bu oturumun süresini gösterir; Steam'deki toplam süre değildir.");
            statusCaption.Text = UiText.Get("Durum:");
            minimizeToTrayBox.Text = UiText.Get("Küçültünce sistem tepsisinde çalışmaya devam et");
            showMenuItem.Text = UiText.Get("Göster");
            stopMenuItem.Text = UiText.Get("Durdur");
            exitMenuItem.Text = UiText.Get("Çıkış");
            trayIcon.Text = UiText.Get("SteamHourTracker • durduruldu");
            sourceBox.Items[0] = UiText.Get("Steam kütüphanesi");
            sourceBox.Items[1] = UiText.Get("Elle kaydedilenler");
            // Refresh cached display strings without changing the current/manual
            // App ID, name, search filter, selection, targets or measured progress.
            gamesBox.BeginUpdate();
            try
            {
                for (int i = 0; i < gamesBox.Items.Count; i++) gamesBox.Items[i] = gamesBox.Items[i];
            }
            finally { gamesBox.EndUpdate(); }
            subtitleLabel.Text = library is { } result
                ? UiText.Format($"{result.AccountName}  •  {result.Games.Count} oyun  •  {result.Games.Count(g => g.Installed)} kurulu")
                : UiText.Get("Steam hesabı bekleniyor.");
            if (sessions.Runs.Count == 0) activeGameLabel.Text = UiText.Get("Henüz oturum başlatılmadı");
            else activeGameLabel.Text = sessions.Runs.Count == 1 ? sessions.Runs[0].Game.Name
                : UiText.Format($"Oturumlar sona erdi. {sessions.Runs.Count(r => r.Status == GameSessionStatus.Failed)} hata.");
            SetStatus(UiText.Get("Hazır. Steam açıkken bir oyun seçip Başlat'a bas."));
            UpdateSelectedRows();
            ResizeSelectedColumns();
            UpdateTime();
        }
        finally
        {
            mainLayout.ResumeLayout(true);
            ResumeLayout(true);
            changingLanguage = false;
        }
    }
}
