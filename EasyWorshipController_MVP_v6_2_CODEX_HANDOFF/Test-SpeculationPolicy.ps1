$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Speech
$speech=([AppDomain]::CurrentDomain.GetAssemblies()|Where-Object {$_.GetName().Name -eq 'System.Speech'}|Select-Object -First 1).Location
$source=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'EasyWorshipController.cs'))

# ScriptureRequest sits above ScriptureDetection in the file, so it is prepended.
$r1=$source.IndexOf('    internal sealed class ScriptureRequest')
$r2=$source.IndexOf('    internal sealed class VersionEntry')
$request=$source.Substring($r1,$r2-$r1).Replace('internal sealed class','public sealed class')

# SpeculationPolicy sits inside this range, between ScriptureDetection and
# UtteranceSegmenter, and only needs ScriptureDetection, ActiveReferenceState and
# ScriptureRequest to compile.
# SpeculationPolicy lives above MainForm, outside the ScriptureDetection..
# UtteranceSegmenter window the other harnesses use, so it is sliced separately
# (moving the class changed which slice contains it; keep both slices explicit).
$a=$source.IndexOf('    internal sealed class ScriptureDetection')
$b=$source.IndexOf('    internal sealed class UtteranceSegmenter')
$body=$source.Substring($a,$b-$a)
$s1=$source.IndexOf('    internal sealed class SpeculationPolicy')
$s2=$source.IndexOf('    internal sealed partial class MainForm')
$policy=$source.Substring($s1,$s2-$s1)

# The sliced code is bare, while BookPronunciationRecovery.cs carries its own
# namespace block. Both are emitted inside one namespace so they can see each
# other; this script refers to the types fully qualified.
$bookSource=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'BookPronunciationRecovery.cs')) -replace '(?m)^using [^;]+;\r?\n',''
Add-Type -TypeDefinition ('using System;using System.Collections.Generic;using System.Text;using System.Text.RegularExpressions;using System.Speech.Synthesis;namespace EasyWorshipControllerMvp {'+$request+$body.Replace('internal sealed class','public sealed class')+$policy.Replace('internal sealed class','public sealed class')+'}'+$bookSource) -ReferencedAssemblies $speech

function Det([string]$book,[int]$chapter,[int]$verse) {
 $d=New-Object EasyWorshipControllerMvp.ScriptureDetection
 $d.Book=$book;$d.Chapter=$chapter;$d.Verse=$verse
 $d.EvidenceKind='explicit';$d.RequiresReview=$false
 return $d
}
function Mem { return New-Object 'EasyWorshipControllerMvp.SpeculationPolicy+Memory' }

$state=New-Object EasyWorshipControllerMvp.ActiveReferenceState
$checks=0

# 1. A complete explicit reference is pre-filled as called.
$det=Det 'Hebrews' 5 2
$m=Mem
$d=[EasyWorshipControllerMvp.SpeculationPolicy]::Plan($state,$det,$m,$false,$true,$false,$false,$false,99.0)
if(-not $d.Prepare -or $d.Target.ReferenceText -ne 'Hebrews 5:2'){throw 'A complete explicit reference should be speculated as called'}
$checks++

# 2. The same reference is not prepared twice.
$m.NotePrepared($d.Target)
if([EasyWorshipControllerMvp.SpeculationPolicy]::Plan($state,$det,$m,$false,$true,$false,$false,$false,99.0).Prepare){throw 'The same reference was prepared twice'}
$checks++

# 3. A chapter-only call pre-fills verse 1, never a guessed verse.
$chapterOnly=Det 'Isaiah' 60 0
$c=[EasyWorshipControllerMvp.SpeculationPolicy]::Plan($state,$chapterOnly,(Mem),$false,$true,$false,$false,$false,99.0)
if(-not $c.Prepare -or $c.Target.Verse -ne 1){throw 'A chapter-only call should pre-fill verse 1'}
$checks++

# 4. Quotation and paraphrase evidence is never speculated.
$quote=Det 'Isaiah' 60 1
$quote.EvidenceKind='quotation';$quote.RequiresReview=$true
if([EasyWorshipControllerMvp.SpeculationPolicy]::Plan($state,$quote,(Mem),$false,$true,$false,$false,$false,99.0).Prepare){throw 'Quotation evidence was speculated'}
$quote.EvidenceKind='paraphrase'
if([EasyWorshipControllerMvp.SpeculationPolicy]::Plan($state,$quote,(Mem),$false,$true,$false,$false,$false,99.0).Prepare){throw 'Paraphrase evidence was speculated'}
$checks++

# 5. An operator action always outranks the machine.
if([EasyWorshipControllerMvp.SpeculationPolicy]::Plan($state,$det,(Mem),$true,$true,$false,$false,$false,99.0).Prepare){throw 'Operator input did not stop speculation'}
$checks++

# 6. Nothing is speculated while not listening.
if([EasyWorshipControllerMvp.SpeculationPolicy]::Plan($state,$det,(Mem),$false,$false,$false,$false,$false,99.0).Prepare){throw 'Speculation ran while not listening'}
$checks++

# 7. Nothing is speculated while a send is in flight.
if([EasyWorshipControllerMvp.SpeculationPolicy]::Plan($state,$det,(Mem),$false,$true,$true,$false,$false,99.0).Prepare){throw 'Speculation ran while a send was in flight'}
$checks++

# 8. The reference about to go live is not pre-filled by the machine.
if([EasyWorshipControllerMvp.SpeculationPolicy]::Plan($state,$det,(Mem),$false,$true,$false,$true,$true,99.0).Prepare){throw 'Speculation fought the send about to happen'}
$checks++

# 9. Recent operator activity in the presentation application stands us down.
if([EasyWorshipControllerMvp.SpeculationPolicy]::Plan($state,$det,(Mem),$false,$true,$false,$false,$false,1.0).Prepare){throw 'Speculation ignored recent operator activity in the target'}
if(-not [EasyWorshipControllerMvp.SpeculationPolicy]::Plan($state,$det,(Mem),$false,$true,$false,$false,$false,5.0).Prepare){throw 'Speculation did not resume once the operator stopped'}
$checks++

# 10. A failed preparation is not retried for the same reference.
$failed=Mem;$failed.FailedKey='Hebrews 5'
if([EasyWorshipControllerMvp.SpeculationPolicy]::Plan($state,$det,$failed,$false,$true,$false,$false,$false,99.0).Prepare){throw 'A failed preparation was retried'}
$checks++

# 11. A reference that has already gone live is never pre-filled again.
$live=Mem
$liveRequest=New-Object EasyWorshipControllerMvp.ScriptureRequest
$liveRequest.Book='Hebrews';$liveRequest.Chapter=5;$liveRequest.Verse=2
$live.NoteLive($liveRequest)
if([EasyWorshipControllerMvp.SpeculationPolicy]::Plan($state,$det,$live,$false,$true,$false,$false,$false,99.0).Prepare){throw 'An already presented reference was pre-filled'}
$checks++

# 12. A confirmed later verse of the same chapter outranks an earlier guess.
$state.Confirm((Det 'Hebrews' 5 5))
$keep=[EasyWorshipControllerMvp.SpeculationPolicy]::Plan($state,(Det 'Hebrews' 5 2),(Mem),$false,$true,$false,$false,$false,99.0)
if(-not $keep.Prepare -or $keep.Target.Verse -ne 5){throw 'A confirmed later verse did not outrank the earlier partial guess'}
$checks++

# 13. Review-required evidence is never speculated.
$review=Det 'Hebrews' 5 2;$review.RequiresReview=$true
if([EasyWorshipControllerMvp.SpeculationPolicy]::Plan((New-Object EasyWorshipControllerMvp.ActiveReferenceState),$review,(Mem),$false,$true,$false,$false,$false,99.0).Prepare){throw 'Review-required evidence was speculated'}
$checks++

# 14. Book and chapter comparison, which decides the verse-only fast path.
if(-not [EasyWorshipControllerMvp.SpeculationPolicy]::SameBookChapter('Hebrews 5:1','Hebrews 5:2')){throw 'Same book and chapter should match'}
if(-not [EasyWorshipControllerMvp.SpeculationPolicy]::SameBookChapter('Hebrews 5.1','Hebrews 5:2')){throw 'Separator form should not matter'}
if([EasyWorshipControllerMvp.SpeculationPolicy]::SameBookChapter('Hebrews 5:1','Hebrews 6:1')){throw 'Different chapters must not match'}
if([EasyWorshipControllerMvp.SpeculationPolicy]::SameBookChapter('Hebrews 5:1','Isaiah 5:1')){throw 'Different books must not match'}
if([EasyWorshipControllerMvp.SpeculationPolicy]::SameBookChapter('','Hebrews 5:1')){throw 'An empty readback must not match'}
if([EasyWorshipControllerMvp.SpeculationPolicy]::SameBookChapter('Hebrews','Hebrews 5:2')){throw 'A partial readback without a verse must not match'}
$checks+=6

Write-Output ('PASS: ' + $checks + ' speculation decisions; pre-fill only, never go-live.')
