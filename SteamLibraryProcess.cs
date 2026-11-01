using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace SteamHourTracker;

internal record LibraryReply(SteamLibrarySnapshot? Library, string? Error);

internal static class SteamLibraryProcess
{
    private const string Prefix = "SHT-LIBRARY:";

    public static int RunWorker()
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        try
        {
            Write(new LibraryReply(SteamLibraryReader.Read(), null));
            return 0;
        }
        catch (Exception ex)
        {
            Write(new LibraryReply(null, ex.Message));
            return 1;
        }
    }

    private static void Write(LibraryReply reply)
    {
        Console.WriteLine(Prefix + JsonSerializer.Serialize(reply));
        Console.Out.Flush();
    }

    public static async Task<SteamLibrarySnapshot> ReadAsync(CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "SteamHourTracker.exe"))
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            WorkingDirectory = AppContext.BaseDirectory
        };
        start.ArgumentList.Add("--library");
        start.Environment[UiText.WorkerLanguageVariable] = UiText.Language;
        start.Environment.Remove("SteamAppId");
        start.Environment.Remove("SteamGameId");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(45));
        using var process = Process.Start(start) ?? throw new InvalidOperationException(UiText.Get("Kütüphane okuyucusu açılamadı."));
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> errors = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            string text = await output;
            await errors;
            string? replyLine = text.Split('\n').LastOrDefault(line => line.StartsWith(Prefix, StringComparison.Ordinal));
            if (replyLine == null)
                throw new InvalidOperationException(UiText.Get("Steam kütüphanesi okunamadı. Steam'i yeniden başlatıp Yenile'ye bas."));
            var reply = JsonSerializer.Deserialize<LibraryReply>(replyLine[Prefix.Length..]);
            if (!string.IsNullOrEmpty(reply?.Error)) throw new InvalidOperationException(reply.Error);
            if (process.ExitCode != 0 || reply?.Library == null)
                throw new InvalidOperationException(UiText.Get("Steam kütüphanesi eksik yanıt döndürdü."));
            return reply.Library;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException(UiText.Get("Kütüphane okuma süresi doldu. Steam kütüphaneni açıp tekrar Yenile'ye bas."));
        }
        finally
        {
            if (!process.HasExited)
            {
                try { process.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) { }
                await process.WaitForExitAsync();
            }
            await Task.WhenAll(output, errors);
        }
    }
}
