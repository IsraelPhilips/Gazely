$ErrorActionPreference='Stop'
# Evaluation only: no speech-profile/default change. Official MIT model card:
# https://huggingface.co/distil-whisper/distil-small.en
$revision='9e4a67ca4569c30be43a3fe7fba1621e504f0093'
$sha='7691eb11167ab7aaf6b3e05d8266f2fd9ad89c550e433f86ac266ebdee6c970a'
$bytes=336191657
$directory=Join-Path (Split-Path $PSScriptRoot -Parent) 'EasyWorshipController_MVP_v6_2_CODEX_HANDOFF/models'
$target=Join-Path $directory 'ggml-distil-small.en.bin'
if(Test-Path -LiteralPath $target){if((Get-FileHash -LiteralPath $target).Hash.ToLowerInvariant() -ne $sha){throw 'Existing candidate checksum differs; preserve and investigate.'};Write-Output 'Verified candidate already present.';return}
$partial=$target+'.partial'
if((Test-Path -LiteralPath $partial) -and (Get-Item -LiteralPath $partial).Length -gt $bytes){throw 'Partial artifact exceeds expected size.'}
$url="https://huggingface.co/distil-whisper/distil-small.en/resolve/$revision/ggml-distil-small.en.bin"
& curl.exe --fail --location --silent --show-error --retry 2 --continue-at - --connect-timeout 30 --max-time 1800 --output $partial $url
if($LASTEXITCODE -ne 0){throw 'Download failed; incomplete file retained separately.'}
if((Get-Item -LiteralPath $partial).Length -ne $bytes -or (Get-FileHash -LiteralPath $partial).Hash.ToLowerInvariant() -ne $sha){throw 'Candidate size/checksum failed; production files unchanged.'}
Move-Item -LiteralPath $partial -Destination $target
[ordered]@{publisher='distil-whisper';model='distil-small.en';revision=$revision;license='MIT';artifact_bytes=$bytes;sha256=$sha;purpose='evaluation only; not selected by production UI';base_plus_distil_plus_vad_bytes=485040966}|ConvertTo-Json|Set-Content (Join-Path $directory 'distil-small.en-provenance.json') -Encoding UTF8
Write-Output 'Verified 336.2 MB candidate downloaded. No production model setting changed.'
