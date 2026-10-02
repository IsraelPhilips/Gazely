$ErrorActionPreference='Stop'
$pending=Join-Path $PSScriptRoot 'Gazely.pending.exe'
$current=Join-Path $PSScriptRoot 'Gazely.exe'
if(!(Test-Path -LiteralPath $pending)){exit 0}
try {
 if(Test-Path -LiteralPath $current){Copy-Item -LiteralPath $current -Destination (Join-Path $PSScriptRoot 'Gazely.previous.exe') -Force}
 Move-Item -LiteralPath $pending -Destination $current -Force
 Write-Host 'Gazely update activated. Previous build retained as Gazely.previous.exe.'
 exit 0
} catch {
 Write-Host 'Close the running Gazely window, then run RUN_GAZELY.bat again. The pending update has been preserved.'
 Write-Host $_.Exception.Message
 exit 1
}
