[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$expectedHash = 'EB17909A76668CF9AE0B92A618A34A50F6C73D3A6787CB4DD8CE36A8B10BFB75'
$sourceUri = 'https://raw.githubusercontent.com/rlabrecque/Steamworks.NET/2025.164.1/com.rlabrecque.steamworks.net/Plugins/steam_api64.dll'
$nativeDirectory = Join-Path (Split-Path -Parent $PSScriptRoot) 'native'
$destination = Join-Path $nativeDirectory 'steam_api64.dll'

if (Test-Path -LiteralPath $destination) {
    if ((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -ne $expectedHash) {
        throw 'The existing native/steam_api64.dll does not match the pinned version. It was left unchanged.'
    }
    Write-Output 'The verified Steam native library is already present.'
    return
}

New-Item -ItemType Directory -Path $nativeDirectory -Force | Out-Null
$temporary = Join-Path $nativeDirectory ('steam_api64.dll.download-' + [Guid]::NewGuid().ToString('N'))
try {
    Invoke-WebRequest -Uri $sourceUri -OutFile $temporary -UseBasicParsing -TimeoutSec 60
    if ((Get-FileHash -LiteralPath $temporary -Algorithm SHA256).Hash -ne $expectedHash) {
        throw 'Downloaded Steam native library failed SHA-256 verification. Nothing was installed.'
    }
    # Do not overwrite a file another process may have created during download.
    [System.IO.File]::Move($temporary, $destination)
    Write-Output 'Downloaded and verified native/steam_api64.dll.'
}
finally {
    if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force }
}
