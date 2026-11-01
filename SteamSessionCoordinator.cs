namespace SteamHourTracker;

// One presence change per batch, independent of the number of game workers.
internal sealed class SteamSessionCoordinator(SteamSessionGroup sessions, ISteamPresence presence)
{
    public bool IsBusy { get; private set; }
    public bool IsPreparing { get; private set; }
    public bool IsRestoring { get; private set; }
    public string? Error { get; private set; }
    public Task Completion { get; private set; } = Task.CompletedTask;
    public event Action? Changed;
    private bool stopRequested;

    // Keep one presence lifetime across all stages, including pauses with no workers.
    internal Task RunSequenceAsync(bool invisible, ulong expectedAccount, CancellationToken token,
        Func<Func<ulong, CancellationToken, Task<ulong>>, ulong, Task> sequence)
    {
        if (IsBusy) throw new InvalidOperationException(UiText.Get("Önce mevcut oturumun bitmesini bekle."));
        IsBusy = IsPreparing = true;
        stopRequested = false;
        Error = null;
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        return Completion = RunAsync([], 0, invisible, started, null, expectedAccount, sequence, token);
    }

    public Task StartAsync(SavedGame[] games, int minutes, bool invisible, IReadOnlyDictionary<uint, GameTargetProgress>? progress = null, ulong expectedAccount = 0)
    {
        if (IsBusy) throw new InvalidOperationException(UiText.Get("Önce mevcut oturumun bitmesini bekle."));
        IsBusy = IsPreparing = true;
        stopRequested = false;
        Error = null;
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Completion = RunAsync(games, minutes, invisible, started, progress, expectedAccount);
        return started.Task;
    }

    private async Task RunAsync(SavedGame[] games, int minutes, bool invisible, TaskCompletionSource started, IReadOnlyDictionary<uint, GameTargetProgress>? progress, ulong expectedAccount,
        Func<Func<ulong, CancellationToken, Task<ulong>>, ulong, Task>? sequence = null, CancellationToken token = default)
    {
        ulong restoreAccount = 0;
        using var presenceGate = new SemaphoreSlim(1, 1);
        long lastInvisibleRetry = 0;
        try
        {
            Changed?.Invoke();
            if (invisible)
            {
                var snapshot = await presence.ReadAsync(token);
                if (stopRequested) return;
                if (expectedAccount != 0 && snapshot.SteamId != expectedAccount) throw new ReconnectAccountChangedException();
                expectedAccount = snapshot.SteamId;
                // Also restore after a partially successful/timeout invisibility request.
                restoreAccount = snapshot.SteamId;
                await presence.SetAsync(SteamPresenceState.Invisible, restoreAccount, token);
                if (stopRequested) return;
            }
            IsPreparing = false;
            async Task<ulong> BeforeRetry(ulong originalAccount, CancellationToken token)
            {
                await presenceGate.WaitAsync(token);
                try
                {
                    token.ThrowIfCancellationRequested();
                    var snapshot = await presence.ReadAsync(token);
                    if (snapshot.SteamId == 0 || (originalAccount != 0 && snapshot.SteamId != originalAccount))
                        throw new ReconnectAccountChangedException();
                    if (invisible && (lastInvisibleRetry == 0 || Environment.TickCount64 - lastInvisibleRetry > 3000))
                    {
                        await presence.SetAsync(SteamPresenceState.Invisible, snapshot.SteamId, token);
                        lastInvisibleRetry = Environment.TickCount64;
                    }
                    token.ThrowIfCancellationRequested();
                    return snapshot.SteamId;
                }
                finally { presenceGate.Release(); }
            }
            if (sequence != null)
            {
                token.ThrowIfCancellationRequested();
                started.TrySetResult();
                await sequence(BeforeRetry, expectedAccount);
            }
            else
            {
                await sessions.StartAsync(games, minutes, progress, BeforeRetry, expectedAccount);
                started.TrySetResult();
                await sessions.Completion;
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) { Error = ex.Message; }
        finally
        {
            if (sessions.IsBusy) await sessions.StopAllAsync();
            IsPreparing = false;
            if (restoreAccount != 0)
            {
                IsRestoring = true;
                Changed?.Invoke();
                try { await presence.SetAsync(SteamPresenceState.Online, restoreAccount); }
                catch (Exception ex)
                {
                    Error = (Error == null ? "" : Error + Environment.NewLine) +
                        UiText.Get("Çevrimiçine dönülemedi: ") + ex.Message;
                }
            }
            IsRestoring = IsBusy = false;
            started.TrySetResult();
            Changed?.Invoke();
        }
    }

    public async Task StopAllAsync()
    {
        stopRequested = true;
        await sessions.StopAllAsync();
        await Completion;
    }

    public async Task StopGamesAsync(IEnumerable<uint> appIds)
    {
        await Task.WhenAll(appIds.Select(sessions.StopAsync));
        if (!sessions.IsBusy) await Completion;
    }
}
