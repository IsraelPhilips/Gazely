param([switch]$SkipIndex)
$ErrorActionPreference='Stop'
[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12
$directory=Join-Path $PSScriptRoot 'models/scripture-search'
New-Item -ItemType Directory -Path $directory -Force | Out-Null
# Only these compact model artifacts are allowed. No repository-wide downloads.
$files=@(
 @('Xenova/bge-small-en-v1.5','ea104dacec62c0de699686887e3f920caeb4f3e3','onnx/model_quantized.onnx','embedding.onnx'),
 @('Xenova/bge-small-en-v1.5','ea104dacec62c0de699686887e3f920caeb4f3e3','vocab.txt','vocab.txt'),
 @('cross-encoder/ms-marco-MiniLM-L6-v2','233902d25c440f23af6f7d6e94d2946bac0bee0a','onnx/model_quint8_avx2.onnx','reranker.onnx'),
 @('cross-encoder/ms-marco-MiniLM-L6-v2','233902d25c440f23af6f7d6e94d2946bac0bee0a','vocab.txt','reranker-vocab.txt')
)
$expected=@{
 'embedding.onnx'='6C9C6101A956D62DFB5E7190C538226C0C5BB9CB27B651234B6DF063EE7DBFE4'
 'reranker.onnx'='C80A8B34256EA453093D612E3AC48D3D965A0C0A48C7906709AF8B8E28461BF9'
 'vocab.txt'='07ECED375CEC144D27C900241F3E339478DEC958F92FDDBC551F295C992038A3'
 'reranker-vocab.txt'='07ECED375CEC144D27C900241F3E339478DEC958F92FDDBC551F295C992038A3'
}
$manifest=@()
foreach($entry in $files){
 $repo=$entry[0];$revision=$entry[1]
 $target=Join-Path $directory $entry[3]
 if(!(Test-Path -LiteralPath $target)){
  $url="https://huggingface.co/$repo/resolve/$revision/$($entry[2])"
  $head=Invoke-WebRequest -Method Head -Uri $url -UseBasicParsing
  $length=$head.Headers['Content-Length'];if($length -and [long]$length -gt 100MB){throw 'Compact model size limit exceeded'}
  Write-Host "Downloading compact search artifact: $($entry[3])"
  Invoke-WebRequest -Uri $url -OutFile ($target+'.partial') -UseBasicParsing
  if((Get-Item -LiteralPath ($target+'.partial')).Length -gt 100MB){throw 'Compact model size limit exceeded'}
  Move-Item -LiteralPath ($target+'.partial') -Destination $target
 }
 $hash=(Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
 if($hash -ne $expected[$entry[3]]){throw "Search artifact checksum mismatch: $($entry[3])"}
 $manifest+= [pscustomobject]@{repository=$repo;revision=$revision;file=$entry[2];local=$entry[3];bytes=(Get-Item -LiteralPath $target).Length;sha256=$hash}
}
$manifest | ConvertTo-Json | Set-Content (Join-Path $directory 'manifest.json') -Encoding UTF8
Copy-Item -Path (Join-Path $PSScriptRoot 'SemanticSearch/third-party/*') -Destination $directory -Force
$env:DOTNET_CLI_HOME=Join-Path $PSScriptRoot 'SemanticSearch/.dotnet'
$env:NUGET_PACKAGES=Join-Path $PSScriptRoot 'SemanticSearch/.packages'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
dotnet publish (Join-Path $PSScriptRoot 'SemanticSearch/Gazely.Search.csproj') -c Release -r win-x64 --self-contained true -o (Join-Path $PSScriptRoot 'search-runtime')
if($LASTEXITCODE -ne 0){throw 'Search worker build failed'}
$runtimeConfig=Get-Content (Join-Path $PSScriptRoot 'search-runtime/Gazely.Search.runtimeconfig.json') -Raw | ConvertFrom-Json
$runtimeVersion=($runtimeConfig.runtimeOptions.includedFrameworks | Where-Object name -eq 'Microsoft.NETCore.App').version
$runtimePackage=Join-Path $env:NUGET_PACKAGES ("microsoft.netcore.app.runtime.win-x64/"+$runtimeVersion)
Copy-Item -LiteralPath (Join-Path $runtimePackage 'LICENSE.TXT') -Destination (Join-Path $PSScriptRoot 'search-runtime/DOTNET-LICENSE.txt') -Force
Copy-Item -LiteralPath (Join-Path $runtimePackage 'THIRD-PARTY-NOTICES.TXT') -Destination (Join-Path $PSScriptRoot 'search-runtime/DOTNET-ThirdPartyNotices.txt') -Force
Copy-Item -LiteralPath (Join-Path $env:NUGET_PACKAGES 'microsoft.ml.onnxruntime/1.20.1/LICENSE') -Destination (Join-Path $PSScriptRoot 'search-runtime/ONNX-LICENSE.txt') -Force
Copy-Item -LiteralPath (Join-Path $env:NUGET_PACKAGES 'microsoft.ml.onnxruntime/1.20.1/ThirdPartyNotices.txt') -Destination (Join-Path $PSScriptRoot 'search-runtime/ONNX-ThirdPartyNotices.txt') -Force
if(!$SkipIndex){
 & (Join-Path $PSScriptRoot 'search-runtime/Gazely.Search.exe') $directory (Join-Path $PSScriptRoot 'corpus/kjv.tsv') --index
 if($LASTEXITCODE -ne 0){throw 'Search index build failed'}
}
