using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace SteamHourTracker;

internal sealed class SteamSession
{
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Process? _process;
    private Task? _stopTask;
    private string? _endMessage;
    public Task<string> Completion { get; private set; } = Task.FromResult(UiText.Get("Hazır."));
    public bool Failed { get; private set; }
    public bool TargetReached { get; private set; }
    public string? ErrorCode { get; private set; }
    public ulong SteamId { get; private set; }
    private bool receivedReady;
    public event Action? Ending;

    public async Task StartAsync(uint appId, int minutes, double? remainingSeconds = null, ulong expectedAccount = 0)
    {
        // Use the apphost even when Visual Studio starts the UI through dotnet.
        string executable = Path.Combine(AppContext.BaseDirectory, "SteamHourTracker.exe");
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            // Our worker reports every error through the stdout protocol. Do not keep an
            // unused stderr read pending for every game.
            RedirectStandardError = false,
            StandardOutputEncoding = Encoding.UTF8,
            WorkingDirectory = AppContext.BaseDirectory
        };
        start.ArgumentList.Add("--worker");
        start.Environment[UiText.WorkerLanguageVariable] = UiText.Language;
        start.ArgumentList.Add(appId.ToString(CultureInfo.InvariantCulture));
        start.ArgumentList.Add(minutes.ToString(CultureInfo.InvariantCulture));
        if (remainingSeconds != null || expectedAccount != 0) start.ArgumentList.Add(remainingSeconds?.ToString("R", CultureInfo.InvariantCulture) ?? "-");
        if (expectedAccount != 0) start.ArgumentList.Add(expectedAccount.ToString(CultureInfo.InvariantCulture));
        _process = Process.Start(start) ?? throw new InvalidOperationException(UiText.Get("Yardımcı işlem başlatılamadı."));
        Completion = MonitorAsync(_process);
        try { await _ready.Task.WaitAsync(TimeSpan.FromSeconds(20)); }
        catch (TimeoutException)
        {
            ErrorCode = "startup_timeout";
            throw new InvalidOperationException(UiText.Get("Steam 20 saniye içinde yanıt vermedi. Steam'i kontrol edip yeniden dene."));
        }
    }

    private async Task<string> MonitorAsync(Process process)
    {
        var messages = Channel.CreateBounded<string>(new BoundedChannelOptions(8)
        { SingleReader = true, SingleWriter = true, FullMode = BoundedChannelFullMode.Wait });
        // .NET 10's redirected Windows pipes block a pool thread even with ReadLineAsync.
        // One sleeping dedicated reader avoids starving startup/timeouts for large batches.
        Task reader = Task.Factory.StartNew(() => ReadMessages(process.StandardOutput, messages.Writer),
            CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        try
        {
            await foreach (string line in messages.Reader.ReadAllAsync())
            {
                WorkerMessage? message;
                try { message = JsonSerializer.Deserialize<WorkerMessage>(line[SteamWorker.Prefix.Length..]); }
                catch (JsonException) { continue; }
                if (message?.Kind == "ready")
                {
                    receivedReady = true;
                    SteamId = message.SteamId;
                    _ready.TrySetResult();
                }
                else if (message?.Kind is "error" or "stopped")
                {
                    _endMessage = message.Message;
                    ErrorCode = message.ErrorCode;
                    Ending?.Invoke();
                    TargetReached = message.Kind == "stopped" && message.TargetReached;
                    if (message.Kind == "error")
                    {
                        Failed = true;
                        _ready.TrySetException(new InvalidOperationException(message.Message));
                    }
                }
            }
            await process.WaitForExitAsync();
            await reader;
            Failed |= process.ExitCode != 0;
            if (receivedReady && _endMessage == null)
            {
                Failed = true;
                ErrorCode = "worker_exit";
            }
            _endMessage ??= process.ExitCode == 0 ? UiText.Get("Oturum sona erdi.") : UiText.Get("Steam yardımcı işlemi beklenmedik şekilde kapandı.");
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            Failed = true;
            if (receivedReady) ErrorCode = "worker_exit";
            _endMessage ??= UiText.Get("Steam yardımcı işlemiyle bağlantı kesildi.");
        }
        Ending?.Invoke();
        _ready.TrySetException(new InvalidOperationException(_endMessage ?? UiText.Get("Bağlantı kurulamadı.")));
        return _endMessage ?? UiText.Get("Oturum sona erdi.");
    }

    private static void ReadMessages(StreamReader reader, ChannelWriter<string> writer)
    {
        Exception? failure = null;
        try
        {
            while (reader.ReadLine() is { } line)
                if (line.StartsWith(SteamWorker.Prefix, StringComparison.Ordinal))
                    writer.WriteAsync(line).AsTask().GetAwaiter().GetResult();
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException) { failure = ex; }
        finally { writer.TryComplete(failure); }
    }

    public Task StopAsync() => _stopTask ??= StopCoreAsync();

    private async Task StopCoreAsync()
    {
        if (_process is not { } process) return;
        try
        {
            process.StandardInput.Close();
            try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(4)); }
            catch (TimeoutException)
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
            await Completion;
        }
        finally { process.Dispose(); }
    }
}
