namespace SteamHourTracker;

public partial class Form1
{
    private readonly CardFarm cardFarm;
    private CardQueueDialog? cardWindow;
    private bool cardStarting;
    private bool cardWindowHiddenToTray;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal Func<ulong, ICardInventory?>? CardInventoryFactory { get; set; }

    private bool CanStartCardMode() => !coordinator.IsBusy && !sessions.IsBusy && !cardFarm.IsBusy && !cardStarting && !closing && !libraryLoading;

    private Task ConfigureCardModeAsync()
    {
        if (closing) return Task.CompletedTask;
        if (cardWindow is { IsDisposed: false })
        {
            cardWindow.Show(); cardWindow.RefreshRows(); cardWindow.Activate(); return Task.CompletedTask;
        }
        cardFarm.UpdatePreparationGoal(preferences.CardMode.HoursBeforeDrops);
        cardWindow = new CardQueueDialog(cardFarm, () => preferences.CardMode, CanStartCardMode, StartCardModeAsync, ConfigureCardOptions,
            EditCardExclusions, CanEditCardExclusions);
        cardWindow.FormClosed += (_, _) => { cardWindow = null; UpdateUiTimer(); };
        cardWindow.VisibleChanged += (_, _) => UpdateUiTimer();
        cardWindow.Show(this);
        cardWindow.RefreshRows();
        return Task.CompletedTask;
    }

    private void ConfigureCardOptions()
    {
        if (!CanStartCardMode()) return;
        int count = multiGameBox.Checked ? selectedGames.Count : AppIdInput.TryParse(appIdBox.Text, out _) ? 1 : 0;
        using var dialog = new CardFarmDialog(count, preferences.CardMode, startOnAccept: false);
        if (dialog.ShowDialog(cardWindow ?? (Form)this) != DialogResult.OK) return;
        preferences.CardMode = dialog.Options;
        cardFarm.UpdatePreparationGoal(dialog.Options.HoursBeforeDrops);
        FlushSettings();
    }

    private bool CanEditCardExclusions() => CanStartCardMode() && settingsAccount != null && settingsWritable;

    private void ApplyCardExclusions() => cardFarm.SetPermanentExclusions(settingsAccount == null
        ? [] : AccountSelection(settingsAccount).CardExcludedAppIds);

    private void EditCardExclusions()
    {
        if (!CanEditCardExclusions()) return;
        string account = settingsAccount!;
        var choices = (library?.Games.Select(g => new SavedGame(g.AppId, g.Name)) ?? [])
            .Concat(games).Concat(selectedGames).Concat(cardFarm.QueueRows.Select(r => new SavedGame(r.AppId, r.Name)));
        if (AppIdInput.TryParse(appIdBox.Text, out uint currentId))
            choices = choices.Append(new SavedGame(currentId, string.IsNullOrWhiteSpace(gameNameBox.Text) ? $"App ID {currentId}" : gameNameBox.Text));
        using var dialog = new CardExclusionsDialog(choices, AccountSelection(account).CardExcludedAppIds);
        if (dialog.ShowDialog(cardWindow ?? (Form)this) != DialogResult.OK || closing || settingsAccount != account) return;
        AccountSelection(account).CardExcludedAppIds = dialog.ExcludedAppIds;
        ApplyCardExclusions();
        FlushSettings();
        cardWindow?.RefreshRows();
    }

    private async Task StartCardModeAsync()
    {
        if (!CanStartCardMode()) return;
        ApplyCardExclusions();
        var options = preferences.CardMode;
        SavedGame[] targets = [];
        if (!options.Automatic)
        {
            if (multiGameBox.Checked) targets = selectedGames.ToArray();
            else { var game = ReadCurrentGame(); targets = game == null ? [] : [game]; }
        }
        cardStarting = true; SetBusy(false); cardWindow?.RefreshRows();
        elapsed.Reset();
        try
        {
            if (options.Automatic)
            {
                if (library == null) throw new InvalidOperationException(UiText.Get("Önce Steam'e giriş yapıp Kütüphaneyi yenile düğmesine bas."));
                ICardInventory? source;
                if (CardInventoryFactory != null) source = CardInventoryFactory(library.SteamId);
                else
                {
                    using var login = new SteamWebLoginDialog(library.SteamId, Path.GetDirectoryName(gamesPath)!);
                    source = login.ShowDialog(cardWindow ?? (Form)this) == DialogResult.OK ? login.Inventory : null;
                }
                if (source == null) return;
                using (source)
                {
                    if (closing || IsDisposed) return;
                    await cardFarm.StartAutomaticAsync(library.Games.Select(g => new SavedGame(g.AppId, g.Name)), source,
                        options, invisibleModeBox.Checked, library.SteamId);
                }
            }
            else await cardFarm.StartAsync(targets, options, invisibleModeBox.Checked, library?.SteamId ?? 0);
        }
        catch (Exception ex) { if (!IsDisposed) { SetStatus(ex.Message, true); cardWindow?.ShowNotice(ex.Message); } }
        finally
        {
            cardStarting = false;
            if (!IsDisposed) { FlushSettings(); UpdateSessionState(); }
        }
    }

    private void PrepareCardStage()
    {
        CaptureTargetProgress();
        progressAccount = settingsAccount;
        progressRevision = sessions.Revision + 1;
        recordedFinalRuns.Clear();
    }

    private void UpdateCardStatus()
    {
        if (!cardFarm.IsBusy) return;
        activeGameLabel.Text = cardFarm.Message;
        string details = cardFarm.Phase is CardFarmPhase.Together or CardFarmPhase.Individual or CardFarmPhase.Pause or CardFarmPhase.Warming
            ? UiText.Get(" • Kalan: ") + ClockText(TimeSpan.FromSeconds(Math.Ceiling(cardFarm.Remaining.TotalSeconds))) : "";
        int reconnecting = sessions.Runs.Count(r => r.Status == GameSessionStatus.Reconnecting);
        if (reconnecting > 0) details += UiText.Format($" • {reconnecting} oyun yeniden bağlanmayı bekliyor; süre duraklatıldı.");
        if (cardFarm.IsAutomatic) details += UiText.Format($" • {cardFarm.GamesRemaining} oyun / {cardFarm.CardsRemaining} kart");
        SetStatus(cardFarm.Message + details);
    }
}
