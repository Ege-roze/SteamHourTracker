using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace SteamHourTracker;

// Only the documented subset needed for a local Steam session.
// No achievement, password, inventory, or Web API calls.
internal static class SteamNative
{
    private const string Dll = "steam_api64.dll";
    private const string ExpectedSha256 = "EB17909A76668CF9AE0B92A618A34A50F6C73D3A6787CB4DD8CE36A8B10BFB75";

    public static void Load()
    {
        string path = Path.Combine(AppContext.BaseDirectory, Dll);
        if (!File.Exists(path))
            throw new FileNotFoundException(UiText.Get("steam_api64.dll bulunamadı. Projeyi yeniden derle; DLL, uygulamayla aynı klasörde olmalı."));
        using (var file = File.OpenRead(path))
        {
            if (Convert.ToHexString(SHA256.HashData(file)) != ExpectedSha256)
                throw new InvalidOperationException(UiText.Get("Steam DLL dosyası doğrulanmış sürümle eşleşmiyor. native klasöründeki özgün dosyayı kullan."));
        }
        NativeLibrary.SetDllImportResolver(typeof(SteamNative).Assembly,
            (name, assembly, searchPath) => name == Dll ? NativeLibrary.Load(path) : IntPtr.Zero);
    }

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int SteamAPI_InitFlat([Out] byte[] errorMessage);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void SteamAPI_Shutdown();
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void SteamAPI_RunCallbacks();
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool SteamAPI_IsSteamRunning();
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr SteamAPI_SteamUser_v023();
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool SteamAPI_ISteamUser_BLoggedOn(IntPtr self);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr SteamAPI_SteamUtils_v010();
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint SteamAPI_ISteamUtils_GetAppID(IntPtr self);

    // These flat calls also accept interfaces acquired from the local Steam client.
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    internal static extern ulong SteamAPI_ISteamUser_GetSteamID(IntPtr self);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr SteamAPI_ISteamFriends_GetPersonaName(IntPtr self);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool SteamAPI_ISteamApps_BIsSubscribedApp(IntPtr self, uint appId);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool SteamAPI_ISteamApps_BIsAppInstalled(IntPtr self, uint appId);
}
