# Steam native dependency

The Valve binary `steam_api64.dll` is required at build/run time and is not included in this source repository. It is a third-party component, separate from the project's MIT-licensed source code.

From the project directory, run:

```powershell
powershell -NoProfile -File .\scripts\Get-SteamNative.ps1
```

The script downloads the pinned binary from the Steamworks.NET `2025.164.1` source release and verifies its SHA-256 hash before placing it here. It does not execute the downloaded file or run automatically during the build. A valid existing file is reused; an unexpected existing file is left untouched.

You can also obtain the matching binary yourself and place it at `native/steam_api64.dll`.

- Source: https://raw.githubusercontent.com/rlabrecque/Steamworks.NET/2025.164.1/com.rlabrecque.steamworks.net/Plugins/steam_api64.dll
- SHA-256: `EB17909A76668CF9AE0B92A618A34A50F6C73D3A6787CB4DD8CE36A8B10BFB75`

See [THIRD-PARTY.md](../THIRD-PARTY.md) for provenance and applicable terms. Valve's native binary is not relicensed under MIT.
