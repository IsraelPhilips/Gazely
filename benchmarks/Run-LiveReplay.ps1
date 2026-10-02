param(
 [Parameter(Mandatory=$true)][string]$Audio,
 [ValidateSet('realtime','accelerated')][string]$Mode='realtime',
 [ValidateSet('base.en','tiny.en','small.en')][string]$Model='base.en',
 [ValidateRange(0,86400)][int]$LimitSeconds=0,
 [string]$OutputDirectory,
 [switch]$SaveChunks,
 [switch]$NeuralVad,
 [ValidateRange(0,3000)][int]$EndpointMs=0,
 [ValidateRange(0,30000)][int]$WindowMs=0,
 [switch]$NoiseCheck,
 [switch]$BuildOnly
)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$app=Join-Path $root 'EasyWorshipController_MVP_v6_2_CODEX_HANDOFF'
$sourcePath=Join-Path $app 'EasyWorshipController.cs'
$source=Get-Content $sourcePath -Raw -Encoding UTF8
function Slice([string]$from,[string]$to) {
 $start=$source.IndexOf($from); $end=$source.IndexOf($to,$start)
 if($start -lt 0 -or $end -le $start){throw "Production source boundary changed: $from"}
 $source.Substring($start,$end-$start)
}
$core=Slice '    internal sealed class ScriptureDetection' '    internal sealed class AudioLevelMeter'
$worker=Slice '        private void OnSpeechChunk(SpeechChunk chunk)' '        private void OnPartialTranscript('
# Instrumentation only: queue policy, reuse, inference and backoff remain production code.
foreach($marker in @('long started = Stopwatch.GetTimestamp();','return !queued.IsFinal || queued.Session != chunk.Session;','if (!chunk.IsFinal && whisperQueue.Count > 0) return;','                    whisperQueue.RemoveAt(0);','AddLog("No speech text returned for utterance " + chunk.UtteranceId);')) {
 if(([regex]::Matches($worker,[regex]::Escape($marker))).Count -ne 1){throw "Replay instrumentation requires review after source change: $marker"}
}
$worker=$worker.Replace('long started = Stopwatch.GetTimestamp();','long started = Stopwatch.GetTimestamp(); TraceStarted(chunk, started);')
$worker=$worker.Replace('return !queued.IsFinal || queued.Session != chunk.Session;', 'return TraceSuperseded(queued, !queued.IsFinal || queued.Session != chunk.Session);')
$worker=$worker.Replace('if (!chunk.IsFinal && whisperQueue.Count > 0) return;', 'if (!chunk.IsFinal && whisperQueue.Count > 0) { TraceDrop(chunk); return; }')
$worker=$worker.Replace('AddLog("No speech text returned for utterance " + chunk.UtteranceId);', 'Event("empty_final",chunk,"","",inferenceMs);')
$worker=$worker.Replace('                    whisperQueue.RemoveAt(0);', '                    TraceDrop(whisperQueue[0]); whisperQueue.RemoveAt(0);')
$harness=(Get-Content (Join-Path $PSScriptRoot 'ReplayHarness.cs') -Raw).Replace('// PRODUCTION_WORKER_METHODS',$worker)
$code='using System; using System.Collections.Generic; using System.Drawing; using System.Diagnostics; using System.IO; using System.Net; using System.Net.Sockets; using System.Text; using System.Text.RegularExpressions; using System.Threading; using System.Windows.Forms; '+$core+$harness
$refs=@()
foreach($name in @('System','System.Core','System.Drawing','System.Windows.Forms')) {
 Add-Type -AssemblyName $name
 $refs+=([AppDomain]::CurrentDomain.GetAssemblies()|Where-Object {$_.GetName().Name -eq $name}|Select-Object -First 1).Location
}
# Unique harness beside existing dependencies; never replace the application executable.
$exe=Join-Path $app ('Replay-'+[Guid]::NewGuid().ToString('N')+'.exe')
try {
 Add-Type -TypeDefinition $code -ReferencedAssemblies $refs -OutputAssembly $exe -OutputType ConsoleApplication
 if($BuildOnly){Write-Output 'Replay harness compiled; no audio, microphone or adapter opened.';return}
 if($NoiseCheck){
  if(!$OutputDirectory){throw 'NoiseCheck needs a new OutputDirectory.'}
  if(Test-Path -LiteralPath $OutputDirectory){throw 'Choose a new output directory.'}
  New-Item -ItemType Directory -Path $OutputDirectory -Force|Out-Null
  & $exe --noise-check ([IO.Path]::GetFullPath((Join-Path $OutputDirectory 'synthetic-noise.tsv')))
  if($LASTEXITCODE -ne 0){throw 'Synthetic noise test failed; see retained report.'}
  return
 }
 $audioPath=(Resolve-Path -LiteralPath $Audio).Path
 if(!$OutputDirectory){$OutputDirectory=Join-Path $PSScriptRoot ('runs/'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+$Mode)}
 $output=[IO.Path]::GetFullPath($OutputDirectory)
 if(Test-Path -LiteralPath $output){throw 'Choose a new output directory to preserve earlier evidence.'}
 New-Item -ItemType Directory -Path $output -Force | Out-Null
 $manifest=[ordered]@{mode=$Mode;model=$Model;audio=$audioPath;audio_sha256=(Get-FileHash $audioPath).Hash;source_sha256=(Get-FileHash $sourcePath).Hash;frame_ms=40;eof_silence_ms=600;limit_seconds=$LimitSeconds;save_chunks=[bool]$SaveChunks;presentation='excluded from executable';prompt='production SpeechVocabulary.Prompt';timing_note='Accelerated mode drains after each frame; not live latency or realistic queue pressure. Parser context expiry uses wall time. Realtime includes queue pressure but excludes WinForms UI dispatch and presentation.'}
 $manifest['neural_vad']=[bool]$NeuralVad
 $manifest['endpoint_override_ms']=$EndpointMs
 $manifest['window_override_ms']=$WindowMs
 $manifest|ConvertTo-Json|Set-Content (Join-Path $output 'manifest.json') -Encoding UTF8
 $modelPath=Join-Path $app ('models/ggml-'+$Model+'.bin')
 if($Model -eq 'small.en' -and !(Test-Path -LiteralPath $modelPath)){$modelPath=Join-Path $app 'models/ggml-small.en-q5_1.bin'}
 [ordered]@{harness_sha256=(Get-FileHash (Join-Path $PSScriptRoot 'ReplayHarness.cs')).Hash;runner_sha256=(Get-FileHash $PSCommandPath).Hash;model_path=$modelPath;model_sha256=(Get-FileHash $modelPath).Hash;logical_processors=[Environment]::ProcessorCount;os=[Environment]::OSVersion.VersionString}|ConvertTo-Json|Set-Content (Join-Path $output 'provenance.json') -Encoding UTF8
 & $exe $audioPath $output $Mode $Model $LimitSeconds ([bool]$SaveChunks).ToString() ([bool]$NeuralVad).ToString() $EndpointMs $WindowMs
 if($LASTEXITCODE -ne 0){throw "Replay failed ($LASTEXITCODE); inspect retained evidence."}
 $rows=@(Import-Csv (Join-Path $output 'events.tsv') -Delimiter "`t")
 $final=@($rows|Where-Object event -eq 'final')
 $final|Select-Object audio_start_ms,audio_end_ms,text,reference|Export-Csv (Join-Path $output 'transcript.csv') -NoTypeInformation -Encoding UTF8
 & (Join-Path $PSScriptRoot 'Summarize-Replay.ps1') -RunDirectory $output
 Write-Output "Evidence: $output"
} finally { if(Test-Path -LiteralPath $exe){Remove-Item -LiteralPath $exe} }
