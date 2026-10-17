param(
    [string]$OutputDirectory = (Join-Path (Split-Path $PSScriptRoot -Parent) 'releases/Gazely-Windows-MVP-20261015'),
    [switch]$NoArchive
)
$ErrorActionPreference = 'Stop'
$source = $PSScriptRoot
$destination = [IO.Path]::GetFullPath($OutputDirectory)
$archive = $destination + '.zip'
if(-not (Test-Path -LiteralPath (Join-Path $source 'cloud-release.json'))) { throw 'Release marker is missing. Build the candidate with an explicit release mode.' }
$cloudMarker=Get-Content -LiteralPath (Join-Path $source 'cloud-release.json') -Raw | ConvertFrom-Json
if(-not (($cloudMarker.access_mode -eq 'free_offline' -and $cloudMarker.environment -eq 'offline' -and -not $cloudMarker.supabase_url -and -not $cloudMarker.portal_url) -or
          ($cloudMarker.access_mode -eq 'email_entry' -and $cloudMarker.environment -eq 'production' -and $cloudMarker.supabase_url -match '^https://') -or
          ($cloudMarker.access_mode -eq 'verified_email' -and $cloudMarker.environment -eq 'production' -and $cloudMarker.portal_url -match '^https://'))) {
    throw 'Public packaging requires an offline free build or a production cloud build.'
}
$candidateHash=(Get-FileHash -LiteralPath (Join-Path $source 'Gazely.mvp-candidate.exe') -Algorithm SHA256).Hash
if($cloudMarker.candidate_sha256 -ne $candidateHash) { throw 'Candidate does not match the release build.' }
if (Test-Path -LiteralPath $destination) { throw "Package directory already exists: $destination" }
if (-not $NoArchive -and (Test-Path -LiteralPath $archive)) { throw "Package archive already exists: $archive" }

foreach ($engine in @('whisper','nemotron')) {
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $source 'Install-SpeechPackage.ps1') -Engine $engine -Offline -Status | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "The installed $engine package failed offline verification." }
}

$files = @(
    'Gazely.mvp-candidate.exe',
    'cloud-release.json',
    'Install-SpeechPackage.ps1',
    'Uninstall-Gazely.ps1',
    'assets/Gazely.ico',
    'assets/gazely.png',
    'models/ggml-silero-v6.2.0.bin',
    'corpus/kjv.tsv',
    'corpus/web-search.tsv',
    'corpus/NOTICE.txt',
    'corpus/provenance.json',
    'corpus/web-provenance.json',
    'speech-runtime/nemotron_worker.py',
    'speech-runtime/packages.json'
)
$directories = @(
    'models/scripture-search',
    'search-runtime',
    'speech-packages/whisper-base-en-v1',
    'speech-packages/nemotron-q8-v1'
)
foreach ($relative in $files + $directories) {
    if (-not (Test-Path -LiteralPath (Join-Path $source $relative))) { throw "Required package input is missing: $relative" }
}

New-Item -ItemType Directory -Path $destination -Force | Out-Null
foreach ($relative in $files) {
    $target = Join-Path $destination $relative
    New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $source $relative) -Destination $target
}
foreach ($relative in $directories) {
    $target = Join-Path $destination $relative
    New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $source $relative) -Destination $target -Recurse
}
Move-Item -LiteralPath (Join-Path $destination 'Gazely.mvp-candidate.exe') -Destination (Join-Path $destination 'Gazely.exe')

$readme = @'
Gazely Windows MVP — initial public build

1. Extract the entire archive into a folder where your Windows account can save files.
2. Open Gazely.exe. No sign-in or internet connection is needed. Everything needed for the two included speech engines is bundled; no model download is needed.
3. Follow the first-run guide. Bible versions proposes an order from read-only local Bible headers. Compare every abbreviation with EasyWorship > Scriptures, including the last row; leave out "More Available...". Edit any mismatch, then confirm and save. If local discovery is unavailable, enter the visible codes one per line or separated by commas.
4. Open EasyWorship 7 and use manual Prepare/Present while checking its live output. Gazely does not treat a live slide as proof of the currently selected Scriptures-library version.

Gazely uses the saved Bible order and its tracked selection for both manual and automatic presentation. Spoken references default to KJV. If someone changes the Bible directly in EasyWorship, use Reset version tracking in Gazely before the next send; the next action reselects from the top. A readable conflicting library row stops output, but an unreadable row does not. The first public build has not been validated on an 8 GB PC, and live EasyWorship selection readback could not be verified on the development machine. The two-hour endurance run was waived; two 30-minute paced replays passed on the development PC. Semantic/quotation suggestions require operator review.

The Bible map, preferences and diagnostics are created locally after launch. This package contains no user's Bible map, preferences, recordings or logs. Keep the extracted folder writable. Windows PowerShell and EasyWorship 7 are required for the respective local verification and presentation features.
'@
[IO.File]::WriteAllText((Join-Path $destination 'README.txt'), $readme, [Text.Encoding]::UTF8)

$manifest = foreach ($item in (Get-ChildItem -LiteralPath $destination -File -Recurse | Sort-Object FullName)) {
    $relative = $item.FullName.Substring($destination.Length + 1).Replace('\','/')
    [pscustomobject]@{ path=$relative; bytes=$item.Length; sha256=(Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash }
}
$manifest | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $destination 'SHA256-MANIFEST.json') -Encoding UTF8
$bytes = (Get-ChildItem -LiteralPath $destination -File -Recurse | Measure-Object Length -Sum).Sum
Write-Output "Package files verified: $($manifest.Count); installed bytes: $bytes"

if (-not $NoArchive) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::CreateFromDirectory($destination,$archive,[IO.Compression.CompressionLevel]::Optimal,$true)
    Write-Output "Archive: $archive"
    Write-Output "Archive SHA256: $((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash)"
}
