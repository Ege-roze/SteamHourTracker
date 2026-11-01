namespace SteamHourTracker;

internal sealed record CardFarmOptions(int TogetherMinutes = 5, int SingleSeconds = 5, int PauseSeconds = 10, int Rounds = 0, bool Automatic = true, int HoursBeforeDrops = 2)
{
    internal CardFarmOptions Normalize() => new(Math.Clamp(TogetherMinutes, 1, 180), Math.Clamp(SingleSeconds, 5, 120),
        Math.Clamp(PauseSeconds, 5, 120), Math.Clamp(Rounds, 0, 100), Automatic, Math.Clamp(HoursBeforeDrops, 0, 10));
}

internal enum CardFarmPhase { Idle, Preparing, Scanning, Warming, Together, Pause, Individual, Stopping, Finished }
internal sealed record CardFarmTiming(TimeSpan Together, TimeSpan Individual, TimeSpan Pause);

// Written for this application. Uses the existing worker lifecycle; never inspects credentials or claims a drop occurred.
internal sealed partial class CardFarm(SteamSessionGroup sessions, SteamSessionCoordinator coordinator)
{
    public bool IsBusy { get; private set; }
    public CardFarmPhase Phase { get; private set; }
    public int Round { get; private set; }
    public string Message { get; private set; } = "";
    public string? Error { get; private set; }
    public Task Completion { get; private set; } = Task.CompletedTask;
    public event Action? Changed;
    public event Action? BeforeStage;
    public event Action? Completed;
    private CancellationTokenSource? cancellation;
    private SavedGame[] queue = [];
    private readonly HashSet<uint> excluded = [];
    private readonly HashSet<uint> permanentExclusions = [];
    private readonly List<string> failures = [];
    private TimeSpan stageDuration;
    private IReadOnlyDictionary<uint, TimeSpan>? stageTargets;
    private bool stageTimePending;
    private readonly System.Diagnostics.Stopwatch pauseClock = new();
    internal Func<CardFarmOptions, CardFarmTiming> Timing { get; set; } = o => new(
        TimeSpan.FromMinutes(o.TogetherMinutes), TimeSpan.FromSeconds(o.SingleSeconds), TimeSpan.FromSeconds(o.PauseSeconds));
    internal TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(1);
    internal Func<int, TimeSpan> RetryDelay { get; set; } = n => TimeSpan.FromSeconds(Math.Min(60, 10 * Math.Pow(2, Math.Min(n - 1, 3))));
    public bool IsExcluded(uint id) => excluded.Contains(id) || permanentExclusions.Contains(id);
    public bool IsPermanentlyExcluded(uint id) => permanentExclusions.Contains(id);
    public void SetPermanentExclusions(IEnumerable<uint> ids)
    {
        if (IsBusy) throw new InvalidOperationException(UiText.Get("Hariç tutulanları değiştirmek için önce kart modunu durdur."));
        permanentExclusions.Clear();
        permanentExclusions.UnionWith(ids.Where(id => id > 0));
    }
    internal void ClearStatus()
    {
        if (IsBusy) return;
        Phase = CardFarmPhase.Idle; Message = ""; Error = null;
        IsAutomatic = false; queue = []; cardGames.Clear(); initialHours.Clear(); cardSeconds.Clear(); stageTimePending = false;
    }
    public TimeSpan Remaining => Phase == CardFarmPhase.Pause ? MaxZero(stageDuration - pauseClock.Elapsed) :
        Phase is CardFarmPhase.Together or CardFarmPhase.Individual or CardFarmPhase.Warming
            ? TimeSpan.FromSeconds(sessions.Runs.Where(r => r.IsActive && !IsExcluded(r.Game.AppId))
                .Select(r => Math.Max(0, StageTarget(r.Game.AppId).TotalSeconds - r.Elapsed.Elapsed.TotalSeconds)).DefaultIfEmpty(0).Max()) : TimeSpan.Zero;
    private TimeSpan StageTarget(uint appId) => stageTargets?.GetValueOrDefault(appId, stageDuration) ?? stageDuration;
    private static TimeSpan MaxZero(TimeSpan value) => value < TimeSpan.Zero ? TimeSpan.Zero : value;

    public Task StartAsync(SavedGame[] games, CardFarmOptions options, bool invisible, ulong account)
    {
        if (IsBusy || coordinator.IsBusy || sessions.IsBusy) throw new InvalidOperationException(UiText.Get("Önce açık oturumu durdur."));
        var selected = games.DistinctBy(g => g.AppId).ToArray();
        if (selected.Length is < 1 or > 30 || selected.Any(g => g.AppId == 0))
            throw new ArgumentException(UiText.Get("Kart modu için 1–30 oyun seç."));
        if (selected.All(g => IsPermanentlyExcluded(g.AppId)))
            throw new ArgumentException(UiText.Get("Seçilen oyunların tamamı kart modundan hariç tutulmuş. Hariç tutulanlar listesini düzenle."));
        if (options != options.Normalize()) throw new ArgumentException(UiText.Get("Kart modu süreleri geçersiz."));
        IsAutomatic = false; inventory = null;
        Initialize(selected);
        return Completion = RunAsync(options, invisible, account, cancellation!.Token);
    }

    private void Initialize(SavedGame[] selected)
    {
        queue = selected.Select(g => g with { TargetMinutes = null }).ToArray();
        excluded.Clear(); failures.Clear(); Error = null; Round = 0;
        cardGames.Clear(); initialHours.Clear(); cardSeconds.Clear(); lastServed.Clear(); serviceNumber = 0; stageTargets = null; stageTimePending = false;
        cancellation = new();
        IsBusy = true;
        SetPhase(CardFarmPhase.Preparing, UiText.Get("Kart modu hazırlanıyor…"));
    }

    private SavedGame[] Available() => queue.Where(g => !IsExcluded(g.AppId)).ToArray();
    private void SetPhase(CardFarmPhase phase, string message)
    {
        Phase = phase; Message = message; Changed?.Invoke();
    }

    private async Task RunAsync(CardFarmOptions options, bool invisible, ulong account, CancellationToken token)
    {
        try
        {
            var timing = Timing(options);
            await coordinator.RunSequenceAsync(invisible, account, token, async (checkAccount, currentAccount) =>
            {
                if (IsAutomatic)
                {
                    await RunAutomaticAsync(options, timing, checkAccount, currentAccount, token);
                    return;
                }
                while (options.Rounds == 0 || Round < options.Rounds)
                {
                    token.ThrowIfCancellationRequested();
                    if (Available().Length == 0) break;
                    Round++;
                    currentAccount = await StageAsync(Available(), timing.Together, CardFarmPhase.Together, checkAccount, currentAccount, token);
                    if (Available().Length == 0) break;
                    stageDuration = timing.Pause;
                    pauseClock.Restart();
                    SetPhase(CardFarmPhase.Pause, UiText.Format($"Kart modu • Tur {Round}: oyunlar kapalı, ara bekleme."));
                    await Task.Delay(timing.Pause, token);
                    pauseClock.Stop();
                    foreach (var game in Available())
                    {
                        token.ThrowIfCancellationRequested();
                        if (!IsExcluded(game.AppId))
                            currentAccount = await StageAsync([game], timing.Individual, CardFarmPhase.Individual, checkAccount, currentAccount, token);
                    }
                }
            });
            Error = coordinator.Error;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) { Error = ex.Message; }
        finally
        {
            await coordinator.StopAllAsync();
            pauseClock.Stop();
            Error ??= coordinator.Error;
            IsBusy = false;
            string result = token.IsCancellationRequested ? UiText.Get("Kart modu durduruldu.") : Error != null ? UiText.Get("Çalışma tamamlanamadı.") : UiText.Get("Kart modu tamamlandı.");
            if (IsAutomatic && !token.IsCancellationRequested && Error == null)
                result = cardGames.Values.Any(g => g.DropsRemaining > 0) ? UiText.Get("Otomatik kart modunda işlenebilir oyun kalmadı.") : UiText.Get("Tarama tamamlandı: kütüphanede Steam'in kart hakkı gösterdiği oyun kalmadı.");
            if (failures.Count > 0) result += Environment.NewLine + UiText.Get("Atlanan oyunlar: ") + string.Join("; ", failures);
            SetPhase(CardFarmPhase.Finished, Error == null ? result : UiText.Get("Kart modu durdu: ") + Error + Environment.NewLine + result);
            cancellation?.Dispose(); cancellation = null;
            if (!token.IsCancellationRequested && Error == null) Completed?.Invoke();
        }
    }

    private async Task<ulong> StageAsync(SavedGame[] games, TimeSpan duration, CardFarmPhase phase,
        Func<ulong, CancellationToken, Task<ulong>> checkAccount, ulong account, CancellationToken token, IReadOnlyDictionary<uint, TimeSpan>? targets = null)
    {
        int attempt = 0;
        while (true)
        {
            token.ThrowIfCancellationRequested();
            try { account = await checkAccount(account, token); break; }
            catch (ReconnectAccountChangedException) { throw; }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                SetPhase(CardFarmPhase.Preparing, UiText.Get("Kart modu • Steam bağlantısı bekleniyor: ") + ex.Message);
                await Task.Delay(RetryDelay(++attempt), token);
            }
        }
        token.ThrowIfCancellationRequested();
        games = games.Where(g => !IsExcluded(g.AppId)).ToArray();
        if (games.Length == 0) return account;
        stageDuration = duration;
        stageTargets = targets;
        SetPhase(phase, phase == CardFarmPhase.Warming ? UiText.Format($"Kart modu • {games.Length} oyunun hazırlık saati tamamlanıyor (eşik ayarı).")
            : phase == CardFarmPhase.Together ? UiText.Format($"Kart modu • Tur {Round}: {games.Length} oyun birlikte çalışıyor.")
            : UiText.Format($"Kart modu • Tur {Round}: tekli aç/kapa — {games[0].Name}"));
        BeforeStage?.Invoke();
        // No normal target or previously completed target is consumed by this independent cycle.
        try
        {
        var startup = sessions.StartAsync(games, 0, beforeRetry: checkAccount, expectedAccount: account);
        stageTimePending = IsAutomatic;
        await startup;
        while (true)
        {
            token.ThrowIfCancellationRequested();
            if (sessions.Runs.Any(r => r.AccountChanged)) throw new ReconnectAccountChangedException();
            foreach (var run in sessions.Runs)
            {
                if (run.Status == GameSessionStatus.Failed && excluded.Add(run.Game.AppId))
                    failures.Add(run.Game.Name + ": " + run.Message);
                if (run.IsActive && (IsExcluded(run.Game.AppId) || run.Elapsed.Elapsed >= StageTarget(run.Game.AppId)))
                    await sessions.StopAsync(run.Game.AppId);
            }
            if (!sessions.IsBusy) break;
            // Long stages need only one check/sec; keep short stages and near deadlines precise.
            double seconds = sessions.Runs.Where(r => r.Status == GameSessionStatus.Running)
                .Select(r => Math.Max(.001, (StageTarget(r.Game.AppId) - r.Elapsed.Elapsed).TotalSeconds))
                .DefaultIfEmpty(PollInterval.TotalSeconds).Min();
            await Task.Delay(TimeSpan.FromSeconds(Math.Min(PollInterval.TotalSeconds, seconds)), token);
        }
        // Every previous worker has exited before any new stage can start.
        await sessions.Completion;
        return account;
        }
        finally
        {
            await sessions.StopAllAsync();
            if (IsAutomatic)
                foreach (var run in sessions.Runs)
                    cardSeconds[run.Game.AppId] = cardSeconds.GetValueOrDefault(run.Game.AppId) + run.Elapsed.Elapsed.TotalSeconds;
            stageTimePending = false;
        }
    }

    public async Task StopAsync()
    {
        if (!IsBusy) return;
        cancellation?.Cancel();
        SetPhase(CardFarmPhase.Stopping, UiText.Get("Kart modu durduruluyor…"));
        await coordinator.StopAllAsync();
        await Completion;
    }

    public async Task SkipAsync(IEnumerable<uint> ids)
    {
        foreach (uint id in ids) excluded.Add(id);
        if (Available().Length == 0) { await StopAsync(); return; }
        await Task.WhenAll(sessions.Runs.Where(r => IsExcluded(r.Game.AppId)).Select(r => sessions.StopAsync(r.Game.AppId)));
        Changed?.Invoke();
    }
}
