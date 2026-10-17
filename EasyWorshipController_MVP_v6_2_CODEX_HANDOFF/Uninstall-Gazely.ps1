$ErrorActionPreference = 'Stop'
$target = [IO.Path]::GetFullPath($PSScriptRoot).TrimEnd('\')
$root = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs\Gazely')).TrimEnd('\')
$packageName = [IO.Path]::GetFileName($target)
if (-not $target.StartsWith($root + '\',[StringComparison]::OrdinalIgnoreCase) -or
    [IO.Path]::GetDirectoryName($target) -ine $root -or
    $packageName -notmatch '^Gazely-Windows-[A-Za-z0-9-]+$' -or
    -not (Test-Path -LiteralPath (Join-Path $target 'package-sha256.txt')) -or
    -not (Test-Path -LiteralPath (Join-Path $target 'Gazely.exe'))) {
    throw 'Gazely installation path could not be verified; nothing was removed.'
}
$running = Get-CimInstance Win32_Process -Filter "Name = 'Gazely.exe'" -ErrorAction SilentlyContinue |
    Where-Object { $_.ExecutablePath -and $_.ExecutablePath.StartsWith($target + '\',[StringComparison]::OrdinalIgnoreCase) }
if ($running) { throw 'Close this Gazely version before uninstalling it.' }
$shortcutPath = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Gazely.lnk'
if (Test-Path -LiteralPath $shortcutPath) {
    $shortcut = (New-Object -ComObject WScript.Shell).CreateShortcut($shortcutPath)
    if ($shortcut.TargetPath -ieq (Join-Path $target 'Gazely.exe')) { Remove-Item -LiteralPath $shortcutPath }
}
Remove-Item -LiteralPath $target -Recurse -Force
$key = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\' + $packageName
if (Test-Path -LiteralPath $key) { Remove-Item -LiteralPath $key -Force }
Write-Output 'Gazely was uninstalled. Local settings and history were preserved.'
