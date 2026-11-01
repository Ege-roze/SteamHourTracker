using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace SteamHourTracker;

internal record WorkerMessage(string Kind, string Message, bool TargetReached = false, string? ErrorCode = null, ulong SteamId = 0);
internal sealed class SteamWorkerException(string message, string code) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}

internal static class SteamWorker
{
    internal const string Prefix = "SHT:";
    // ~13 calls/sec retains Steam's >10 Hz recommendation without the former 20 wakeups/sec.
    internal const int CallbackIntervalMilliseconds = 75;

    public static int Run(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        bool initialized = false;
        string? sessionDirectory = null;
        try
        {
            if (args.Length is < 3 or > 5 || !AppIdInput.TryParse(args[1], out uint appId) ||
                !int.TryParse(args[2], NumberStyles.None, CultureInfo.InvariantCulture, out int minutes) ||
                minutes is < 0 or > 10080)
                throw new ArgumentException(UiText.Get("Geçerli bir App ID ve 0–10080 dakika arasında süre gerekiyor."));
            double durationSeconds = minutes * 60d;
            if (args.Length >= 4 && args[3] != "-" && (minutes == 0 ||
                !double.TryParse(args[3], NumberStyles.Float, CultureInfo.InvariantCulture, out durationSeconds) ||
                !double.IsFinite(durationSeconds) || durationSeconds <= 0 || durationSeconds > minutes * 60d))
                throw new ArgumentException(UiText.Get("Kalan süre sıfırdan büyük olmalı ve hedefi aşmamalı."));
            ulong expectedAccount = 0;
            if (args.Length == 5 && (!ulong.TryParse(args[4], NumberStyles.None, CultureInfo.InvariantCulture, out expectedAccount) || expectedAccount == 0))
                throw new ArgumentException(UiText.Get("Geçerli bir Steam hesap kimliği gerekiyor."));

            // EOF also stops the worker if the main window crashes or is killed.
            var stopRequest = Task.Run(() => Console.ReadLine());
            // One kernel event per short-lived worker; unlike Task.Wait this does not spin
            // on every callback interval. The OS releases it when this worker exits.
            var stopSignal = ((IAsyncResult)stopRequest).AsyncWaitHandle;
            SteamNative.Load();
            if (stopRequest.IsCompleted) return 0;
            if (!SteamNative.SteamAPI_IsSteamRunning())
                throw new SteamWorkerException(UiText.Get("Steam açık değil. Steam'i açıp hesabına giriş yap."), "steam_unavailable");

            // Each start gets a fresh process: Steam caches the App ID within a process.
            // Environment changes affect only this worker, never the machine or Steam.
            string id = appId.ToString(CultureInfo.InvariantCulture);
            Environment.SetEnvironmentVariable("SteamAppId", id);
            Environment.SetEnvironmentVariable("SteamGameId", id);
            sessionDirectory = Path.Combine(Path.GetTempPath(), "SteamHourTracker", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(sessionDirectory);
            File.WriteAllText(Path.Combine(sessionDirectory, "steam_appid.txt"), id, Encoding.ASCII);
            Directory.SetCurrentDirectory(sessionDirectory);

            byte[] error = new byte[1024];
            int result = SteamNative.SteamAPI_InitFlat(error);
            if (result != 0)
            {
                int end = Array.IndexOf(error, (byte)0);
                string detail = Encoding.UTF8.GetString(error, 0, end < 0 ? error.Length : end);
                throw new SteamWorkerException(UiText.Get("Steam bağlantısı kurulamadı. App ID'yi, oyun lisansını ve Steam oturumunu kontrol et. ") + detail, "init_failed");
            }
            initialized = true;
            IntPtr user = SteamNative.SteamAPI_SteamUser_v023();
            IntPtr utils = SteamNative.SteamAPI_SteamUtils_v010();
            if (user == IntPtr.Zero || utils == IntPtr.Zero)
                throw new InvalidOperationException(UiText.Get("Steam arayüzleri alınamadı. Steam istemcisini güncelle."));
            if (SteamNative.SteamAPI_ISteamUtils_GetAppID(utils) != appId)
                throw new InvalidOperationException(UiText.Get("Steam farklı bir App ID döndürdü; oturum durduruldu."));
            if (!SteamNative.SteamAPI_ISteamUser_BLoggedOn(user))
                throw new SteamWorkerException(UiText.Get("Steam çevrimiçi oturumu bulunamadı. Çevrimiçi olarak giriş yap."), "logged_out");
            ulong account = SteamNative.SteamAPI_ISteamUser_GetSteamID(user);
            if (account == 0 || (expectedAccount != 0 && account != expectedAccount))
                throw new SteamWorkerException(UiText.Get("Steam hesabı değişti. Kütüphaneyi yenileyip tekrar başlat."), "account_changed");

            if (stopRequest.IsCompleted) return 0;
            Send("ready", UiText.Get("Steam bağlantısı kuruldu."), steamId: account);
            var elapsed = Stopwatch.StartNew();
            long lastCheck = 0;
            while (!stopRequest.IsCompleted)
            {
                SteamNative.SteamAPI_RunCallbacks();
                if (elapsed.ElapsedMilliseconds - lastCheck >= 1000)
                {
                    lastCheck = elapsed.ElapsedMilliseconds;
                    if (!SteamNative.SteamAPI_IsSteamRunning() || !SteamNative.SteamAPI_ISteamUser_BLoggedOn(user))
                        throw new SteamWorkerException(UiText.Get("Steam bağlantısı kesildi."), "connection_lost");
                    if (SteamNative.SteamAPI_ISteamUser_GetSteamID(user) != account)
                        throw new SteamWorkerException(UiText.Get("Steam hesabı değişti. Kütüphaneyi yenileyip tekrar başlat."), "account_changed");
                }
                if (minutes > 0 && elapsed.Elapsed >= TimeSpan.FromSeconds(durationSeconds))
                {
                    Send("stopped", UiText.Get("Belirlenen süre doldu."), targetReached: true);
                    return 0;
                }
                int wait = CallbackIntervalMilliseconds;
                if (minutes > 0) wait = Math.Clamp((int)Math.Ceiling((durationSeconds - elapsed.Elapsed.TotalSeconds) * 1000), 1, wait);
                // EOF/Stop wakes immediately, even between callback checks.
                stopSignal.WaitOne(wait);
            }
            Send("stopped", UiText.Get("Oturum durduruldu."));
            return 0;
        }
        catch (Exception ex)
        {
            Send("error", ex is BadImageFormatException
                ? UiText.Get("64 bit Steam DLL dosyası gerekiyor. Projeyi x64 olarak derle.")
                : ex.Message, errorCode: (ex as SteamWorkerException)?.Code);
            return 1;
        }
        finally
        {
            if (initialized) SteamNative.SteamAPI_Shutdown();
            if (sessionDirectory != null)
            {
                Directory.SetCurrentDirectory(AppContext.BaseDirectory);
                try
                {
                    // Delete only our own file and then the empty session directory.
                    File.Delete(Path.Combine(sessionDirectory, "steam_appid.txt"));
                    Directory.Delete(sessionDirectory);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }

    private static void Send(string kind, string message, bool targetReached = false, string? errorCode = null, ulong steamId = 0)
    {
        try
        {
            Console.WriteLine(Prefix + JsonSerializer.Serialize(new WorkerMessage(kind, message, targetReached, errorCode, steamId)));
            Console.Out.Flush();
        }
        catch (IOException) { /* The parent may already have exited. */ }
    }
}

internal static class AppIdInput
{
    public static bool TryParse(string text, out uint appId) =>
        uint.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out appId) && appId > 0;
}
