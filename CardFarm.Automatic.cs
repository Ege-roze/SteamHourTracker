namespace SteamHourTracker;

internal sealed record CardQueueRow(uint AppId, string Name, int? Remaining, double? SteamHours, double? Hours, TimeSpan? PreparationRemaining, string Status);

internal sealed partial class CardFarm
{
    public bool IsAutomatic { get; private set; }
    private ICardInventory? inventory;
    private Dictionary<uint, SavedGame> owned = [];
    private readonly Dictionary<uint, CardGame> cardGames = [];
    private readonly Dictionary<uint, double> initialHours = [], cardSeconds = [];
    private readonly Dictionary<uint, long> lastServed = [];
    private long serviceNumber;
    internal Func<int, TimeSpan> PreparationThreshold { get; set; } = hours => TimeSpan.FromHours(hours);
    private TimeSpan preparationGoal = TimeSpan.FromHours(2);
    internal void UpdatePreparationGoal(int hours)
    {
        if (!IsBusy) preparationGoal = PreparationThreshold(Math.Clamp(hours, 0, 10));
    }
    public int GamesRemaining => cardGames.Values.Count(g => g.DropsRemaining > 0 && !IsExcluded(g.AppId));
    public int CardsRemaining => cardGames.Values.Where(g => !IsExcluded(g.AppId)).Sum(g => g.DropsRemaining);
    public IReadOnlyList<CardQueueRow> QueueRows
    {
        get
        {
            string RunStatus(uint id, bool needsHours)
            {
                if (IsPermanentlyExcluded(id)) return UiText.Get("Kalıcı hariç");
                if (excluded.Contains(id)) return UiText.Get("Atlandı");
                if (!IsBusy) return Phase == CardFarmPhase.Finished ? UiText.Get("Durduruldu") : UiText.Get("Sırada");
                var run = sessions.Runs.FirstOrDefault(r => r.Game.AppId == id);
                if (run?.Status == GameSessionStatus.Reconnecting) return UiText.Get("Bağlantı bekleniyor");
                if (run?.Status == GameSessionStatus.Starting) return UiText.Get("Bağlanıyor");
                if (run?.Status == GameSessionStatus.Stopping) return UiText.Get("Durduruluyor");
                if (run?.Status == GameSessionStatus.Running)
                    return Phase == CardFarmPhase.Warming ? UiText.Get("Saat tamamlanıyor") : Phase == CardFarmPhase.Individual ? UiText.Get("Tekli aç/kapa") : UiText.Get("Kart düşmesi bekleniyor");
                if (Phase == CardFarmPhase.Scanning) return UiText.Get("Kartlar kontrol ediliyor");
                return needsHours ? UiText.Get("Hazırlık bekliyor") : Phase == CardFarmPhase.Pause ? UiText.Get("Ara veriliyor") : UiText.Get("Kart sırası bekliyor");
            }
            if (!IsAutomatic)
                return queue.Select(g => new CardQueueRow(g.AppId, g.Name, null, null, null, null, RunStatus(g.AppId, false))).ToArray();
            return cardGames.Values.OrderByDescending(g => g.DropsRemaining > 0 && !IsExcluded(g.AppId)).ThenBy(g => g.Name).Select(g =>
            {
                double hours = EffectiveHours(g);
                TimeSpan? remaining = g.DropsRemaining > 0 && !IsExcluded(g.AppId) ? MaxZero(preparationGoal - TimeSpan.FromHours(hours)) : null;
                string status = IsPermanentlyExcluded(g.AppId) ? UiText.Get("Kalıcı hariç") : excluded.Contains(g.AppId) ? UiText.Get("Atlandı") : !g.DropCountShown ? UiText.Get("Kart hakkı gösterilmiyor") :
                    g.DropsRemaining == 0 ? UiText.Get("Kart hakkı bitti") : RunStatus(g.AppId, remaining > TimeSpan.Zero);
                return new CardQueueRow(g.AppId, g.Name, g.DropCountShown ? g.DropsRemaining : null, g.HoursPlayed, hours, remaining, status);
            }).ToArray();
        }
    }

    public Task StartAutomaticAsync(IEnumerable<SavedGame> libraryGames, ICardInventory source, CardFarmOptions options, bool invisible, ulong account)
    {
        if (IsBusy || coordinator.IsBusy || sessions.IsBusy) throw new InvalidOperationException(UiText.Get("Önce açık oturumu durdur."));
        if (account == 0) throw new ArgumentException(UiText.Get("Otomatik kart modu için Steam kütüphanesi okunmalı."));
        if (options != options.Normalize()) throw new ArgumentException(UiText.Get("Kart modu süreleri geçersiz."));
        UpdatePreparationGoal(options.HoursBeforeDrops);
        owned = libraryGames.Where(g => g.AppId > 0).DistinctBy(g => g.AppId).ToDictionary(g => g.AppId);
        inventory = source; IsAutomatic = true;
        Initialize([]);
        return Completion = RunAsync(options, invisible, account, cancellation!.Token);
    }

    private double EffectiveHours(CardGame game)
    {
        double current = stageTimePending ? sessions.Runs.FirstOrDefault(r => r.Game.AppId == game.AppId)?.Elapsed.Elapsed.TotalSeconds ?? 0 : 0;
        return Math.Max(game.HoursPlayed, initialHours.GetValueOrDefault(game.AppId, game.HoursPlayed) +
            (cardSeconds.GetValueOrDefault(game.AppId) + current) / 3600);
    }

    private async Task ScanAsync(ulong account, CancellationToken token)
    {
        int attempt = 0;
        while (true)
        {
            token.ThrowIfCancellationRequested();
            SetPhase(CardFarmPhase.Scanning, UiText.Get("Kart modu • Steam'deki bütün rozet sayfaları taranıyor…"));
            try
            {
                var snapshot = await inventory!.ReadAsync(token);
                if (snapshot.Account != account) throw new ReconnectAccountChangedException();
                var scanned = snapshot.Games.Where(g => owned.ContainsKey(g.AppId)).ToArray();
                if (scanned.Any(g => g.AppId == 0 || g.DropsRemaining < 0 || !double.IsFinite(g.HoursPlayed) || g.HoursPlayed < 0) ||
                    scanned.DistinctBy(g => g.AppId).Count() != scanned.Length) throw new CardPageException(UiText.Get("Kart listesi geçersiz; eksik bilgiyle devam edilmedi."));
                cardGames.Clear();
                foreach (var game in scanned)
                {
                    cardGames.Add(game.AppId, game);
                    initialHours.TryAdd(game.AppId, game.HoursPlayed);
                }
                queue = scanned.Where(g => g.DropsRemaining > 0).Select(g => new SavedGame(g.AppId, g.Name)).ToArray();
                Changed?.Invoke();
                return;
            }
            catch (HttpRequestException) { }
            catch (OperationCanceledException) when (!token.IsCancellationRequested) { }
            SetPhase(CardFarmPhase.Scanning, UiText.Get("Kart modu • Rozet taraması bağlantı bekliyor. Oyunlar kapalı; Durdur ile iptal edebilirsin."));
            await Task.Delay(RetryDelay(++attempt), token);
        }
    }

    private async Task RunAutomaticAsync(CardFarmOptions options, CardFarmTiming timing,
        Func<ulong, CancellationToken, Task<ulong>> checkAccount, ulong account, CancellationToken token)
    {
        await ScanAsync(account, token);
        double thresholdSeconds = preparationGoal.TotalSeconds;
        while (Available().Length > 0)
        {
            token.ThrowIfCancellationRequested(); Round++;
            var candidates = Available();
            var ready = candidates.Where(g => EffectiveHours(cardGames[g.AppId]) * 3600 >= thresholdSeconds)
                .OrderBy(g => lastServed.GetValueOrDefault(g.AppId)).ThenBy(g => g.AppId).Take(30).ToArray();
            if (ready.Length == 0)
            {
                var warming = candidates.OrderByDescending(g => EffectiveHours(cardGames[g.AppId])).ThenBy(g => g.AppId).Take(30).ToArray();
                var targets = warming.ToDictionary(g => g.AppId, g => TimeSpan.FromSeconds(Math.Min(timing.Together.TotalSeconds,
                    Math.Max(.001, thresholdSeconds - EffectiveHours(cardGames[g.AppId]) * 3600))));
                account = await StageAsync(warming, timing.Together, CardFarmPhase.Warming, checkAccount, account, token, targets);
                await ScanAsync(account, token);
                continue;
            }
            foreach (var game in ready) lastServed[game.AppId] = ++serviceNumber;
            account = await StageAsync(ready, timing.Together, CardFarmPhase.Together, checkAccount, account, token);
            await ScanAsync(account, token);
            var remaining = ready.Where(g => !IsExcluded(g.AppId) && cardGames.TryGetValue(g.AppId, out var card) && card.DropsRemaining > 0).ToArray();
            if (remaining.Length == 0) continue;
            stageTargets = null; stageDuration = timing.Pause; pauseClock.Restart();
            SetPhase(CardFarmPhase.Pause, UiText.Format($"Kart modu • {GamesRemaining} oyun / {CardsRemaining} kart kaldı. Tekli aç/kapa öncesi ara."));
            await Task.Delay(timing.Pause, token); pauseClock.Stop();
            foreach (var game in remaining)
            {
                token.ThrowIfCancellationRequested();
                if (!IsExcluded(game.AppId))
                    account = await StageAsync([game], timing.Individual, CardFarmPhase.Individual, checkAccount, account, token);
            }
            await ScanAsync(account, token);
        }
    }
}
