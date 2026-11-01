#nullable enable
namespace SteamHourTracker;

partial class Form1
{
    private System.ComponentModel.IContainer? components;
    private TableLayoutPanel mainLayout = null!;
    private TableLayoutPanel sourceLayout = null!;
    private TableLayoutPanel nameLayout = null!;
    private FlowLayoutPanel limitLayout = null!;
    private FlowLayoutPanel actionLayout = null!;
    private CheckBox multiGameBox = null!;
    private CheckBox invisibleModeBox = null!;
    private FlowLayoutPanel optionsLayout = null!;
    private TableLayoutPanel multiLayout = null!;
    private FlowLayoutPanel selectionActions = null!;
    private ListView selectedGamesView = null!;
    private ColumnHeader selectedNameColumn = null!;
    private ColumnHeader selectedIdColumn = null!;
    private ColumnHeader selectedStatusColumn = null!;
    private ColumnHeader selectedTimeColumn = null!;
    private ColumnHeader selectedTargetColumn = null!;
    private ColumnHeader selectedRemainingColumn = null!;
    private Button addSelectedButton = null!;
    private Button removeSelectedButton = null!;
    private Button clearSelectedButton = null!;
    private Button stopSelectedButton = null!;
    private Button targetButton = null!;
    private ComboBox gamesBox = null!;
    private ComboBox sourceBox = null!;
    private TextBox searchBox = null!;
    private Button refreshLibraryButton = null!;
    private TextBox appIdBox = null!;
    private TextBox gameNameBox = null!;
    private Button saveButton = null!;
    private Button removeButton = null!;
    private Button startButton = null!;
    private Button stopButton = null!;
    private Button resetProgressButton = null!;
    private Button weeklySummaryButton = null!;
    private Button cardModeButton = null!;
    private CheckBox autoStopBox = null!;
    private NumericUpDown minutesBox = null!;
    private CheckBox minimizeToTrayBox = null!;
    private Label subtitleLabel = null!;
    private Label activeGameLabel = null!;
    private Label elapsedLabel = null!;
    private TextBox statusLabel = null!;
    private Label sourceLabel = null!;
    private Label searchLabel = null!;
    private Label gameLabel = null!;
    private Label idLabel = null!;
    private Label nameLabel = null!;
    private Label limitLabel = null!;
    private Label minutesLabel = null!;
    private Label sessionLabel = null!;
    private Label elapsedCaption = null!;
    private Label timeCaption = null!;
    private Label statusCaption = null!;
    private System.Windows.Forms.Timer uiTimer = null!;
    private NotifyIcon trayIcon = null!;
    private ContextMenuStrip trayMenu = null!;
    private ToolStripMenuItem showMenuItem = null!;
    private ToolStripMenuItem stopMenuItem = null!;
    private ToolStripMenuItem exitMenuItem = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing) components?.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        mainLayout = new TableLayoutPanel();
        subtitleLabel = new Label();
        sourceLabel = new Label();
        sourceLayout = new TableLayoutPanel();
        sourceBox = new ComboBox();
        removeButton = new Button();
        refreshLibraryButton = new Button();
        searchLabel = new Label();
        searchBox = new TextBox();
        gameLabel = new Label();
        gamesBox = new ComboBox();
        idLabel = new Label();
        appIdBox = new TextBox();
        nameLabel = new Label();
        nameLayout = new TableLayoutPanel();
        gameNameBox = new TextBox();
        saveButton = new Button();
        limitLabel = new Label();
        limitLayout = new FlowLayoutPanel();
        autoStopBox = new CheckBox();
        minutesBox = new NumericUpDown();
        minutesLabel = new Label();
        actionLayout = new FlowLayoutPanel();
        multiGameBox = new CheckBox();
        invisibleModeBox = new CheckBox();
        optionsLayout = new FlowLayoutPanel();
        multiLayout = new TableLayoutPanel();
        selectionActions = new FlowLayoutPanel();
        selectedGamesView = new ListView();
        selectedNameColumn = new ColumnHeader();
        selectedIdColumn = new ColumnHeader();
        selectedStatusColumn = new ColumnHeader();
        selectedTimeColumn = new ColumnHeader();
        selectedTargetColumn = new ColumnHeader();
        selectedRemainingColumn = new ColumnHeader();
        addSelectedButton = new Button();
        removeSelectedButton = new Button();
        clearSelectedButton = new Button();
        stopSelectedButton = new Button();
        targetButton = new Button();
        startButton = new Button();
        stopButton = new Button();
        resetProgressButton = new Button();
        weeklySummaryButton = new Button();
        cardModeButton = new Button();
        sessionLabel = new Label();
        activeGameLabel = new Label();
        elapsedCaption = new Label();
        elapsedLabel = new Label();
        timeCaption = new Label();
        statusCaption = new Label();
        statusLabel = new TextBox();
        minimizeToTrayBox = new CheckBox();
        uiTimer = new System.Windows.Forms.Timer(components);
        trayMenu = new ContextMenuStrip(components);
        showMenuItem = new ToolStripMenuItem();
        stopMenuItem = new ToolStripMenuItem();
        exitMenuItem = new ToolStripMenuItem();
        trayIcon = new NotifyIcon(components);
        mainLayout.SuspendLayout();
        sourceLayout.SuspendLayout();
        nameLayout.SuspendLayout();
        limitLayout.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)minutesBox).BeginInit();
        actionLayout.SuspendLayout();
        optionsLayout.SuspendLayout();
        multiLayout.SuspendLayout();
        selectionActions.SuspendLayout();
        trayMenu.SuspendLayout();
        SuspendLayout();
        // 
        // mainLayout
        // 
        mainLayout.AutoSize = true;
        mainLayout.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        mainLayout.ColumnCount = 2;
        mainLayout.ColumnStyles.Add(new ColumnStyle());
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        mainLayout.Controls.Add(subtitleLabel, 0, 0);
        mainLayout.Controls.Add(sourceLabel, 0, 1);
        mainLayout.Controls.Add(sourceLayout, 1, 1);
        mainLayout.Controls.Add(searchLabel, 0, 2);
        mainLayout.Controls.Add(searchBox, 1, 2);
        mainLayout.Controls.Add(gameLabel, 0, 3);
        mainLayout.Controls.Add(gamesBox, 1, 3);
        mainLayout.Controls.Add(idLabel, 0, 4);
        mainLayout.Controls.Add(appIdBox, 1, 4);
        mainLayout.Controls.Add(nameLabel, 0, 5);
        mainLayout.Controls.Add(nameLayout, 1, 5);
        mainLayout.Controls.Add(limitLabel, 0, 6);
        mainLayout.Controls.Add(limitLayout, 1, 6);
        mainLayout.Controls.Add(optionsLayout, 0, 7);
        mainLayout.Controls.Add(multiLayout, 0, 8);
        mainLayout.Controls.Add(actionLayout, 1, 9);
        mainLayout.Controls.Add(sessionLabel, 0, 10);
        mainLayout.Controls.Add(activeGameLabel, 1, 10);
        mainLayout.Controls.Add(elapsedCaption, 0, 11);
        mainLayout.Controls.Add(elapsedLabel, 1, 11);
        mainLayout.Controls.Add(timeCaption, 0, 12);
        mainLayout.Controls.Add(statusCaption, 0, 13);
        mainLayout.Controls.Add(statusLabel, 1, 13);
        mainLayout.Controls.Add(minimizeToTrayBox, 0, 14);
        mainLayout.Dock = DockStyle.Top;
        mainLayout.Location = new Point(0, 0);
        mainLayout.Margin = new Padding(3, 4, 3, 4);
        mainLayout.Name = "mainLayout";
        mainLayout.Padding = new Padding(14, 12, 14, 12);
        mainLayout.RowCount = 15;
        mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        mainLayout.RowStyles.Add(new RowStyle());
        mainLayout.RowStyles.Add(new RowStyle());
        mainLayout.RowStyles.Add(new RowStyle());
        mainLayout.RowStyles.Add(new RowStyle());
        mainLayout.RowStyles.Add(new RowStyle());
        mainLayout.RowStyles.Add(new RowStyle());
        mainLayout.RowStyles.Add(new RowStyle());
        mainLayout.RowStyles.Add(new RowStyle());
        mainLayout.RowStyles.Add(new RowStyle());
        mainLayout.RowStyles.Add(new RowStyle());
        mainLayout.RowStyles.Add(new RowStyle());
        mainLayout.RowStyles.Add(new RowStyle());
        mainLayout.RowStyles.Add(new RowStyle());
        mainLayout.Size = new Size(777, 623);
        mainLayout.TabIndex = 1;
        // 
        // subtitleLabel
        // 
        subtitleLabel.AutoSize = true;
        mainLayout.SetColumnSpan(subtitleLabel, 2);
        subtitleLabel.Dock = DockStyle.Fill;
        subtitleLabel.Location = new Point(17, 20);
        subtitleLabel.Margin = new Padding(3, 4, 3, 16);
        subtitleLabel.Name = "subtitleLabel";
        subtitleLabel.Size = new Size(743, 20);
        subtitleLabel.TabIndex = 0;
        subtitleLabel.Text = UiText.Get("Steam hesabı bekleniyor.");
        // 
        // sourceLabel
        // 
        sourceLabel.Anchor = AnchorStyles.Left;
        sourceLabel.AutoSize = true;
        sourceLabel.Location = new Point(17, 68);
        sourceLabel.Name = "sourceLabel";
        sourceLabel.Size = new Size(87, 20);
        sourceLabel.TabIndex = 1;
        sourceLabel.Text = UiText.Get("Oyun listesi:");
        // 
        // sourceLayout
        // 
        sourceLayout.AutoSize = true;
        sourceLayout.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        sourceLayout.ColumnCount = 3;
        sourceLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        sourceLayout.ColumnStyles.Add(new ColumnStyle());
        sourceLayout.ColumnStyles.Add(new ColumnStyle());
        sourceLayout.Controls.Add(sourceBox, 0, 0);
        sourceLayout.Controls.Add(removeButton, 1, 0);
        sourceLayout.Controls.Add(refreshLibraryButton, 2, 0);
        sourceLayout.Dock = DockStyle.Fill;
        sourceLayout.Location = new Point(107, 56);
        sourceLayout.Margin = new Padding(0);
        sourceLayout.Name = "sourceLayout";
        sourceLayout.RowCount = 1;
        sourceLayout.RowStyles.Add(new RowStyle());
        sourceLayout.Size = new Size(656, 45);
        sourceLayout.TabIndex = 0;
        // 
        // sourceBox
        // 
        sourceBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        sourceBox.DropDownStyle = ComboBoxStyle.DropDownList;
        sourceBox.Items.AddRange(new object[] { UiText.Get("Steam kütüphanesi"), UiText.Get("Elle kaydedilenler") });
        sourceBox.Location = new Point(3, 8);
        sourceBox.Margin = new Padding(3, 4, 3, 4);
        sourceBox.Name = "sourceBox";
        sourceBox.Size = new Size(387, 28);
        sourceBox.TabIndex = 0;
        // 
        // removeButton
        // 
        removeButton.AutoSize = true;
        removeButton.Enabled = false;
        removeButton.Location = new Point(396, 4);
        removeButton.Margin = new Padding(3, 4, 3, 4);
        removeButton.MinimumSize = new Size(63, 37);
        removeButton.Name = "removeButton";
        removeButton.Size = new Size(86, 37);
        removeButton.TabIndex = 1;
        removeButton.Text = UiText.Get("Sil");
        removeButton.UseVisualStyleBackColor = true;
        // 
        // refreshLibraryButton
        // 
        refreshLibraryButton.AutoSize = true;
        refreshLibraryButton.Location = new Point(488, 4);
        refreshLibraryButton.Margin = new Padding(3, 4, 3, 4);
        refreshLibraryButton.MinimumSize = new Size(149, 37);
        refreshLibraryButton.Name = "refreshLibraryButton";
        refreshLibraryButton.Size = new Size(165, 37);
        refreshLibraryButton.TabIndex = 2;
        refreshLibraryButton.Text = UiText.Get("Kütüphaneyi yenile");
        refreshLibraryButton.UseVisualStyleBackColor = true;
        // 
        // searchLabel
        // 
        searchLabel.Anchor = AnchorStyles.Left;
        searchLabel.AutoSize = true;
        searchLabel.Location = new Point(17, 108);
        searchLabel.Name = "searchLabel";
        searchLabel.Size = new Size(35, 20);
        searchLabel.TabIndex = 2;
        searchLabel.Text = UiText.Get("Ara:");
        // 
        // searchBox
        // 
        searchBox.Dock = DockStyle.Fill;
        searchBox.Location = new Point(110, 105);
        searchBox.Margin = new Padding(3, 4, 3, 4);
        searchBox.Name = "searchBox";
        searchBox.PlaceholderText = UiText.Get("Oyun adı veya App ID");
        searchBox.Size = new Size(650, 27);
        searchBox.TabIndex = 1;
        // 
        // gameLabel
        // 
        gameLabel.Anchor = AnchorStyles.Left;
        gameLabel.AutoSize = true;
        gameLabel.Location = new Point(17, 144);
        gameLabel.Name = "gameLabel";
        gameLabel.Size = new Size(46, 20);
        gameLabel.TabIndex = 3;
        gameLabel.Text = UiText.Get("Oyun:");
        // 
        // gamesBox
        // 
        gamesBox.Dock = DockStyle.Fill;
        gamesBox.DropDownStyle = ComboBoxStyle.DropDownList;
        gamesBox.DropDownWidth = 600;
        gamesBox.IntegralHeight = false;
        gamesBox.Location = new Point(110, 140);
        gamesBox.Margin = new Padding(3, 4, 3, 4);
        gamesBox.MaxDropDownItems = 16;
        gamesBox.Name = "gamesBox";
        gamesBox.Size = new Size(650, 28);
        gamesBox.TabIndex = 2;
        // 
        // idLabel
        // 
        idLabel.Anchor = AnchorStyles.Left;
        idLabel.AutoSize = true;
        idLabel.Location = new Point(17, 179);
        idLabel.Name = "idLabel";
        idLabel.Size = new Size(59, 20);
        idLabel.TabIndex = 4;
        idLabel.Text = "App ID:";
        // 
        // appIdBox
        // 
        appIdBox.Anchor = AnchorStyles.Left;
        appIdBox.Location = new Point(110, 176);
        appIdBox.Margin = new Padding(3, 4, 3, 4);
        appIdBox.MaxLength = 10;
        appIdBox.Name = "appIdBox";
        appIdBox.PlaceholderText = UiText.Get("Örn. 730");
        appIdBox.Size = new Size(87, 27);
        appIdBox.TabIndex = 3;
        // 
        // nameLabel
        // 
        nameLabel.Anchor = AnchorStyles.Left;
        nameLabel.AutoSize = true;
        nameLabel.Location = new Point(17, 219);
        nameLabel.Name = "nameLabel";
        nameLabel.Size = new Size(71, 20);
        nameLabel.TabIndex = 5;
        nameLabel.Text = UiText.Get("Oyun adı:");
        // 
        // nameLayout
        // 
        nameLayout.AutoSize = true;
        nameLayout.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        nameLayout.ColumnCount = 2;
        nameLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        nameLayout.ColumnStyles.Add(new ColumnStyle());
        nameLayout.Controls.Add(gameNameBox, 0, 0);
        nameLayout.Controls.Add(saveButton, 1, 0);
        nameLayout.Dock = DockStyle.Fill;
        nameLayout.Location = new Point(107, 207);
        nameLayout.Margin = new Padding(0);
        nameLayout.Name = "nameLayout";
        nameLayout.RowCount = 1;
        nameLayout.RowStyles.Add(new RowStyle());
        nameLayout.Size = new Size(656, 45);
        nameLayout.TabIndex = 4;
        // 
        // gameNameBox
        // 
        gameNameBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        gameNameBox.Location = new Point(3, 9);
        gameNameBox.Margin = new Padding(3, 4, 3, 4);
        gameNameBox.MaxLength = 80;
        gameNameBox.Name = "gameNameBox";
        gameNameBox.Size = new Size(558, 27);
        gameNameBox.TabIndex = 0;
        // 
        // saveButton
        // 
        saveButton.AutoSize = true;
        saveButton.Location = new Point(567, 4);
        saveButton.Margin = new Padding(3, 4, 3, 4);
        saveButton.MinimumSize = new Size(86, 37);
        saveButton.Name = "saveButton";
        saveButton.Size = new Size(86, 37);
        saveButton.TabIndex = 1;
        saveButton.Text = UiText.Get("Kaydet");
        saveButton.UseVisualStyleBackColor = true;
        // 
        // limitLabel
        // 
        limitLabel.Anchor = AnchorStyles.Left;
        limitLabel.AutoSize = true;
        limitLabel.Location = new Point(17, 260);
        limitLabel.Name = "limitLabel";
        limitLabel.Size = new Size(76, 20);
        limitLabel.TabIndex = 6;
        limitLabel.Text = UiText.Get("Süre sınırı:");
        // 
        // limitLayout
        // 
        limitLayout.AutoSize = true;
        limitLayout.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        limitLayout.Controls.Add(autoStopBox);
        limitLayout.Controls.Add(minutesBox);
        limitLayout.Controls.Add(minutesLabel);
        limitLayout.Dock = DockStyle.Fill;
        limitLayout.Location = new Point(107, 252);
        limitLayout.Margin = new Padding(0);
        limitLayout.Name = "limitLayout";
        limitLayout.Size = new Size(656, 36);
        limitLayout.TabIndex = 5;
        // 
        // autoStopBox
        // 
        autoStopBox.AutoSize = true;
        autoStopBox.Location = new Point(3, 8);
        autoStopBox.Margin = new Padding(3, 8, 7, 4);
        autoStopBox.Name = "autoStopBox";
        autoStopBox.Size = new Size(141, 24);
        autoStopBox.TabIndex = 0;
        autoStopBox.Text = UiText.Get("Otomatik durdur");
        autoStopBox.UseVisualStyleBackColor = true;
        // 
        // minutesBox
        // 
        minutesBox.Enabled = false;
        minutesBox.Location = new Point(154, 4);
        minutesBox.Margin = new Padding(3, 4, 3, 4);
        minutesBox.Maximum = new decimal(new int[] { 10080, 0, 0, 0 });
        minutesBox.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
        minutesBox.Name = "minutesBox";
        minutesBox.Size = new Size(91, 27);
        minutesBox.TabIndex = 1;
        minutesBox.Value = new decimal(new int[] { 60, 0, 0, 0 });
        // 
        // minutesLabel
        // 
        minutesLabel.AutoSize = true;
        minutesLabel.Location = new Point(251, 9);
        minutesLabel.Margin = new Padding(3, 9, 3, 4);
        minutesLabel.Name = "minutesLabel";
        minutesLabel.Size = new Size(92, 20);
        minutesLabel.TabIndex = 2;
        minutesLabel.Text = UiText.Get("dakika sonra");
        //
        // multiGameBox
        //
        multiGameBox.AutoSize = true;
        multiGameBox.Margin = new Padding(3, 4, 18, 4);
        multiGameBox.Name = "multiGameBox";
        multiGameBox.TabIndex = 6;
        multiGameBox.Text = UiText.Get("Birden fazla oyun");
        multiGameBox.UseVisualStyleBackColor = true;
        //
        // optionsLayout / invisibleModeBox
        //
        optionsLayout.AutoSize = true;
        optionsLayout.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        optionsLayout.Dock = DockStyle.Fill;
        optionsLayout.WrapContents = true;
        optionsLayout.Margin = new Padding(0, 4, 0, 0);
        optionsLayout.Name = "optionsLayout";
        optionsLayout.TabIndex = 6;
        mainLayout.SetColumnSpan(optionsLayout, 2);
        optionsLayout.Controls.Add(multiGameBox);
        optionsLayout.Controls.Add(invisibleModeBox);
        optionsLayout.Controls.Add(cardModeButton);
        cardModeButton.AutoSize = true;
        cardModeButton.Name = "cardModeButton";
        cardModeButton.Text = UiText.Get("Kart modu…");
        cardModeButton.Margin = new Padding(12, 0, 3, 0);
        cardModeButton.MinimumSize = new Size(100, 28);
        cardModeButton.UseVisualStyleBackColor = true;
        invisibleModeBox.AutoSize = true;
        invisibleModeBox.Margin = new Padding(3, 4, 3, 4);
        invisibleModeBox.Name = "invisibleModeBox";
        invisibleModeBox.TabIndex = 7;
        invisibleModeBox.Text = UiText.Get("Saat kasarken görünmez moda geç");
        invisibleModeBox.UseVisualStyleBackColor = true;
        //
        // multiLayout
        //
        multiLayout.AutoSize = true;
        multiLayout.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        mainLayout.SetColumnSpan(multiLayout, 2);
        multiLayout.ColumnCount = 1;
        multiLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        multiLayout.RowCount = 2;
        multiLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        multiLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 138F));
        multiLayout.Controls.Add(selectionActions, 0, 0);
        multiLayout.Controls.Add(selectedGamesView, 0, 1);
        multiLayout.Dock = DockStyle.Fill;
        multiLayout.Margin = new Padding(0);
        multiLayout.Name = "multiLayout";
        multiLayout.TabIndex = 7;
        multiLayout.Visible = false;
        //
        // selectionActions
        //
        selectionActions.AutoSize = true;
        selectionActions.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        selectionActions.Controls.Add(addSelectedButton);
        selectionActions.Controls.Add(removeSelectedButton);
        selectionActions.Controls.Add(clearSelectedButton);
        selectionActions.Controls.Add(stopSelectedButton);
        selectionActions.Controls.Add(targetButton);
        selectionActions.Dock = DockStyle.Fill;
        selectionActions.Margin = new Padding(0);
        selectionActions.Name = "selectionActions";
        selectionActions.TabIndex = 0;
        selectionActions.WrapContents = false;
        //
        // Selection buttons
        //
        addSelectedButton.AutoSize = true;
        addSelectedButton.MinimumSize = new Size(75, 32);
        addSelectedButton.Name = "addSelectedButton";
        addSelectedButton.TabIndex = 0;
        addSelectedButton.Text = UiText.Get("Ekle");
        addSelectedButton.UseVisualStyleBackColor = true;
        removeSelectedButton.AutoSize = true;
        removeSelectedButton.MinimumSize = new Size(115, 32);
        removeSelectedButton.Name = "removeSelectedButton";
        removeSelectedButton.TabIndex = 1;
        removeSelectedButton.Text = UiText.Get("Listeden çıkar");
        removeSelectedButton.UseVisualStyleBackColor = true;
        clearSelectedButton.AutoSize = true;
        clearSelectedButton.MinimumSize = new Size(125, 32);
        clearSelectedButton.Name = "clearSelectedButton";
        clearSelectedButton.TabIndex = 2;
        clearSelectedButton.Text = UiText.Get("Listeyi temizle");
        clearSelectedButton.UseVisualStyleBackColor = true;
        stopSelectedButton.AutoSize = true;
        stopSelectedButton.MinimumSize = new Size(130, 32);
        stopSelectedButton.Name = "stopSelectedButton";
        stopSelectedButton.TabIndex = 3;
        stopSelectedButton.Text = UiText.Get("Seçileni durdur");
        stopSelectedButton.UseVisualStyleBackColor = true;
        targetButton.AutoSize = true;
        targetButton.MinimumSize = new Size(100, 32);
        targetButton.Name = "targetButton";
        targetButton.TabIndex = 4;
        targetButton.Text = UiText.Get("Süre hedefi");
        targetButton.UseVisualStyleBackColor = true;
        //
        // selectedGamesView
        //
        selectedNameColumn.Text = UiText.Get("Oyun");
        selectedNameColumn.Width = 290;
        selectedIdColumn.Text = "App ID";
        selectedIdColumn.Width = 80;
        selectedStatusColumn.Text = UiText.Get("Durum");
        selectedStatusColumn.Width = 170;
        selectedTimeColumn.Text = UiText.Get("Süre");
        selectedTimeColumn.Width = 100;
        selectedTargetColumn.Text = UiText.Get("Hedef");
        selectedTargetColumn.Width = 110;
        selectedRemainingColumn.Text = UiText.Get("Kalan süre");
        selectedRemainingColumn.Width = 100;
        selectedGamesView.Columns.AddRange(new ColumnHeader[] { selectedNameColumn, selectedIdColumn, selectedStatusColumn, selectedTimeColumn, selectedTargetColumn, selectedRemainingColumn });
        selectedGamesView.Dock = DockStyle.Top;
        selectedGamesView.FullRowSelect = true;
        selectedGamesView.HideSelection = false;
        selectedGamesView.Name = "selectedGamesView";
        selectedGamesView.ShowItemToolTips = true;
        selectedGamesView.Size = new Size(740, 132);
        selectedGamesView.TabIndex = 1;
        selectedGamesView.View = View.Details;
        // 
        // actionLayout
        // 
        actionLayout.AutoSize = true;
        actionLayout.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        actionLayout.Controls.Add(startButton);
        actionLayout.Controls.Add(stopButton);
        actionLayout.Controls.Add(resetProgressButton);
        actionLayout.Controls.Add(weeklySummaryButton);
        weeklySummaryButton.AutoSize = true;
        weeklySummaryButton.MinimumSize = new Size(120, 37);
        weeklySummaryButton.Margin = new Padding(3, 4, 3, 4);
        weeklySummaryButton.Name = "weeklySummaryButton";
        weeklySummaryButton.Text = UiText.Get("Haftalık özet");
        weeklySummaryButton.TabIndex = 3;
        weeklySummaryButton.UseVisualStyleBackColor = true;
        resetProgressButton.AutoSize = true;
        resetProgressButton.MinimumSize = new Size(120, 37);
        resetProgressButton.Margin = new Padding(3, 4, 3, 4);
        resetProgressButton.Name = "resetProgressButton";
        resetProgressButton.Text = UiText.Get("Hedefi sıfırla");
        resetProgressButton.TabIndex = 2;
        resetProgressButton.UseVisualStyleBackColor = true;
        actionLayout.Dock = DockStyle.Fill;
        actionLayout.Location = new Point(107, 296);
        actionLayout.Margin = new Padding(0, 8, 0, 8);
        actionLayout.Name = "actionLayout";
        actionLayout.Size = new Size(656, 48);
        actionLayout.TabIndex = 8;
        // 
        // startButton
        // 
        startButton.AutoSize = true;
        startButton.Location = new Point(3, 4);
        startButton.Margin = new Padding(3, 4, 3, 4);
        startButton.MinimumSize = new Size(103, 37);
        startButton.Name = "startButton";
        startButton.Size = new Size(103, 40);
        startButton.TabIndex = 0;
        startButton.Text = UiText.Get("Başlat");
        startButton.UseVisualStyleBackColor = true;
        // 
        // stopButton
        // 
        stopButton.AutoSize = true;
        stopButton.Enabled = false;
        stopButton.Location = new Point(112, 4);
        stopButton.Margin = new Padding(3, 4, 3, 4);
        stopButton.MinimumSize = new Size(103, 37);
        stopButton.Name = "stopButton";
        stopButton.Size = new Size(103, 40);
        stopButton.TabIndex = 1;
        stopButton.Text = UiText.Get("Durdur");
        stopButton.UseVisualStyleBackColor = true;
        // 
        // sessionLabel
        // 
        sessionLabel.Anchor = AnchorStyles.Left;
        sessionLabel.AutoSize = true;
        sessionLabel.Location = new Point(17, 359);
        sessionLabel.Name = "sessionLabel";
        sessionLabel.Size = new Size(62, 20);
        sessionLabel.TabIndex = 7;
        sessionLabel.Text = UiText.Get("Oturum:");
        // 
        // activeGameLabel
        // 
        activeGameLabel.AutoSize = true;
        activeGameLabel.Dock = DockStyle.Fill;
        activeGameLabel.Location = new Point(110, 359);
        activeGameLabel.Margin = new Padding(3, 7, 3, 7);
        activeGameLabel.Name = "activeGameLabel";
        activeGameLabel.Size = new Size(650, 20);
        activeGameLabel.TabIndex = 8;
        activeGameLabel.Text = UiText.Get("Henüz oturum başlatılmadı");
        // 
        // elapsedCaption
        // 
        elapsedCaption.Anchor = AnchorStyles.Left;
        elapsedCaption.AutoSize = true;
        elapsedCaption.Location = new Point(17, 393);
        elapsedCaption.Name = "elapsedCaption";
        elapsedCaption.Size = new Size(84, 20);
        elapsedCaption.TabIndex = 9;
        elapsedCaption.Text = UiText.Get("Geçen süre:");
        // 
        // elapsedLabel
        // 
        elapsedLabel.AutoSize = true;
        elapsedLabel.Location = new Point(110, 393);
        elapsedLabel.Margin = new Padding(3, 7, 3, 7);
        elapsedLabel.Name = "elapsedLabel";
        elapsedLabel.Size = new Size(63, 20);
        elapsedLabel.TabIndex = 10;
        elapsedLabel.Text = "00:00:00";
        // 
        // timeCaption
        // 
        timeCaption.AutoSize = true;
        mainLayout.SetColumnSpan(timeCaption, 2);
        timeCaption.Dock = DockStyle.Fill;
        timeCaption.Location = new Point(17, 428);
        timeCaption.Margin = new Padding(3, 8, 3, 12);
        timeCaption.Name = "timeCaption";
        timeCaption.Size = new Size(743, 20);
        timeCaption.TabIndex = 11;
        timeCaption.Text = UiText.Get("Sayaç yalnızca bu oturumun süresini gösterir; Steam'deki toplam süre değildir.");
        // 
        // statusCaption
        // 
        statusCaption.AutoSize = true;
        statusCaption.Location = new Point(17, 468);
        statusCaption.Margin = new Padding(3, 8, 3, 4);
        statusCaption.Name = "statusCaption";
        statusCaption.Size = new Size(57, 20);
        statusCaption.TabIndex = 12;
        statusCaption.Text = UiText.Get("Durum:");
        // 
        // statusLabel
        // 
        statusLabel.Dock = DockStyle.Fill;
        statusLabel.Location = new Point(110, 464);
        statusLabel.Margin = new Padding(3, 4, 3, 4);
        statusLabel.MinimumSize = new Size(114, 80);
        statusLabel.Multiline = true;
        statusLabel.Name = "statusLabel";
        statusLabel.ReadOnly = true;
        statusLabel.ScrollBars = ScrollBars.Vertical;
        statusLabel.Size = new Size(650, 80);
        statusLabel.TabIndex = 9;
        statusLabel.Text = UiText.Get("Hazır. Steam açıkken bir oyun seçip Başlat'a bas.");
        // 
        // minimizeToTrayBox
        // 
        minimizeToTrayBox.AutoSize = true;
        mainLayout.SetColumnSpan(minimizeToTrayBox, 2);
        minimizeToTrayBox.Location = new Point(17, 579);
        minimizeToTrayBox.Margin = new Padding(3, 16, 3, 4);
        minimizeToTrayBox.Name = "minimizeToTrayBox";
        minimizeToTrayBox.Size = new Size(355, 24);
        minimizeToTrayBox.TabIndex = 8;
        minimizeToTrayBox.Text = UiText.Get("Küçültünce sistem tepsisinde çalışmaya devam et");
        minimizeToTrayBox.UseVisualStyleBackColor = true;
        minimizeToTrayBox.Visible = false;
        // 
        // uiTimer
        // 
        uiTimer.Interval = 1000;
        // 
        // trayMenu
        // 
        trayMenu.ImageScalingSize = new Size(20, 20);
        trayMenu.Items.AddRange(new ToolStripItem[] { showMenuItem, stopMenuItem, exitMenuItem });
        trayMenu.Name = "trayMenu";
        trayMenu.Size = new Size(125, 76);
        // 
        // showMenuItem
        // 
        showMenuItem.Name = "showMenuItem";
        showMenuItem.Size = new Size(124, 24);
        showMenuItem.Text = UiText.Get("Göster");
        // 
        // stopMenuItem
        // 
        stopMenuItem.Enabled = false;
        stopMenuItem.Name = "stopMenuItem";
        stopMenuItem.Size = new Size(124, 24);
        stopMenuItem.Text = UiText.Get("Durdur");
        // 
        // exitMenuItem
        // 
        exitMenuItem.Name = "exitMenuItem";
        exitMenuItem.Size = new Size(124, 24);
        exitMenuItem.Text = UiText.Get("Çıkış");
        // 
        // trayIcon
        // 
        trayIcon.ContextMenuStrip = trayMenu;
        trayIcon.Text = "SteamHourTracker";
        // 
        // Form1
        // 
        AutoScaleDimensions = new SizeF(8F, 20F);
        AutoScaleMode = AutoScaleMode.Font;
        AutoScroll = true;
        ClientSize = new Size(777, 880);
        Controls.Add(mainLayout);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        Margin = new Padding(3, 4, 3, 4);
        MaximizeBox = false;
        MinimizeBox = false;
        MinimumSize = new Size(637, 518);
        Name = "Form1";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "SteamHourTracker";
        mainLayout.ResumeLayout(false);
        mainLayout.PerformLayout();
        sourceLayout.ResumeLayout(false);
        sourceLayout.PerformLayout();
        nameLayout.ResumeLayout(false);
        nameLayout.PerformLayout();
        limitLayout.ResumeLayout(false);
        limitLayout.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)minutesBox).EndInit();
        actionLayout.ResumeLayout(false);
        actionLayout.PerformLayout();
        optionsLayout.ResumeLayout(false);
        optionsLayout.PerformLayout();
        multiLayout.ResumeLayout(false);
        multiLayout.PerformLayout();
        selectionActions.ResumeLayout(false);
        selectionActions.PerformLayout();
        trayMenu.ResumeLayout(false);
        ResumeLayout(false);
        PerformLayout();
    }
}
