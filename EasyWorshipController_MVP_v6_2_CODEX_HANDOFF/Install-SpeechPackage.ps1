param(
    [ValidateSet('whisper','nemotron')][string]$Engine='whisper',
    [string]$CacheDirectory='',
    [string]$CancelFile='',
    [switch]$Offline,
    [switch]$Status
)
$ErrorActionPreference='Stop'
function Get-Sha256([string]$Path) {
    $sha=[Security.Cryptography.SHA256]::Create()
    $stream=[IO.File]::OpenRead($Path)
    try { return [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','') }
    finally { $stream.Dispose();$sha.Dispose() }
}
$manifest=Get-Content (Join-Path $PSScriptRoot 'speech-runtime/packages.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$package=$manifest.packages | Where-Object { $_.id -eq $Engine }
if(!$package){throw 'Unknown engine package'}
$root=Join-Path $PSScriptRoot 'speech-packages'
$target=Join-Path $root $package.directory
if(!$CacheDirectory){$CacheDirectory=Join-Path $root 'downloads'}
$CacheDirectory=[IO.Path]::GetFullPath($CacheDirectory)
function Check-Cancel {
    if($CancelFile -and (Test-Path -LiteralPath $CancelFile)){throw [OperationCanceledException]::new('Installation canceled. Verified downloads and partial downloads are retained for resume.')}
}
function Verify-File($path,$entry) {
    return (Test-Path -LiteralPath $path) -and (Get-Item -LiteralPath $path).Length -eq $entry.bytes -and
        (Get-Sha256 $path) -eq $entry.sha256
}
function Check-Installed {
    $receipt=Join-Path $target 'installed.json'
    if(!(Test-Path -LiteralPath $receipt)){return $false}
    try {
        $installed=Get-Content -LiteralPath $receipt -Raw | ConvertFrom-Json
        if($installed.package -ne $package.directory -or !$installed.files){return $false}
        $safeRoot=[IO.Path]::GetFullPath($target).TrimEnd('\')+'\'
        foreach($file in $installed.files){
            Check-Cancel
            $candidate=[IO.Path]::GetFullPath((Join-Path $target $file.path))
            if(!$candidate.StartsWith($safeRoot,[StringComparison]::OrdinalIgnoreCase) -or !(Verify-File $candidate $file)){return $false}
        }
        return $true
    } catch [OperationCanceledException] { throw }
      catch { return $false }
}
$downloadBytes=($package.artifacts | Measure-Object -Property bytes -Sum).Sum
Write-Output "$($package.name): $downloadBytes download bytes before cache reuse."
if(Check-Installed){$size=(Get-ChildItem -LiteralPath $target -Recurse -File | Measure-Object -Property Length -Sum).Sum;Write-Output "Installed and verified offline. Installed bytes: $size";exit 0}
if($Status){Write-Output 'Not installed, incomplete, or integrity check failed.';exit 2}
New-Item -ItemType Directory -Force -Path $root,$CacheDirectory | Out-Null
$lockPath=Join-Path $root ($Engine+'.install.lock')
$installLock=[IO.File]::Open($lockPath,[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
$staging=Join-Path $root ('.stg-'+[guid]::NewGuid().ToString('N').Substring(0,8))
New-Item -ItemType Directory -Path $staging | Out-Null
try {
    foreach($artifact in $package.artifacts) {
        Check-Cancel
        $cached=Join-Path $CacheDirectory $artifact.file
        if(!(Verify-File $cached $artifact)) {
            if($Offline){throw "Offline cache is missing or invalid: $($artifact.file)"}
            $partial=$cached+'.partial'
            if(!(Verify-File $partial $artifact)) {
                if(!(Get-Command curl.exe -ErrorAction SilentlyContinue)){throw 'curl.exe is required to download resumable packages on this Windows installation.'}
                Write-Output "Downloading $($artifact.file) (resume supported)..."
                $start=New-Object Diagnostics.ProcessStartInfo
                $start.FileName='curl.exe';$start.UseShellExecute=$false;$start.CreateNoWindow=$true
                $start.Arguments='--fail --location --retry 3 --connect-timeout 20 --continue-at - --output "'+$partial+'" "'+$artifact.url+'"'
                $download=[Diagnostics.Process]::Start($start)
                try {while(!$download.WaitForExit(200)){try{Check-Cancel}catch{$download.Kill();throw}};if($download.ExitCode -ne 0){throw "Download interrupted ($($download.ExitCode)); retry to resume."}}
                finally {$download.Dispose()}
            }
            if(!(Verify-File $partial $artifact)){throw "Checksum or size mismatch: $($artifact.file). Package was not activated."}
            Move-Item -LiteralPath $partial -Destination $cached -Force
        }
        Check-Cancel
        $destination=Join-Path $staging $artifact.destination
        New-Item -ItemType Directory -Force -Path $destination | Out-Null
        if($artifact.archive) {
            Add-Type -AssemblyName System.IO.Compression.FileSystem
            $zip=[IO.Compression.ZipFile]::OpenRead($cached)
            try {
                foreach($entry in $zip.Entries) {
                    Check-Cancel
                    if(!$entry.Name){continue}
                    if($artifact.include -and $entry.FullName -notmatch $artifact.include){continue}
                    $file=[IO.Path]::GetFullPath((Join-Path $destination $entry.FullName))
                    $safeRoot=[IO.Path]::GetFullPath($destination).TrimEnd('\')+'\'
                    if(!$file.StartsWith($safeRoot,[StringComparison]::OrdinalIgnoreCase)){throw 'Archive contains an unsafe path.'}
                    New-Item -ItemType Directory -Force -Path ([IO.Path]::GetDirectoryName($file)) | Out-Null
                    [IO.Compression.ZipFileExtensions]::ExtractToFile($entry,$file,$true)
                }
            }finally{$zip.Dispose()}
        }else{Copy-Item -LiteralPath $cached -Destination (Join-Path $destination $artifact.installedName)}
    }
    if($Engine -eq 'nemotron') {
        [IO.File]::WriteAllText((Join-Path $staging 'python/python312._pth'),"python312.zip`r`n.`r`nLib\site-packages`r`n",[Text.Encoding]::ASCII)
        $model=$package.artifacts | Where-Object {$_.installedName -like '*.gguf'}
        ConvertTo-Json -InputObject @(@{path=('nemotron/'+$model.installedName);bytes=$model.bytes;sha256=$model.sha256;url=$model.url}) | Set-Content -LiteralPath (Join-Path $staging 'manifest.json') -Encoding UTF8
        'Licensed by NVIDIA Corporation under the NVIDIA Open Model License' | Set-Content -LiteralPath (Join-Path $staging 'licenses/NOTICE.txt') -Encoding UTF8
        & (Join-Path $staging 'python/python.exe') -c "import websocket,ctypes,hashlib; assert websocket.__version__ == '1.9.0'"
        if($LASTEXITCODE -ne 0){throw 'Offline Python/runtime import check failed.'}
    }
    $files=@(Get-ChildItem -LiteralPath $staging -Recurse -File | ForEach-Object {
        Check-Cancel
        @{path=$_.FullName.Substring($staging.Length+1);bytes=$_.Length;sha256=(Get-Sha256 $_.FullName)}
    })
    @{package=$package.directory;installedUtc=[DateTime]::UtcNow.ToString('o');files=$files} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $staging 'installed.json') -Encoding UTF8
    Check-Cancel
    # A complete older package remains recoverable. Never replace running assets in place.
    if(Test-Path -LiteralPath $target){Move-Item -LiteralPath $target -Destination ($target+'.previous-'+[guid]::NewGuid().ToString('N'))}
    Move-Item -LiteralPath $staging -Destination $target
    [long]$installedBytes=0;foreach($file in $files){$installedBytes+=$file.bytes}
    Write-Output ('Installed '+$package.name+'. Verified file bytes: '+$installedBytes)
}finally{
    $installLock.Dispose()
    # Only this invocation's generated staging directory may be removed.
    $safeRoot=[IO.Path]::GetFullPath($root).TrimEnd('\')+'\'
    $safeStaging=[IO.Path]::GetFullPath($staging)
    if($safeStaging.StartsWith($safeRoot,[StringComparison]::OrdinalIgnoreCase) -and
       [IO.Path]::GetFileName($safeStaging) -match '^\.stg-[0-9a-f]{8}$' -and
       (Test-Path -LiteralPath $safeStaging)){
        Remove-Item -LiteralPath $safeStaging -Recurse -Force
    }
}
