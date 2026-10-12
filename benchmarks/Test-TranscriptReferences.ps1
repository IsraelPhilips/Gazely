param([Parameter(Mandatory=$true)][string]$Transcript,[Parameter(Mandatory=$true)][string]$Output,[switch]$Events)
$ErrorActionPreference='Stop'
if(Test-Path -LiteralPath $Output){throw 'Use a new output path to preserve evidence.'}
$app=Join-Path (Split-Path $PSScriptRoot -Parent) 'EasyWorshipController_MVP_v6_2_CODEX_HANDOFF'
$source=Get-Content (Join-Path $app 'EasyWorshipController.cs') -Raw -Encoding UTF8
$start=$source.IndexOf('    internal sealed class ScriptureDetection')
$end=$source.IndexOf('    internal sealed class UtteranceSegmenter')
if($start -lt 0 -or $end -le $start){throw 'Production parser boundary changed.'}
$parser='using System; using System.Collections.Generic; using System.Text; using System.Text.RegularExpressions; '+$source.Substring($start,$end-$start).Replace('internal sealed class','public sealed class')
Add-Type -TypeDefinition $parser
$detector=New-Object ScriptureDetector
$active=New-Object ActiveReferenceState
$eventIndex=0
# Diagnostic excludes UI dispatch, quote retrieval and wall-clock expiry.
# Events includes partial anchoring; CSV inputs are final-only. Neither is end-to-end accuracy.
$inputRows=if($Events){Import-Csv -LiteralPath $Transcript -Delimiter "`t"}else{Import-Csv -LiteralPath $Transcript}
$rows=foreach($row in $inputRows){
 $detector.SeedActiveReference($active.Context)
 $eventIndex++
 if($Events -and $row.event -eq 'partial'){
  $preview=$detector.Preview($row.text,$true)
  $active.Preview($preview)
  [pscustomobject]@{event='partial';audio_start_ms=$row.audio_start_ms;audio_end_ms=$row.audio_end_ms;text=$row.text;reference=$(if($preview){$preview.ReferenceText}else{''});kind=$(if($preview){$preview.EvidenceKind}else{''});active_reference=$(if($active.Current){$active.Current.ReferenceText}else{''});active_review_required=$(if($active.Current){$active.Current.RequiresReview}else{$null});active_evidence=$(if($active.Current){$active.Current.EvidenceKind}else{''})}
  continue
 }
 $result=$detector.FeedFinal($row.text,"replay:$eventIndex")
 $active.Confirm($result)
 if(!$result -and $detector.UnresolvedSource -and $detector.UnresolvedSource.EndsWith($row.text) -and $detector.UnresolvedBook){
  $draft=New-Object ScriptureDetection
  $draft.Book=$detector.UnresolvedBook;$draft.Chapter=$detector.UnresolvedChapter
  $draft.SourceText=$row.text;$draft.EvidenceKind='draft';$draft.RequiresReview=$true
  $active.Confirm($draft)
 }
 [pscustomobject]@{event='final';audio_start_ms=$row.audio_start_ms;audio_end_ms=$row.audio_end_ms;text=$row.text;reference=$(if($result){$result.ReferenceText}else{''});kind=$(if($result){$result.EvidenceKind}else{''});active_reference=$(if($active.Current){$active.Current.ReferenceText}else{''});active_review_required=$(if($active.Current){$active.Current.RequiresReview}else{$null});active_evidence=$(if($active.Current){$active.Current.EvidenceKind}else{''})}
}
$rows | Export-Csv -LiteralPath $Output -NoTypeInformation -Encoding UTF8
$rows | Where-Object reference | Select-Object text,reference,kind | Format-Table -AutoSize
