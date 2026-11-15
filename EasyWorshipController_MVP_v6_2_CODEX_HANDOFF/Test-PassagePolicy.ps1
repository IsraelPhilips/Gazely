$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Speech
$source=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'EasyWorshipController.cs'))
$a=$source.IndexOf('    internal sealed class ScriptureDetection')
$b=$source.IndexOf('    internal sealed class UtteranceSegmenter')
$bookRaw=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'BookPronunciationRecovery.cs')) -replace '(?m)^using [^;]+;\r?\n',''
$bookSource=$bookRaw.Substring($bookRaw.IndexOf('{')+1,$bookRaw.LastIndexOf('}')-$bookRaw.IndexOf('{')-1)
Add-Type -TypeDefinition ('using System;using System.Collections.Generic;using System.Text;using System.Text.RegularExpressions;using System.Speech.Synthesis;'+$source.Substring($a,$b-$a).Replace('internal sealed class','public sealed class')+$bookSource) -ReferencedAssemblies ([AppDomain]::CurrentDomain.GetAssemblies()|Where-Object {$_.GetName().Name -eq 'System.Speech'}|Select-Object -First 1).Location
function Quote([long]$start,[long]$end) {
 $d=New-Object ScriptureDetection
 $d.Book='Matthew';$d.Chapter=6;$d.Verse=33;$d.EvidenceKind='quotation';$d.RequiresReview=$true
 $d.MatchScore=.98;$d.LexicalCoverage=.95;$d.CompetitionMargin=.12;$d.InformativeWords=8
 $d.EvidenceStartSample=$start;$d.EvidenceEndSample=$end
 return $d
}
$policy=New-Object PassageSelectionPolicy
$anchor=(New-Object ScriptureDetector).Feed('Genesis 49:3')
$policy.Spoken($anchor,0)
$q=Quote 16000 32000
if($policy.Observe($q,$anchor,2,$false,$false)){throw 'Fresh anchor was stolen'}
$q=Quote 16000 32000
if($policy.Observe($q,$anchor,20,$false,$false) -or $q.Corroborated){throw 'Repeated audio manufactured confirmation'}
$q=Quote 24000 48000
if($policy.Observe($q,$anchor,20,$false,$false) -or $q.Corroborated){throw 'Overlapping audio manufactured confirmation'}
$q=Quote 64000 96000
if(!$policy.Observe($q,$anchor,20,$false,$false) -or ![PassageSelectionPolicy]::CanAutoQuote($q)){throw 'Independent evidence did not supersede stale anchor'}
if($policy.Observe($q,$anchor,25,$true,$true)){throw 'Operator hold lost'}
$q.CompetitionMargin=.01
if([PassageSelectionPolicy]::CanAutoQuote($q)){throw 'Parallel wording allowed live'}
$q.CompetitionMargin=.12;$q.ContextOnly=$true
if([PassageSelectionPolicy]::CanAutoQuote($q)){throw 'Old context allowed live'}
$q.ContextOnly=$false;$q.EvidenceKind='paraphrase'
if([PassageSelectionPolicy]::CanAutoQuote($q)){throw 'Semantic score allowed live'}
$q.EvidenceKind='quotation';$q.InformativeWords=3
if([PassageSelectionPolicy]::CanAutoQuote($q)){throw 'Short ambiguous quote allowed live'}
$policy.Reset();$anchor.Book='Matthew';$anchor.Chapter=0;$anchor.Verse=0;$policy.Spoken($anchor,0)
$q=Quote 0 16000
if($policy.Observe($q,$anchor,20,$true,$false)){throw 'Quote silently supplied missing explicit numbers'}
Write-Output 'PASS: fresh/stale anchors, advancing/non-overlapping evidence, operator hold, ambiguity, semantic-only/short quotes and incomplete reference protection.'
