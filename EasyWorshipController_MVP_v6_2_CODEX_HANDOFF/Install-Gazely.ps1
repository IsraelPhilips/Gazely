param(
    [Parameter(Mandatory=$true)][string]$Archive,
    [Parameter(Mandatory=$true)][string]$ExpectedSha256,
    [string]$InstallRoot = (Join-Path $env:LOCALAPPDATA 'Programs\Gazely'),
    [switch]$NoShortcuts
)
$ErrorActionPreference = 'Stop'
function Get-Sha256([string]$Path) {
    $sha = [Security.Cryptography.SHA256]::Create()
    $stream = [IO.File]::OpenRead($Path)
    try { return [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','') }
    finally { $stream.Dispose(); $sha.Dispose() }
}
$archivePath = [IO.Path]::GetFullPath($Archive)
if (-not (Test-Path -LiteralPath $archivePath)) { throw "Gazely package not found: $archivePath" }
$actualHash = Get-Sha256 $archivePath
if ($actualHash -ne $ExpectedSha256) { throw 'Gazely package checksum did not match; nothing was installed.' }
$packageName = [IO.Path]::GetFileNameWithoutExtension($archivePath)
$root = [IO.Path]::GetFullPath($InstallRoot)
$target = Join-Path $root $packageName

if (Test-Path -LiteralPath $target) {
    $receipt = Join-Path $target 'package-sha256.txt'
    if (-not (Test-Path -LiteralPath $receipt) -or (Get-Content -LiteralPath $receipt -Raw).Trim() -ne $actualHash) {
        throw "An existing Gazely installation needs review: $target"
    }
    Write-Output "Gazely is already installed at $target"
} else {
    New-Item -ItemType Directory -Path $root -Force | Out-Null
    # Keep extraction short enough for Windows PowerShell/.NET Framework paths.
    $stagingParent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    $staging = Join-Path $stagingParent ('gz-' + [guid]::NewGuid().ToString('N').Substring(0,8))
    New-Item -ItemType Directory -Path $staging | Out-Null
    try {
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        [IO.Compression.ZipFile]::ExtractToDirectory($archivePath,$staging)
        $package = Join-Path $staging $packageName
        $manifestPath = Join-Path $package 'SHA256-MANIFEST.json'
        if (-not (Test-Path -LiteralPath $manifestPath)) { throw 'Gazely package manifest is missing.' }
        $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
        if ($manifest.Count -lt 300) { throw 'Gazely package appears incomplete.' }
        $safeRoot = [IO.Path]::GetFullPath($package).TrimEnd('\') + '\'
        foreach ($entry in $manifest) {
            $path = [IO.Path]::GetFullPath((Join-Path $package $entry.path))
            if (-not $path.StartsWith($safeRoot,[StringComparison]::OrdinalIgnoreCase) -or
                -not (Test-Path -LiteralPath $path) -or
                (Get-Item -LiteralPath $path).Length -ne $entry.bytes -or
                (Get-Sha256 $path) -ne $entry.sha256) {
                throw "Gazely package file failed verification: $($entry.path)"
            }
        }
        Move-Item -LiteralPath $package -Destination $target
        [IO.File]::WriteAllText((Join-Path $target 'package-sha256.txt'),$actualHash,[Text.Encoding]::ASCII)
        Write-Output "Gazely installed at $target"
    } finally {
        $safeParent = $stagingParent.TrimEnd('\') + '\'
        if ([IO.Path]::GetFullPath($staging).StartsWith($safeParent,[StringComparison]::OrdinalIgnoreCase) -and
            (Test-Path -LiteralPath $staging)) {
            Remove-Item -LiteralPath $staging -Recurse -Force
        }
    }
}

if (-not $NoShortcuts) {
    $shell = New-Object -ComObject WScript.Shell
    $menu = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
    $shortcutPath = Join-Path $menu 'Gazely.lnk'
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = Join-Path $target 'Gazely.exe'
    $shortcut.WorkingDirectory = $target
    $shortcut.IconLocation = Join-Path $target 'assets\Gazely.ico'
    $shortcut.Save()
    Write-Output 'Gazely Start Menu shortcut created.'
    $uninstaller = Join-Path $target 'Uninstall-Gazely.ps1'
    if (-not (Test-Path -LiteralPath $uninstaller)) { throw 'Gazely uninstaller is missing from the package.' }
    $key = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\' + $packageName
    New-Item -Path $key -Force | Out-Null
    Set-ItemProperty -Path $key -Name DisplayName -Value "Gazely ($packageName)"
    Set-ItemProperty -Path $key -Name DisplayIcon -Value (Join-Path $target 'Gazely.exe')
    Set-ItemProperty -Path $key -Name Publisher -Value 'Gazely'
    Set-ItemProperty -Path $key -Name InstallLocation -Value $target
    Set-ItemProperty -Path $key -Name UninstallString -Value ('powershell.exe -NoProfile -ExecutionPolicy Bypass -File "' + $uninstaller + '"')
    Set-ItemProperty -Path $key -Name NoModify -Value 1
    Set-ItemProperty -Path $key -Name NoRepair -Value 1
    Write-Output 'Gazely registered in Windows Installed apps.'
}
