$ErrorActionPreference='Stop'
# Evaluation assets only. Explicit, revision-pinned files; no model auto-discovery.
$root=Join-Path $PSScriptRoot 'tools/streaming-candidates'
$n='https://huggingface.co/nvidia/nemotron-speech-streaming-en-0.6b/resolve/ebe59e5a817142986528bbbee5dba8db7b38ed50'
$p='https://huggingface.co/eschmidbauer/parakeet-unified-en-0.6b-onnx/resolve/7a16ff98b72e5b0beba6f182a5357c704d977b15'
$items=@(
 @{path='nemotron/nemotron-speech-streaming-en-0.6b.q8_0.gguf';url="$n/nemotron-speech-streaming-en-0.6b.q8_0.gguf";bytes=699872960;sha256='d9a01898d2a611c8764e23a1c2f45e70bbd5a425dc4de93692ac951dd603812d'},
 @{path='parakeet/encoder.int8.onnx';url="$p/onnx_int8/encoder.int8.onnx";bytes=654040125;sha256='8c430baee5a815b4ceee861f5f51b5b44db5836623ab2bfe9d0386e57b3f25ab'},
 @{path='parakeet/decoder_joint.int8.onnx';url="$p/onnx_int8/decoder_joint.int8.onnx";bytes=8995073;sha256='26a9f0ffb682cf2a712ac86e2e11b6da932f9860f70dc996841cba6392010b55'},
 @{path='parakeet/tokenizer.model';url="$p/onnx_int8/tokenizer.model";bytes=251056;sha256='07d4e5a63840a53ab2d4d106d2874768143fb3fbdd47938b3910d2da05bfb0a9'},
 @{path='nemo-runtime.zip';url='https://github.com/NVIDIA/NeMo-Speech.cpp/releases/download/v0.2.0/nemo-speech-0.2.0-windows-x86_64-cpu.zip';bytes=0;sha256='82c80451086e86194aba9af19f800da77cd66d1c28d993d0efceffd6cdb49bb8'}
)
foreach($item in $items){
 $target=Join-Path $root $item.path
 New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
 if(Test-Path -LiteralPath $target){
  if((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() -ne $item.sha256){throw "Existing checksum mismatch: $target"}
  Write-Output "Already verified: $($item.path)";continue
 }
 Write-Output "Downloading: $($item.path)"
 & curl.exe --fail --location --silent --show-error --retry 3 --continue-at - --connect-timeout 30 --max-time 1800 --output ($target+'.partial') $item.url
 if($LASTEXITCODE -ne 0){throw "Download incomplete: $($item.path); partial retained for resume."}
 if($item.bytes -gt 0 -and (Get-Item -LiteralPath ($target+'.partial')).Length -ne $item.bytes){throw 'Size mismatch'}
 if((Get-FileHash -LiteralPath ($target+'.partial') -Algorithm SHA256).Hash.ToLowerInvariant() -ne $item.sha256){throw 'SHA256 mismatch'}
 Move-Item -LiteralPath ($target+'.partial') -Destination $target
 Write-Output "Verified: $($item.path)"
}
$items | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $root 'verified-artifacts.json') -Encoding UTF8
Write-Output 'All pinned speech assets verified. Production settings unchanged.'
