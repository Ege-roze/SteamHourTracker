# Steam native library provenance

File: `native/steam_api64.dll`

This binary is not included in the source repository. Source builds obtain it separately using the instructions in `native/README.md`; the helper script verifies the pinned hash. Ready-to-run Windows release packages include the unmodified redistributable beside the executable, as described in Valve's [Steamworks API setup documentation](https://partner.steamgames.com/doc/sdk/api). The project's MIT license applies to original application code, not this binary.

Publisher: Valve Corp.

Source release: Steamworks.NET `2025.164.1` (Steamworks SDK 1.64).

Exact source URL:
https://raw.githubusercontent.com/rlabrecque/Steamworks.NET/2025.164.1/com.rlabrecque.steamworks.net/Plugins/steam_api64.dll

SHA-256:
`EB17909A76668CF9AE0B92A618A34A50F6C73D3A6787CB4DD8CE36A8B10BFB75`

During preparation, Windows Authenticode validation returned **Valid**.
Signer subject: `CN=Valve Corp., O=Valve Corp., L=Bellevue, S=Washington, C=US`.

The DLL is a Valve binary; it is not covered by the Steamworks.NET C# wrapper's MIT license.
The Steamworks.NET wrapper itself is not included or used by this project.
Valve's Steamworks SDK terms apply to the native library; inspect those terms before redistributing it.

## .NET runtime in ready-to-run packages

Self-contained Windows releases include Microsoft's .NET and Windows Desktop runtime components. Their original license and third-party notices are included under `licenses`. These components retain their own licenses; the project's MIT license does not replace them.

API signatures were checked against the same release's `CodeGen/steam/steam_api.h`
and `CodeGen/steam/steam_api_flat.h`. This project contains its own minimal C# interop declarations.

## Local Steam library reader

Uses the installed Valve `steamclient64.dll`, loaded from Steam's registered installation directory.
No copy of that DLL or SAM executable is distributed. `SteamClient023`, `SteamUser023`,
`SteamFriends017`, and `STEAMAPPS_INTERFACE_VERSION008` follow Valve's SDK signatures.
Legacy `STEAMAPPS_INTERFACE_VERSION001.GetAppData` was checked against SAM's wrapper declaration.
This is an independent small implementation, not a copy of SAM's implementation.

Reference implementations/specifications consulted:
- https://github.com/gibbed/SteamAchievementManager/blob/master/SAM.Picker/GamePicker.cs
- https://github.com/gibbed/SteamAchievementManager/blob/master/SAM.API/Client.cs
- https://github.com/gibbed/SteamAchievementManager/blob/master/SAM.API/Wrappers/SteamApps001.cs
- https://github.com/xPaw/SteamAppInfo/blob/master/README.md

Cache entry IDs are parsed without reading its access-token fields or dumping raw cache data.

## Optional local presence control

`SteamPresence.cs` checks the active account through SteamClient023 / SteamUser023,
then runs the installed Steam executable with `steam://friends/status/invisible`
or `steam://friends/status/online`. These URLs were verified in the installed Valve
client's `resource/menus/steam.menu` and `steamui/sp.js` on 2026-10-20.
No third-party status changer is included. No login credentials are read.
The helper uses no App ID and does not initialize a game session.
Steam's game-facing GetPersonaState is not used to acknowledge invisibility:
it reported Online while the Friends setting was Invisible during validation.

## Microsoft WebView2

Official NuGet package: `Microsoft.Web.WebView2`, version `1.0.4314.35`, publisher Microsoft.
Source: https://www.nuget.org/packages/Microsoft.Web.WebView2/1.0.4314.35
The package's unchanged license is included as `licenses/WebView2-LICENSE.txt`.
The separately installed Microsoft Edge WebView2 Runtime supplies the browser engine.
Only Core/WinForms assemblies are used. The WPF reference is removed during project evaluation,
after the explicit SDK/NuGet imports, so it is absent from both Visual Studio's dependency list
and compilation. WebView2's native loader targets remain enabled.

The official browser component is used for Steam's own login page.
Card inventory parsing, queue preparation and orchestration are original application code.
No Idle Master/ASF implementation is incorporated. Behaviour was researched in these primary documents:
- https://github.com/jonas-med-ett-s/idle_master_extended/wiki/Login-methods
- https://github.com/jonas-med-ett-s/idle_master_extended/wiki/Fast-mode
- https://github.com/JustArchiNET/ArchiSteamFarm/wiki/Configuration#hoursuntilcarddrops
- https://steamcommunity.com/tradingcards/faq
