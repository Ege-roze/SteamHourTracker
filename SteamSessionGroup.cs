using System.Diagnostics;

namespace SteamHourTracker;

internal enum GameSessionStatus { Starting, Running, Reconnecting, Stopping, Stopped, Failed }
internal sealed class ReconnectAccountChangedException() : InvalidOperationException(UiText.Get("Steam hesabı değişti. Kütüphaneyi yenileyip tekrar başlat."));

internal sealed class GameSessionRun(SavedGame game, int targetMinutes, double previousSeconds = 0)
{
    public double PreviousSeconds { get; } = previousSeconds;
    public bool TargetReached { get; internal set; }
    public double CompletedSeconds => TargetMinutes == 0 ? 0 : TargetReached ? TargetMinutes * 60d :
        Math.Min(TargetMinutes * 60d, PreviousSeconds + Elapsed.Elapsed.TotalSeconds);
    public SavedGame Game { get; } = game;
    public int TargetMinutes { get; } = targetMinutes;
    public TimeSpan? Remaining => TargetMinutes == 0 ? null :
        TimeSpan.FromSeconds(Math.Max(0, Math.Ceiling(TargetMinutes * 60 - CompletedSeconds)));
    public GameSessionStatus Status { get; internal set; } = GameSessionStatus.Starting;
    public string Message { get; internal set; } = UiText.Get("Bağlanıyor…");
    public Stopwatch Elapsed { get; } = new();
    internal Action<DateTimeOffset, DateTimeOffset, double>? RecordWorkedTime { get; set; }
    private DateTimeOffset lastSampleUtc;
    private double lastSampleSeconds;

    internal void StartClock()
    {
        if (Elapsed.IsRunning) return;
        lastSampleUtc = DateTimeOffset.UtcNow;
        lastSampleSeconds = Elapsed.Elapsed.TotalSeconds;
        Elapsed.Start();
    }

    internal void CaptureWorkedTime() { if (Elapsed.IsRunning) CaptureWorkedTime(DateTimeOffset.UtcNow); }

    private void CaptureWorkedTime(DateTimeOffset now)
    {
        double current = Elapsed.Elapsed.TotalSeconds;
        double delta = current - lastSampleSeconds;
        if (delta > 0) RecordWorkedTime?.Invoke(lastSampleUtc, now, delta);
        lastSampleSeconds = current;
        lastSampleUtc = now;
    }

    internal void StopClock()
    {
        if (!Elapsed.IsRunning) return;
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Elapsed.Stop();
        CaptureWorkedTime(now);
    }
    public bool IsActive => Status is GameSessionStatus.Starting or GameSessionStatus.Running or GameSessionStatus.Reconnecting or GameSessionStatus.Stopping;
    internal SteamSession Session { get; set; } = new();
    internal CancellationTokenSource RetryCancellation { get; } = new();
    internal ulong ExpectedAccount { get; set; }
    internal bool HasConnected { get; set; }
    internal bool AccountChanged { get; set; }
    public int RetryCount { get; internal set; }
    internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal Task Lifetime { get; set; } = Task.CompletedTask;
    internal Task? StopTask { get; set; }
    internal bool StopRequested { get; set; }
}

// Called on the UI thread; awaits retain its synchronization context.
// Each entry owns one worker and cleans it up independently of the other games.
internal sealed class SteamSessionGroup
{
    private readonly List<GameSessionRun> runs = [];
    public IReadOnlyList<GameSessionRun> Runs => runs;
    public bool IsBusy => runs.Any(r => r.IsActive);
    public Task Completion { get; private set; } = Task.CompletedTask;
    public event Action? Changed;
    public int Revision { get; private set; }
    internal Func<int, TimeSpan> RetryDelay { get; set; } = attempt => TimeSpan.FromSeconds(Math.Min(60, 10 * Math.Pow(2, Math.Min(3, attempt - 1))));

    public void ClearFinished()
    {
        if (IsBusy) throw new InvalidOperationException(UiText.Get("Önce oturumları durdur."));
        runs.Clear();
    }

    public Task StartAsync(IEnumerable<SavedGame> games, int minutes, IReadOnlyDictionary<uint, GameTargetProgress>? progress = null,
        Func<ulong, CancellationToken, Task<ulong>>? beforeRetry = null, ulong expectedAccount = 0)
    {
        if (IsBusy) throw new InvalidOperationException(UiText.Get("Önce açık oturumları durdur."));
        var selected = games.DistinctBy(g => g.AppId).ToArray();
        if (selected.Length == 0 || selected.Any(g => g.AppId == 0))
            throw new ArgumentException(UiText.Get("En az bir geçerli oyun seç."));
        if (minutes is < 0 or > 10080) throw new ArgumentOutOfRangeException(nameof(minutes));
        if (selected.Any(g => g.TargetMinutes is < 1 or > 10080))
            throw new ArgumentException(UiText.Get("Oyun süresi hedefi 1 dakika ile 168 saat arasında olmalı."), nameof(games));
        runs.Clear();
        Revision++;
        runs.AddRange(selected.Select(g => new GameSessionRun(g, g.TargetMinutes ?? minutes,
            GameTargetProgress.Read(progress, g, g.TargetMinutes ?? minutes))));
        foreach (var run in runs)
        {
            run.ExpectedAccount = expectedAccount;
            run.Lifetime = RunAsync(run, beforeRetry);
        }
        Completion = Task.WhenAll(runs.Select(r => r.Lifetime));
        Changed?.Invoke();
        return Task.WhenAll(runs.Select(r => r.Started.Task));
    }

    private async Task RunAsync(GameSessionRun run, Func<ulong, CancellationToken, Task<ulong>>? beforeRetry)
    {
        bool failed = false;
        int consecutiveFailures = 0;
        try
        {
            while (!run.StopRequested)
            {
                if (run.TargetMinutes > 0 && run.CompletedSeconds >= run.TargetMinutes * 60d)
                {
                    run.TargetReached = true;
                    run.Message = UiText.Get("Hedef tamamlandı.");
                    failed = false;
                    break;
                }
                bool retry = false;
                if (run.RetryCount > 0 && beforeRetry != null)
                {
                    try { run.ExpectedAccount = await beforeRetry(run.ExpectedAccount, run.RetryCancellation.Token); }
                    catch (ReconnectAccountChangedException) { throw; }
                    catch (OperationCanceledException) when (run.StopRequested) { break; }
                    catch (Exception ex) { run.Message = ex.Message; retry = failed = true; }
                }
                if (run.StopRequested) break;
                if (!retry)
                {
                    run.Session = new SteamSession();
                    run.Session.Ending += run.StopClock;
                    run.Status = GameSessionStatus.Starting;
                    Changed?.Invoke();
                    try
                    {
                        await run.Session.StartAsync(run.Game.AppId, run.TargetMinutes,
                            run.CompletedSeconds > 0 ? run.TargetMinutes * 60d - run.CompletedSeconds : null, run.ExpectedAccount);
                        if (run.StopRequested) break;
                        run.HasConnected = true;
                        if (run.Session.SteamId != 0) run.ExpectedAccount = run.Session.SteamId;
                        consecutiveFailures = 0;
                        run.Status = GameSessionStatus.Running;
                        run.Message = run.RetryCount == 0 ? UiText.Get("Çalışıyor") : UiText.Get("Bağlantı yeniden kuruldu. Kalan süreden devam ediliyor.");
                        run.StartClock();
                        run.Started.TrySetResult();
                        Changed?.Invoke();
                        string message = await run.Session.Completion;
                        if (!run.StopRequested)
                        {
                            run.Message = message;
                            failed = run.Session.Failed;
                            run.AccountChanged = run.Session.ErrorCode == "account_changed";
                            run.TargetReached = !failed && run.Session.TargetReached;
                        }
                    }
                    catch (Exception ex) when (!run.StopRequested)
                    {
                        failed = true;
                        run.AccountChanged = run.Session.ErrorCode == "account_changed";
                        run.Message = ex.Message;
                    }
                    finally
                    {
                        run.StopClock();
                        retry = failed && (run.Session.ErrorCode is "connection_lost" or "steam_unavailable" or "logged_out" or "worker_exit" ||
                            (run.HasConnected && run.Session.ErrorCode is "init_failed" or "startup_timeout"));
                        try { await run.Session.StopAsync(); }
                        catch (Exception ex) { failed = true; retry = false; run.Message = UiText.Get("Oturum kapatılamadı: ") + ex.Message; }
                    }
                }
                if (run.StopRequested || !retry || run.TargetReached) break;
                run.RetryCount++;
                TimeSpan delay = RetryDelay(++consecutiveFailures);
                run.Status = GameSessionStatus.Reconnecting;
                run.Message += UiText.Format($" {delay.TotalSeconds:0} saniye sonra yeniden denenecek (deneme {run.RetryCount}). Durdur ile iptal edebilirsin.");
                run.Started.TrySetResult();
                Changed?.Invoke();
                await Task.Delay(delay, run.RetryCancellation.Token);
            }
        }
        catch (OperationCanceledException) when (run.StopRequested) { }
        catch (Exception ex)
        {
            if (!run.StopRequested)
            {
                failed = true;
                run.AccountChanged = ex is ReconnectAccountChangedException;
                run.Message = ex.Message;
            }
        }
        finally
        {
            run.StopClock();
            try { await run.Session.StopAsync(); }
            catch (Exception ex)
            {
                failed = true;
                run.Message = UiText.Get("Oturum kapatılamadı: ") + ex.Message;
            }
            run.Status = failed && !run.StopRequested ? GameSessionStatus.Failed : GameSessionStatus.Stopped;
            if (run.StopRequested) run.Message = UiText.Get("Oturum durduruldu.");
            run.RetryCancellation.Dispose();
            run.Started.TrySetResult();
            Changed?.Invoke();
        }
    }

    public Task StopAsync(uint appId)
    {
        var run = runs.Find(r => r.Game.AppId == appId);
        if (run == null) return Task.CompletedTask;
        if (run.StopTask != null) return run.StopTask;
        if (!run.IsActive) return run.Lifetime;
        return run.StopTask = StopCoreAsync(run);
    }

    private async Task StopCoreAsync(GameSessionRun run)
    {
        run.StopRequested = true;
        run.Status = GameSessionStatus.Stopping;
        run.Message = UiText.Get("Oturum durduruldu.");
        run.StopClock();
        try { run.RetryCancellation.Cancel(); }
        catch (ObjectDisposedException) { }
        Changed?.Invoke();
        // RunAsync records shutdown errors and performs final state notification.
        try { await run.Session.StopAsync(); }
        catch { }
        await run.Lifetime;
    }

    public Task StopAllAsync() => Task.WhenAll(runs.Select(r => StopAsync(r.Game.AppId)).ToArray());
}
