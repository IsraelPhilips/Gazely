param([Parameter(Mandatory=$true)][string]$RunDirectory)
$ErrorActionPreference='Stop'
$rows=@(Import-Csv (Join-Path $RunDirectory 'events.tsv') -Delimiter "`t")
$manifest=Get-Content (Join-Path $RunDirectory 'manifest.json') -Raw|ConvertFrom-Json
$final=@($rows|Where-Object event -eq 'final')
function Distribution($data,[string]$field) {
 $values=@($data|ForEach-Object {[double]::Parse($_.$field,[Globalization.CultureInfo]::InvariantCulture)}|Sort-Object)
 if(!$values.Count){return $null}
 [ordered]@{count=$values.Count;p50=$values[[Math]::Max(0,[Math]::Ceiling($values.Count*.50)-1)];p95=$values[[Math]::Max(0,[Math]::Ceiling($values.Count*.95)-1)];max=$values[-1]}
}
function UnionMs($data) {
 $intervals=@($data|ForEach-Object {[pscustomobject]@{Start=[double]::Parse($_.audio_start_ms,[Globalization.CultureInfo]::InvariantCulture);End=[double]::Parse($_.audio_end_ms,[Globalization.CultureInfo]::InvariantCulture)}}|Sort-Object Start)
 if(!$intervals.Count){return 0}
 $start=$intervals[0].Start;$end=$intervals[0].End;$total=0
 foreach($interval in $intervals|Select-Object -Skip 1){if($interval.Start -gt $end){$total+=$end-$start;$start=$interval.Start;$end=$interval.End}else{$end=[Math]::Max($end,$interval.End)}}
 $total+$end-$start
}
$emitted=@($rows|Where-Object {$_.event -eq 'emitted' -and $_.is_final -eq 'True'})
$summary=[ordered]@{
 mode=$manifest.mode;final_results=$final.Count;detected_results=@($final|Where-Object reference -ne '').Count
 emitted_final_windows=$emitted.Count;dropped_final_windows=@($rows|Where-Object {$_.event -eq 'dropped' -and $_.is_final -eq 'True'}).Count
 superseded_partials=@($rows|Where-Object event -eq 'superseded').Count
 empty_finals=@($rows|Where-Object event -eq 'empty_final').Count;errors=@($rows|Where-Object event -eq 'error').Count
 emitted_final_audio_union_ms=(UnionMs $emitted);transcribed_final_audio_union_ms=(UnionMs $final)
 coverage_note='Union of final window intervals, including pre-roll, overlap and EOF padding; not speech recall. Compare event spans to annotated speech to identify actual loss.'
 inference_ms=(Distribution $final 'inference_ms');queue_ms=(Distribution $final 'queue_ms')
 ready_from_last_voice_ms=(Distribution $final 'ready_from_last_voice_ms')
 latency_note=$manifest.timing_note
 wer=$null;wer_note='Requires a manually corrected reference transcript. Pewbeam text is not treated as ground truth.'
}
$werPath=Join-Path $RunDirectory 'wer-user-reference.json'
if(Test-Path -LiteralPath $werPath) {
 $wer=Get-Content $werPath -Raw|ConvertFrom-Json
 $summary.wer=$wer.wer
 $summary.wer_note='Against the user-supplied approximately 99% accurate reference; see wer-user-reference.json for normalization and provenance. Not independently timestamp-aligned.'
}
$summary|ConvertTo-Json -Depth 5|Set-Content (Join-Path $RunDirectory 'summary.json') -Encoding UTF8
$summary|ConvertTo-Json -Depth 5
