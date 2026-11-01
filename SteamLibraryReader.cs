using Microsoft.Win32;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace SteamHourTracker;

public sealed record LibraryGame(uint AppId, string Name, bool Installed);
public sealed record SteamLibrarySnapshot(ulong SteamId, string AccountName, List<LibraryGame> Games, int Candidates);

internal static class SteamLibraryReader
{
    // Versioned interfaces: do not guess newer vtable layouts after Steam updates.
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
    [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
    private delegate int GetAppData(IntPtr self, uint appId, [MarshalAs(UnmanagedType.LPStr)] string key, [Out] byte[] value, int size);

    [DllImport("kernel32.dll", EntryPoint = "LoadLibraryExW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibraryEx(string path, IntPtr file, uint flags);

    private static T Method<T>(IntPtr instance, int index) where T : Delegate
    {
        if (instance == IntPtr.Zero) throw new InvalidOperationException(UiText.Get("Steam arayüzü alınamadı."));
        IntPtr address = Marshal.ReadIntPtr(Marshal.ReadIntPtr(instance), index * IntPtr.Size);
        return Marshal.GetDelegateForFunctionPointer<T>(address);
    }

    public static SteamLibrarySnapshot Read()
    {
        SteamNative.Load();
        if (!SteamNative.SteamAPI_IsSteamRunning())
            throw new InvalidOperationException(UiText.Get("Steam açık değil. Steam'i açıp hesabına giriş yap, ardından Yenile'ye bas."));

        using var settings = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
        string? root = settings?.GetValue("SteamPath") as string;
        if (string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root))
            throw new InvalidOperationException(UiText.Get("Steam kurulum klasörü bulunamadı. Steam'i bir kez açıp tekrar dene."));
        root = Path.GetFullPath(root);
        string clientPath = Path.Combine(root, "steamclient64.dll");
        if (!File.Exists(clientPath)) throw new FileNotFoundException(UiText.Get("Steam'in 64 bit istemci dosyası bulunamadı."));

        List<uint> candidates;
        using (var stream = new FileStream(Path.Combine(root, "appcache", "appinfo.vdf"), FileMode.Open,
                   FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            candidates = SteamAppCache.ReadAppIds(stream);

        // The library reader runs in its own short-lived process, without an App ID.
        // It never calls SteamAPI_Init, logs in, or announces a game as running.
        Environment.SetEnvironmentVariable("SteamAppId", null);
        Environment.SetEnvironmentVariable("SteamGameId", null);
        IntPtr module = LoadLibraryEx(clientPath, IntPtr.Zero, 0x00000100 | 0x00001000);
        if (module == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), UiText.Get("Steam istemci dosyası yüklenemedi."));
        // Keep module loaded until process exit: Steam owns internal worker threads.
        var create = Marshal.GetDelegateForFunctionPointer<CreateInterface>(NativeLibrary.GetExport(module, "CreateInterface"));
        IntPtr client = create("SteamClient023", IntPtr.Zero);
        if (client == IntPtr.Zero) throw new InvalidOperationException(UiText.Get("SteamClient023 desteklenmiyor. Steam'i güncelle."));
        int pipe = 0, user = 0;
        try
        {
            pipe = Method<CreatePipe>(client, 0)(client);
            if (pipe == 0) throw new InvalidOperationException(UiText.Get("Steam bağlantısı açılamadı."));
            user = Method<ConnectUser>(client, 2)(client, pipe);
            if (user == 0) throw new InvalidOperationException(UiText.Get("Açık Steam hesabı bulunamadı. Steam'e giriş yap."));
            IntPtr steamUser = Method<GetInterface>(client, 5)(client, user, pipe, "SteamUser023");
            IntPtr apps = Method<GetInterface>(client, 15)(client, user, pipe, "STEAMAPPS_INTERFACE_VERSION008");
            IntPtr metadata = Method<GetInterface>(client, 15)(client, user, pipe, "STEAMAPPS_INTERFACE_VERSION001");
            IntPtr friends = Method<GetInterface>(client, 8)(client, user, pipe, "SteamFriends017");
            if (steamUser == IntPtr.Zero || apps == IntPtr.Zero || metadata == IntPtr.Zero || friends == IntPtr.Zero)
                throw new InvalidOperationException(UiText.Get("Steam kütüphane arayüzü kullanılamıyor. Steam'i güncelleyip yeniden dene."));
            if (!SteamNative.SteamAPI_ISteamUser_BLoggedOn(steamUser))
                throw new InvalidOperationException(UiText.Get("Steam hesabı çevrimiçi değil. Giriş yaptıktan sonra Yenile'ye bas."));

            ulong steamId = SteamNative.SteamAPI_ISteamUser_GetSteamID(steamUser);
            if (steamId == 0) throw new InvalidOperationException(UiText.Get("Aktif Steam hesabı belirlenemedi."));
            string accountName = Marshal.PtrToStringUTF8(SteamNative.SteamAPI_ISteamFriends_GetPersonaName(friends)) ?? UiText.Get("Steam hesabı");
            var readData = Method<GetAppData>(metadata, 0);
            var buffer = new byte[4096];
            string ReadValue(uint id, string key)
            {
                Array.Clear(buffer);
                if (readData(metadata, id, key, buffer, buffer.Length) <= 0) return "";
                int end = Array.IndexOf(buffer, (byte)0);
                return Encoding.UTF8.GetString(buffer, 0, end < 0 ? buffer.Length : end);
            }

            var games = new List<LibraryGame>();
            foreach (uint id in candidates)
            {
                if (!SteamNative.SteamAPI_ISteamApps_BIsSubscribedApp(apps, id)) continue;
                string type = ReadValue(id, "type");
                if (!type.Equals("game", StringComparison.OrdinalIgnoreCase) &&
                    !type.Equals("demo", StringComparison.OrdinalIgnoreCase)) continue;
                string name = ReadValue(id, "name");
                games.Add(new LibraryGame(id, string.IsNullOrWhiteSpace(name) ? $"App ID {id}" : name,
                    SteamNative.SteamAPI_ISteamApps_BIsAppInstalled(apps, id)));
            }
            if (!SteamNative.SteamAPI_ISteamUser_BLoggedOn(steamUser) ||
                SteamNative.SteamAPI_ISteamUser_GetSteamID(steamUser) != steamId)
                throw new InvalidOperationException(UiText.Get("Steam hesabı tarama sırasında değişti. Yenile'ye tekrar bas."));
            return new SteamLibrarySnapshot(steamId, accountName,
                games.OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase).ToList(), candidates.Count);
        }
        finally
        {
            if (user != 0) Method<ReleaseUser>(client, 4)(client, pipe, user);
            if (pipe != 0) Method<ReleasePipe>(client, 1)(client, pipe);
        }
    }
}
