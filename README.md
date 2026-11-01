# SteamHourTracker

A lightweight Windows desktop app for running Steam idle sessions without launching the games themselves. Built with C# and .NET 10 Windows Forms, with English and Turkish interfaces.

SteamHourTracker connects to the Steam client already running on your computer. Regular sessions and library discovery do not require a password, Steam Guard code, or Web API key. Automatic card farming uses a separate sign-in on Steam's own website.

The app measures connected session time locally. Its timers do not prove that Steam has credited that time, and sessions cannot continue while the computer is turned off. Steam controls card eligibility and drop timing.

## Features

- Discover games from the current Steam account, including uninstalled games available in Steam's local cache.
- Run one game or multiple games simultaneously.
- Set individual time targets and resume unfinished targets across restarts.
- Reconnect automatically after supported connection failures.
- Optionally switch Steam to Invisible while idling and back to Online when finished.
- Scan for remaining card drops, prepare low-playtime games, and process the farming queue automatically.
- View card counts, preparation time remaining, and per-game status in a separate window.
- Exclude games from card mode, with separate lists for each Steam account.
- View current-week playtime totals and receive completion notifications.
- Keep sessions running in the system tray.
- Save preferences automatically, including the interface language.

## Requirements

| Component | Requirement |
| --- | --- |
| Operating system | Windows x64 |
| Steam | Installed, running, and signed in to the account whose games you want to use |
| Building from source | .NET 10 SDK; Visual Studio with .NET desktop development support is optional |
| Running a framework-dependent build | .NET 10 Windows Desktop Runtime |
| Steam web sign-in | Microsoft Edge WebView2 Runtime |

The project targets `net10.0-windows` and x64. The official `Microsoft.Web.WebView2` package is pinned to `1.0.4314.35`. If the browser runtime is missing, install it from [Microsoft's official WebView2 page](https://developer.microsoft.com/microsoft-edge/webview2/).

## Build and run

First, prepare the pinned Steam native dependency from the project directory:

```powershell
powershell -NoProfile -File .\scripts\Get-SteamNative.ps1
```

This downloads and verifies Valve's native DLL without executing it. The DLL is not stored in this repository or covered by the project's MIT license. See [native/README.md](native/README.md) for manual setup and provenance.

### Visual Studio

1. Open `SteamHourTracker.slnx` in a Visual Studio version that supports .NET 10.
2. Allow NuGet package restore to finish.
3. Press **F5** to build and run.

### Command line

Run these commands from the project directory:

```powershell
dotnet restore SteamHourTracker.csproj
dotnet build SteamHourTracker.csproj -c Release
dotnet run --project SteamHourTracker.csproj -c Release --no-build
```

After building, you can also run `bin\Release\net10.0-windows\SteamHourTracker.exe` directly. Keep it with the other files in its output directory; the executable alone is not a standalone package.

The build copies `native/steam_api64.dll` into the output directory. The app verifies its SHA-256 hash before loading it. See [Third-party components](#third-party-components) for provenance and dependency notices.

Opening the app restores preferences and reads the library. It does not start game sessions automatically.

## Quick start

1. Open Steam and sign in online.
2. Start SteamHourTracker and wait for the library to load. The top row shows the account name and game counts.
3. Select a game, search by name or App ID, or enter an App ID manually.
4. Press **Start** to begin a session and **Stop** to end it.

Use **Save** to add a manually entered game to **Saved manually**. After switching Steam accounts, press **Refresh library** to load the new account's games and saved selections.

Enable **Stop automatically** to set a limit of 1–10,080 minutes. Without a limit, the session continues until stopped.

### Language

Use **Dil / Language** at the top right to choose **English** or **Türkçe**. The selection applies immediately and is remembered across restarts. Stop active sessions and wait for library loading to finish before changing languages.

The interface, app messages, dialogs, tray menu, and notifications are translated. Game names, account names, and Steam's own web content are left unchanged. Instructions below use the English interface labels.

### System tray

Clicking the main window's **X** hides the app in the system tray; active sessions continue. Double-click the tray icon or choose **Show** to restore it. Choose **Exit** from the tray menu to stop all sessions and close the app completely.

Window resizing, maximizing, and minimizing are currently disabled. Scrolling keeps controls accessible at larger font sizes.

## Multiple games

1. Enable **Multiple games**.
2. Select a game or enter its App ID, then press **Add**. Repeat for the other games.
3. Press **Start selected** to run the selected games together.
4. Select rows and use **Stop selected** to stop individual games, or **Stop all** to end every session. Hold **Ctrl** to select multiple rows.

Duplicate App IDs are ignored. Searching or changing the library source does not clear the selected list. **Remove from list** and **Clear list** affect the selection, not manually saved games.

Each game's timer starts after its connection succeeds. A failure in one game does not stop the others. Hover over a row for details. Stop active sessions before editing the queue or targets.

Selections and their order are saved separately for each Steam account. Disable **Multiple games** to return to single-game mode.

## Time targets and saved progress

Select one or more rows and press **Time target**, or double-click a row. Choose **Custom time target**, enter a duration, and press **Apply**. Targets range from 1 minute to 168 hours. Each game stops independently when its target is reached.

**Use general time limit** follows the main window's automatic-stop setting. If that setting is off, the game runs without a time limit. Custom targets still apply when the general limit is disabled.

| Display | Meaning |
| --- | --- |
| **Target** | Hours and minutes; inherited targets are prefixed with **General** |
| **Time left** | Remaining target time, including progress from earlier sessions; unlimited sessions show **—** |
| **Time** / **Elapsed** | Current-session timers, not lifetime Steam playtime |

A target is time to complete **in this app**, not a desired total on your Steam profile. Stopping a three-hour target after one hour leaves two hours to run next time, including after an app restart.

Progress is saved approximately every five seconds while working, and when stopping, hiding to the tray, or exiting. An abrupt shutdown may lose time since the last successful save. Time while the app is closed or disconnected is not counted.

Completed targets are skipped on the next start. Use **Reset target** to begin counting again; this does not change Steam's recorded playtime. In single-game mode, it resets the current game's progress. In multiple-game mode, it resets the selected rows.

Changing a custom target resets that game's progress; applying the same target again preserves it. Changing or toggling the general limit resets progress for targets that inherit it across saved accounts, while custom targets remain intact. Loading preferences never resumes sessions automatically.

## Card farming

Press **Card mode…** to open the separate card window. Opening this window alone does not start games or scan badges. Press **Start** inside it to begin.

### Automatic mode

Automatic mode is enabled by default. Under **Settings**, keep **Scan the entire library and farm remaining cards automatically** enabled.

On first use, sign in on the official Steam page shown inside WebView2. The web account must match the account currently open in the desktop Steam client. A valid remembered web session can be reused on later runs.

The app reads all badge pages and selects games that both have remaining drops and appear in the local Steam library. Games with no drops remaining, badges with a blank eligibility field, and games missing from the local library are not started. Uninstalled games can be processed. Larger queues are handled in batches of up to 30 simultaneous games.

The default cycle for ready games is:

1. Run games together for **5 minutes**.
2. Stop them and scan badges again.
3. Wait **10 seconds**.
4. Start and stop each remaining game individually for **5 seconds**.
5. Scan again and repeat until no processable games remain.

When no games are ready, low-playtime games run together until they reach the preparation threshold. Only each game's missing time is added. Badge scans also run at the configured interval during preparation.

**The default preparation threshold is 2 hours. It is an app setting, not a confirmed requirement read from your Steam account.** You can set it from 0 to 10 hours; 0 disables preparation. A saved three-hour default from older versions is migrated to two hours once, while later explicit choices are preserved.

The app combines Steam-reported hours with connected time measured during the current run without counting the same time twice. A new run reads current hours from Steam again. Neither reaching the threshold nor cycling games guarantees a card drop or a faster drop rate.

### Card window

| Column or control | Meaning |
| --- | --- |
| **Steam hours** | Total playtime reported by the latest badge scan |
| **Total time** | Steam playtime adjusted using connected time measured in this run |
| **Preparation left** | Time needed to reach the threshold; pauses while disconnected |
| **Cards left** | Remaining drops reported by Steam |
| **Status** | Preparation, farming, reconnection, exclusion, or completion state |
| **Only games with remaining card drops** | Hide completed games and badges without displayed drop eligibility |
| **Skip selected** | Skip selected games for this run only |
| **Refresh** | Refresh displayed measurements; does not trigger an extra network scan |
| **Stop** | Cancel the entire card run, including pauses and retry waits |

Preparation time is not a countdown to the next card. Steam does not provide an exact next-drop time.

Closing the card window with **X** or **Close** leaves its work running. Reopen it with **Card mode…**. Closing the main window hides both windows to the tray. Use the tray's **Exit** command to stop everything.

### Manual mode

Disable automatic scanning in **Settings**, save, then press **Start**. Manual mode uses the single game or the 1–30 games selected in the main window. It does not read card counts or stop automatically when drops run out.

The together/scan interval can be set to 1–180 minutes. Individual runs and pauses can be set to 5–120 seconds. Manual rounds can be 1–100, or 0 to continue until stopped. The round limit does not apply to automatic mode.

### Exclusions and session behavior

Use **Exclusions…** while card mode is stopped. Check games to exclude them, then press **Save**. Searching preserves other selections. Uncheck and save to include a game again.

Permanent exclusions are stored per Steam account and apply to automatic and manual card modes. They do not affect regular sessions or targets. Excluded games show **Permanently excluded** and are omitted from remaining-work totals. **Skip selected** remains temporary. Stopping a selected game from the main window during card mode also skips it for the rest of that run.

Regular sessions and card mode cannot run together. Card mode preserves normal time targets and their saved progress, but its connected time contributes to the weekly summary. Optional Invisible status is maintained across stages and pauses, with an attempt to return Online after the run ends.

Temporary scan failures pause with games stopped and retry after 10, 20, 40, then at most 60 seconds. Expired authentication or unreadable pages stop the run. Missing or malformed data is not treated as zero cards; a present but empty eligibility field is shown separately. Persistent game errors are reported and skipped. Switching Steam accounts stops the card run. Each stage waits for the previous workers to stop before starting new ones.

Card settings are saved, but queue position and remaining card counts are not. Reopening the app never starts card mode automatically.

## Automatic reconnection

Supported connection failures trigger retries automatically. Only the affected game waits; other games keep running. Retry delays are 10, 20, 40, then at most 60 seconds. A successful connection resets the delay sequence. Account and status checks may add their own processing time.

The game shows **Reconnecting** while its timer and target progress are paused. Once connected, it resumes from the remaining time. Stopping the game or exiting cancels pending retries; hiding the app does not.

The active Steam account is checked before reconnecting. An account change requires **Refresh library** and a new manual start. Invalid App IDs, DLL/setup errors, and general connection/license failures on the first start are not retried automatically. Supported temporary failures after a successful connection can be retried.

If Steam is closed or signed out, open it or sign in again. Connection state is checked approximately once per second; Steam's server-side playtime credit is not independently verified.

## Invisible mode

Enable **Go invisible while idling** before starting. The app asks the local Steam client to switch to **Invisible** before launching sessions, and back to **Online** after the last session ends or all sessions are stopped. This setting is off by default and remembered afterward. When disabled, the app leaves Steam status alone.

Stopping only one of several games does not restore Online while the others remain active. Hiding to the tray preserves Invisible status. Reconnection reapplies it before restarting a game.

If the initial status command fails, games are not started. After an account change, the app does not change the replacement account's status. Steam's acceptance of the local command is checked, but visibility on friends' screens is not independently verified. A lost connection or forced app termination may prevent the return to Online.

## Weekly summary and notifications

**Weekly summary** shows time measured by this app for each game during the current Monday–Sunday week, using the computer's local time. Press **Refresh** in that window to update it while sessions are running.

- Each account has separate totals, with one accumulated entry per game.
- Simultaneous games count independently: two games running for one hour produce two hours of combined game time.
- Single-game, multiple-game, targeted, unlimited, and card sessions are included.
- Connection waits, reconnect delays, and time with the app closed are excluded.
- Resetting a target or removing a game from the selection does not erase its weekly total.
- A new week replaces the previous week's records. Older Steam playtime and historical target progress are not imported.

Totals use the existing settings file and regular checkpoints; there is no separate history database.

A Windows tray notification appears when a game's time target is reached or card mode ends naturally. Manual Stop and Exit do not produce completion notifications. Card-mode notifications report the actual result, including skipped games where applicable. Click a notification to restore the app. Windows notification settings may suppress it.

## Settings and local data

Preferences are saved automatically. Language, multiple-game mode, Invisible mode, automatic-stop settings, and card options are remembered. Current games, library source, selected-game order, targets, progress, exclusions, and weekly totals are separated by Steam account. Search text is cleared when reopening or refreshing the library.

| Location | Contents |
| --- | --- |
| `%LOCALAPPDATA%\SteamHourTracker\settings.json` | Preferences, Steam account IDs, game selections, targets, progress, exclusions, and current weekly totals |
| `%LOCALAPPDATA%\SteamHourTracker\games.json` | Manually saved game names and App IDs |
| `%LOCALAPPDATA%\SteamHourTracker\SteamWebView\<Steam ID>` | WebView2 profile and its remembered Steam web session |
| `%TEMP%\SteamHourTracker` | Temporary App ID files used by session workers |

Corrupt settings are backed up before defaults are loaded. Unsupported settings versions are preserved. Write failures keep the previous complete file and show a status message. When the library cannot be read, general preferences still load and manual selections use a separate local account entry.

Temporary session files are removed on normal shutdown; a forced termination may leave a small file behind. Helpers stop when their connection to the parent app closes. A helper that does not respond to Stop within four seconds is terminated.

## Steam web sign-in and connection protection

Automatic card mode uses HTTPS requests to Steam Community badge pages. Passwords and Steam Guard codes are entered on Steam's own page inside WebView2. Application code does not read or save password fields; browser password saving and autofill are disabled.

Session cookies are used in memory for badge requests and are not logged. The browser itself retains the web session in its local profile. **Reset web session** clears cookies from this app's web profile without signing out of the desktop Steam client. After successful verification, the sign-in window closes.

Certificate errors in the sign-in page or its resources cancel the connection and any pending verification. The same window cannot bypass the error. Check the network and computer date/time, then close and reopen the sign-in window. Valid HTTPS connections continue through the normal flow. This protection does not add a background service or scanner.

The app has no telemetry or automatic updater. It does not modify achievements, inventory, or game files, and does not launch game executables.

## Library discovery

The library reader connects to the current local Steam account without requiring a public profile or Web API key. It reads candidate App IDs from Steam's `appcache/appinfo.vdf`, checks the account's license response with `BIsSubscribedApp`, and lists games and demos while filtering out DLC, tools, and videos.

Uninstalled games can appear if Steam has cached their metadata. Newly added or uncached games may be missing. Open the Steam library, wait for it to load, then press **Refresh library**. Family sharing and temporary licenses depend on Steam's current response; this is not a guaranteed complete server inventory.

The reader uses the installed `steamclient64.dll`, which is not copied into the project. It does not initialize a game session. Reading runs in a separate process with a 45-second timeout and is cancelled on Exit.

## Resource usage

Visible counters update once per second. When both windows are hidden in the tray, screen updates stop while progress checkpoints continue approximately every five seconds. Restoring a window refreshes its display immediately. Time measurement is independent of UI refresh frequency.

Bursts of settings changes are combined, and unchanged settings are not rewritten. Card-stage checks use a one-second interval with shorter waits near a deadline. Steam callbacks run every 75 ms, and Stop wakes the wait immediately. Worker messages use bounded buffering and blocking reads rather than busy polling.

Each active game requires its own helper process, so running fewer simultaneous games reduces resource use. The WebView2 window is used for authentication rather than kept open throughout farming. There is no additional continuous polling timer for language selection or the card window.

## Troubleshooting

| Problem | What to check |
| --- | --- |
| Steam cannot connect | Open Steam, sign in online, and confirm the App ID belongs to a game available to the account. Run both apps under the same Windows user and privilege level. |
| Games are missing | Let Steam finish loading its library, then use **Refresh library**. See the cache limitations above. |
| DLL is missing or rejected | Rebuild and keep the verified DLL beside the executable. Use the original binary described in `THIRD-PARTY.md`. |
| Card sign-in is unavailable | Install WebView2 Runtime, confirm that the web and desktop accounts match, and reopen card mode if the web session expired. |
| Certificate error | Check the network and system clock, then reopen the sign-in window. The app does not bypass certificate errors. |
| App stays open after closing | **X** hides it to the tray. Use the tray menu's **Exit** command to close it completely. |
| Language button is disabled | Stop active sessions and wait for library loading or card-mode startup to finish. |
| Steam playtime or cards have not changed | Local timers are measurements only. Steam controls credited playtime and card drops; the app does not guarantee either. |

## Source layout

| Files | Responsibility |
| --- | --- |
| `Form1.cs`, `Form1.Designer.cs`, `Form1.*.cs` | Main UI, multiple-game selection, settings, progress, cards, language selection, and notifications |
| `UiText.cs`, `Strings.en.json` | Text translation and English resources |
| `SteamNative.cs`, `SteamWorker.cs`, `SteamSession.cs` | Native API declarations, game connection workers, and process lifecycle |
| `SteamSessionGroup.cs`, `SteamSessionCoordinator.cs`, `SteamPresence.cs` | Concurrent sessions, reconnection, and optional presence control |
| `SteamLibraryReader.cs`, `SteamLibraryProcess.cs`, `SteamAppCache.cs` | Local library discovery and cache parsing |
| `CardFarm.cs`, `CardFarm.Automatic.cs`, `SteamBadges.cs` | Card stages, automatic queue preparation, and badge parsing |
| `CardQueueDialog.cs`, `CardFarmDialog.cs`, `CardExclusionsDialog.cs` | Card dashboard, options, and exclusions |
| `SteamWebLoginDialog.cs` | Steam web authentication and certificate-error handling |
| `AppSettings.cs`, `GameTargetDialog.cs` | Persistent settings and per-game target editing |
| `WeeklyPlaytime.cs`, `WeeklySummaryDialog.cs` | Current-week accounting and summary UI |

## Third-party components

The app uses Valve's native `steam_api64.dll` (obtained separately) and the official Microsoft WebView2 package. It contains its own C# interop declarations, badge parser, and session orchestration; the Steamworks.NET C# wrapper, Steam Achievement Manager, and Idle Master implementations are not included.

See [THIRD-PARTY.md](THIRD-PARTY.md) for exact binary provenance, the pinned SHA-256 hash, references, and applicable third-party terms. The [WebView2 license](licenses/WebView2-LICENSE.txt) is included separately. The native Valve binary is not covered by the Steamworks.NET wrapper's MIT license.

## License

The original application source code and documentation are available under the [MIT License](LICENSE), copyright © 2026 Ege. Third-party components retain their own licenses and are not relicensed by this project's MIT license. See [THIRD-PARTY.md](THIRD-PARTY.md) and the [WebView2 license](licenses/WebView2-LICENSE.txt).

## References

- [Steam API documentation](https://partner.steamgames.com/doc/api/steam_api)
- [Steam Trading Cards FAQ](https://steamcommunity.com/tradingcards/faq)
- [Idle Master Extended: Fast mode behavior](https://github.com/jonas-med-ett-s/idle_master_extended/wiki/Fast-mode)
- [ArchiSteamFarm: preparation threshold explanation](https://github.com/JustArchiNET/ArchiSteamFarm/wiki/Configuration#hoursuntilcarddrops)

These references describe the APIs and behavior consulted during development; they are not additional application dependencies.
