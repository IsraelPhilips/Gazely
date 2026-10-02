param(
    [ValidateSet("tiny.en","base.en","small.en","fast","both")]
    [string]$Model = "base.en"
)

$ErrorActionPreference = "Stop"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$runtimeDir = Join-Path $here "whisper\runtime"
$modelDir = Join-Path $here "models"
$tempDir = Join-Path $env:TEMP "ew-whisper-setup"
$runtimeBuild = "b5130"
$runtimeUrl = "https://github.com/ggml-org/whisper.cpp/releases/download/$runtimeBuild/whisper-bin-x64.zip"
$runtimeSha256 = "f9ec6c52a2e949b62ab51fa21d0d497958f9e41c3010c157c4e42932d5316f3c"
$zipPath = Join-Path $tempDir "whisper-bin-x64.zip"
$tinyUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny.en.bin?download=true"
$baseUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.en.bin?download=true"
$smallUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small.en.bin?download=true"
$vadUrl = "https://huggingface.co/ggml-org/whisper-vad/resolve/main/ggml-silero-v6.2.0.bin?download=true"

New-Item -ItemType Directory -Force -Path $runtimeDir,$modelDir,$tempDir | Out-Null

function Download-File {
    param(
        [string]$Url,
        [string]$Destination,
        [string]$Label,
        [long]$MinimumBytes
    )

    if (Test-Path $Destination) {
        $existing = (Get-Item $Destination).Length
        if ($existing -ge $MinimumBytes) {
            Write-Host "$Label already exists ($existing bytes) - skipping."
            return
        }
        Remove-Item $Destination -Force
    }

    Write-Host ""
    Write-Host "Downloading $Label..."
    Write-Host $Url

    $curl = Get-Command curl.exe -ErrorAction SilentlyContinue
    if ($null -ne $curl) {
        & $curl.Source -L --fail --retry 3 --retry-delay 2 --connect-timeout 20 -o $Destination $Url
        if ($LASTEXITCODE -ne 0) {
            throw "curl download failed for $Label (exit code $LASTEXITCODE)."
        }
    } else {
        Invoke-WebRequest -Uri $Url -OutFile $Destination -UseBasicParsing
    }

    if (!(Test-Path $Destination)) { throw "$Label file was not created." }
    $size = (Get-Item $Destination).Length
    if ($size -lt $MinimumBytes) { throw "$Label download is unexpectedly small ($size bytes)." }

    Write-Host "Saved: $Destination"
    Write-Host "Size: $size bytes"
}

try {
    Write-Host ""
    Write-Host "EasyWorship Assistant - Local Whisper Setup v6.1"
    Write-Host "================================================="
    Write-Host "Runtime build: $runtimeBuild"
    Write-Host ""

    Download-File $runtimeUrl $zipPath "whisper.cpp Windows x64 runtime" 1000000

    Write-Host ""
    Write-Host "Verifying runtime checksum..."
    $actualHash = (Get-FileHash $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualHash -ne $runtimeSha256) {
        Remove-Item $zipPath -Force -ErrorAction SilentlyContinue
        throw "Runtime checksum mismatch. Expected $runtimeSha256 but got $actualHash."
    }
    Write-Host "Runtime checksum OK."

    Write-Host ""
    Write-Host "Extracting whisper.cpp runtime..."
    Get-ChildItem $runtimeDir -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force
    Expand-Archive -Path $zipPath -DestinationPath $runtimeDir -Force

    $server = Get-ChildItem $runtimeDir -Filter whisper-server.exe -Recurse | Select-Object -First 1
    if ($null -eq $server) { throw "whisper-server.exe was not found after extraction." }
    Write-Host "whisper-server.exe: $($server.FullName)"

    if ($Model -eq "tiny.en" -or $Model -eq "fast" -or $Model -eq "both") {
        Download-File $tinyUrl (Join-Path $modelDir "ggml-tiny.en.bin") "Fast local speech model (~75 MiB)" 60000000
    }

    if ($Model -eq "base.en" -or $Model -eq "fast" -or $Model -eq "both") {
        Download-File $baseUrl (Join-Path $modelDir "ggml-base.en.bin") "Balanced local speech model (~142 MiB)" 100000000
    }

    if ($Model -eq "small.en" -or $Model -eq "both") {
        Download-File $smallUrl (Join-Path $modelDir "ggml-small.en.bin") "Whisper small.en model (~466 MiB)" 400000000
    }

    Download-File $vadUrl (Join-Path $modelDir "ggml-silero-v6.2.0.bin") "Silero VAD model" 500000

    Write-Host ""
    Write-Host "SETUP COMPLETE"
    Write-Host "Local Whisper is ready."
    Write-Host "Next: run BUILD_AND_RUN.bat."
    exit 0
}
catch {
    Write-Host ""
    Write-Host "SETUP FAILED"
    Write-Host "============"
    Write-Host $_.Exception.GetType().FullName
    Write-Host $_.Exception.Message
    Write-Host ""
    Write-Host "This window will remain open so you can send the exact error."
    exit 1
}
