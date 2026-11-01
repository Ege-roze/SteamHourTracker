using Microsoft.Win32;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace SteamHourTracker;

internal enum SteamPresenceState { Online = 1, Invisible = 7 }
internal record SteamPresenceSnapshot(ulong SteamId);
internal record PresenceReply(SteamPresenceSnapshot? Presence, string? Error);

internal interface ISteamPresence
{
    Task<SteamPresenceSnapshot> ReadAsync();
    Task SetAsync(SteamPresenceState state, ulong steamId);
    Task<SteamPresenceSnapshot> ReadAsync(CancellationToken token) => ReadAsync().WaitAsync(token);
    Task SetAsync(SteamPresenceState state, ulong steamId, CancellationToken token) => SetAsync(state, steamId).WaitAsync(token);
}

internal sealed class SteamPresenceProcess : ISteamPresence
{
    private const string Prefix = "SHT-PRESENCE:";

    public Task<SteamPresenceSnapshot> ReadAsync() => RequestAsync("read", 0);
    public Task<SteamPresenceSnapshot> ReadAsync(CancellationToken token) => RequestAsync("read", 0, token);
    public async Task SetAsync(SteamPresenceState state, ulong steamId) =>
        await RequestAsync(state == SteamPresenceState.Invisible ? "invisible" : "online", steamId);
    public async Task SetAsync(SteamPresenceState state, ulong steamId, CancellationToken token) =>
        await RequestAsync(state == SteamPresenceState.Invisible ? "invisible" : "online", steamId, token);

    private static async Task<SteamPresenceSnapshot> RequestAsync(string command, ulong steamId, CancellationToken token = default)
    {
        var start = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "SteamHourTracker.exe"))
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, WorkingDirectory = AppContext.BaseDirectory
        };
        start.ArgumentList.Add("--presence");
        start.Environment[UiText.WorkerLanguageVariable] = UiText.Language;
        start.ArgumentList.Add(command);
        start.ArgumentList.Add(steamId.ToString(CultureInfo.InvariantCulture));
        start.Environment.Remove("SteamAppId");
        start.Environment.Remove("SteamGameId");
        using var process = Process.Start(start) ?? throw new InvalidOperationException(UiText.Get("Steam durum yardımcısı açılamadı."));
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> errors = process.StandardError.ReadToEndAsync();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            string text = await output;
            await errors;
            string? line = text.Split('\n').LastOrDefault(l => l.StartsWith(Prefix, StringComparison.Ordinal));
            var reply = line == null ? null : JsonSerializer.Deserialize<PresenceReply>(line[Prefix.Length..]);
            if (!string.IsNullOrEmpty(reply?.Error)) throw new InvalidOperationException(reply.Error);
            if (process.ExitCode != 0 || reply?.Presence == null)
                throw new InvalidOperationException(UiText.Get("Steam durum bilgisi alınamadı."));
            return reply.Presence;
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            throw new InvalidOperationException(UiText.Get("Steam durum değiştirme süresi doldu. Arkadaşlar menüsündeki durumunu kontrol et."));
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

    public static int RunWorker(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        try
        {
            if (args.Length != 3 || args[1] is not ("read" or "invisible" or "online") ||
                !ulong.TryParse(args[2], NumberStyles.None, CultureInfo.InvariantCulture, out ulong account) ||
                (args[1] != "read" && account == 0))
                throw new InvalidOperationException(UiText.Get("Geçersiz Steam durum isteği."));
            Write(new PresenceReply(SteamPresenceClient.Execute(args[1], account), null));
            return 0;
        }
        catch (Exception ex)
        {
            Write(new PresenceReply(null, ex.Message));
            return 1;
        }
    }

    private static void Write(PresenceReply reply)
    {
        Console.WriteLine(Prefix + JsonSerializer.Serialize(reply));
        Console.Out.Flush();
    }
}

// Runs only in a short-lived helper, with no App ID and no SteamAPI_Init.
// The same Steam URLs are used by Steam's own Friends menu.
internal static class SteamPresenceClient
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr CreateInterface([MarshalAs(UnmanagedType.LPStr)] string version, IntPtr result);
    [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
    private delegate int CreatePipe(IntPtr self);
    [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
    private delegate int ConnectUser(IntPtr self, int pipe);
    [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
    private delegate void ReleaseUser(IntPtr self, int pipe, int user);
    [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
    [return: MarshalAs(UnmanagedType.I1)]
    private delegate bool ReleasePipe(IntPtr self, int pipe);
    [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
    private delegate IntPtr GetInterface(IntPtr self, int user, int pipe, [MarshalAs(UnmanagedType.LPStr)] string version);
    [DllImport("kernel32.dll", EntryPoint = "LoadLibraryExW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibraryEx(string path, IntPtr file, uint flags);

    private static T Method<T>(IntPtr instance, int index) where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(instance), index * IntPtr.Size));

    public static SteamPresenceSnapshot Execute(string command, ulong expectedAccount)
    {
        SteamNative.Load();
        if (!SteamNative.SteamAPI_IsSteamRunning()) throw new InvalidOperationException(UiText.Get("Steam açık değil."));
        using var settings = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
        string? root = settings?.GetValue("SteamPath") as string;
        if (string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root))
            throw new InvalidOperationException(UiText.Get("Steam kurulum klasörü bulunamadı."));
        root = Path.GetFullPath(root);
        Environment.SetEnvironmentVariable("SteamAppId", null);
        Environment.SetEnvironmentVariable("SteamGameId", null);
        IntPtr module = LoadLibraryEx(Path.Combine(root, "steamclient64.dll"), IntPtr.Zero, 0x00000100 | 0x00001000);
        if (module == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), UiText.Get("Steam durum bağlantısı açılamadı."));
        // Steam owns background threads; keep its module loaded until helper exit.
        var create = Marshal.GetDelegateForFunctionPointer<CreateInterface>(NativeLibrary.GetExport(module, "CreateInterface"));
        IntPtr client = create("SteamClient023", IntPtr.Zero);
        if (client == IntPtr.Zero) throw new InvalidOperationException(UiText.Get("SteamClient023 desteklenmiyor."));
        int pipe = 0, user = 0;
        try
        {
            pipe = Method<CreatePipe>(client, 0)(client);
            if (pipe == 0) throw new InvalidOperationException(UiText.Get("Steam bağlantısı açılamadı."));
            user = Method<ConnectUser>(client, 2)(client, pipe);
            if (user == 0) throw new InvalidOperationException(UiText.Get("Açık Steam hesabı bulunamadı."));
            IntPtr steamUser = Method<GetInterface>(client, 5)(client, user, pipe, "SteamUser023");
            if (steamUser == IntPtr.Zero)
                throw new InvalidOperationException(UiText.Get("Steam durum arayüzü kullanılamıyor."));
            SteamPresenceSnapshot Read()
            {
                if (!SteamNative.SteamAPI_ISteamUser_BLoggedOn(steamUser))
                    throw new InvalidOperationException(UiText.Get("Steam hesabına giriş yapılmamış."));
                ulong id = SteamNative.SteamAPI_ISteamUser_GetSteamID(steamUser);
                if (id == 0 || (expectedAccount != 0 && id != expectedAccount))
                    throw new InvalidOperationException(UiText.Get("Steam hesabı değişti; diğer hesabın durumu değiştirilmedi."));
                return new SteamPresenceSnapshot(id);
            }
            SteamPresenceSnapshot snapshot = Read();
            if (command == "read") return snapshot;
            var start = new ProcessStartInfo(Path.Combine(root, "steam.exe"))
            {
                UseShellExecute = false, CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden, WorkingDirectory = root
            };
            start.ArgumentList.Add("steam://friends/status/" + command);
            using var launcher = Process.Start(start) ?? throw new InvalidOperationException(UiText.Get("Steam durum komutu gönderilemedi."));
            if (!launcher.WaitForExit(10000))
                throw new InvalidOperationException(UiText.Get("Steam durum komutuna zamanında yanıt vermedi. Arkadaşlar menüsünü kontrol et."));
            if (launcher.ExitCode != 0)
                throw new InvalidOperationException(UiText.Get("Steam durum komutunu kabul etmedi. Arkadaşlar menüsünü kontrol et."));
            // This confirms delivery, not server visibility. GetPersonaState reports
            // Online even while the Friends UI is Invisible, so it cannot acknowledge
            // this command. Always send Online on stop, regardless of that API value.
            return Read();
        }
        finally
        {
            if (user != 0) Method<ReleaseUser>(client, 4)(client, pipe, user);
            if (pipe != 0) Method<ReleasePipe>(client, 1)(client, pipe);
        }
    }
}
