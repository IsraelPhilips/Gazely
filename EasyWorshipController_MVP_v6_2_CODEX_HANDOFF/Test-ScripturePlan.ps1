$ErrorActionPreference='Stop'
$plan=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'ScripturePlan.cs')) -replace '(?m)^using [^;]+;\r?\n',''
$plan=$plan -replace 'internal sealed class','public sealed class' -replace 'internal static class','public static class' -replace 'internal enum','public enum'
Add-Type -TypeDefinition ('using System;using System.Collections.Generic;using System.Text;using System.Text.RegularExpressions;'+$plan)

$checks=0
function Plan { return New-Object EasyWorshipControllerMvp.ScripturePlan }
function Ord([string]$n){ return [EasyWorshipControllerMvp.PlanOrdinal]::$n }

# --- Declared count -------------------------------------------------------
$countCases=@(
  @('we would be reading three scriptures today',3),
  @('we are going to look at 2 passages',2),
  @('let us examine four texts',4),
  @('we will be studying a couple of scriptures',2),
  @('i want to look at one scripture',1),
  @('turn with me to Genesis',0),
  @('we are reading the bible',0),
  @('that was a good passage',0)
)
foreach($case in $countCases){
  $got=[EasyWorshipControllerMvp.PlanDirectives]::DeclaredCount($case[0])
  if($got -ne $case[1]){throw ('DeclaredCount(' + $case[0] + ') = ' + $got + ', expected ' + $case[1])}
}
$checks++

# --- Activation vs announcement ------------------------------------------
$activation=@(
  'let us start from Hebrews ten two',
  'turn to Micah four one',
  'go to Isaiah sixty',
  'open to Romans eight'
)
foreach($text in $activation){
  if(-not [EasyWorshipControllerMvp.PlanDirectives]::IsActivation($text)){throw ('Not recognised as activation: ' + $text)}
}
$notActivation=@(
  'we are reading three scriptures',
  'Psalms one three and Hebrews ten two',
  'that is a wonderful passage'
)
foreach($text in $notActivation){
  if([EasyWorshipControllerMvp.PlanDirectives]::IsActivation($text)){throw ('Wrongly treated as activation: ' + $text)}
}
$checks++

# --- The reported scenario ------------------------------------------------
$p=Plan
[void]$p.Add('Psalms',1,3,0,$false)
[void]$p.Add('Hebrews',10,2,0,$false)
[void]$p.Add('Deuteronomy',28,1,0,$false)
if($p.Count -ne 3){throw ('Plan should hold three scriptures, holds ' + $p.Count)}
[void]$p.Activate('Hebrews',10,2)
if($p.Active.Book -ne 'Hebrews'){throw 'Activation did not select Hebrews'}

$nextInPsalms=$p.Resolve((Ord 'Next'),'Psalms')
if($null -eq $nextInPsalms -or $nextInPsalms.Book -ne 'Psalms'){throw 'Next-in-Psalms did not resolve to Psalms'}

$last=$p.Resolve((Ord 'Last'),'')
if($null -eq $last -or $last.Book -ne 'Deuteronomy'){throw 'The final scripture did not resolve to Deuteronomy'}

$firstCalled=$p.Resolve((Ord 'First'),'')
if($null -eq $firstCalled -or $firstCalled.Book -ne 'Psalms'){throw 'The first called did not resolve to Psalms'}
$checks++

# --- Ambiguity abstains ---------------------------------------------------
$two=Plan
[void]$two.Add('Psalms',1,1,0,$false)
[void]$two.Add('Psalms',23,1,0,$false)
[void]$two.Add('Micah',4,1,0,$false)
[void]$two.Activate('Psalms',1,1)
$ambiguous=$two.Resolve((Ord 'Next'),'Psalms')
if($null -ne $ambiguous){throw ('An ambiguous ordinal resolved to ' + $ambiguous.Book + ' ' + $ambiguous.Chapter)}
$unambiguous=$two.Resolve((Ord 'Next'),'Micah')
if($null -eq $unambiguous -or $unambiguous.Book -ne 'Micah'){throw 'An unambiguous book-constrained ordinal failed'}
if($null -ne $two.Resolve((Ord 'Next'),'Titus')){throw 'A book absent from the plan resolved to something'}
$checks++

# --- Out of range ---------------------------------------------------------
$one=Plan
[void]$one.Add('Micah',4,1,0,$false)
if($null -ne $one.Resolve((Ord 'Third'),'')){throw 'A third scripture resolved when only one exists'}
if($null -ne $one.Resolve((Ord 'Second'),'')){throw 'A second scripture resolved when only one exists'}
$checks++

# --- Deduplication keeps one slot -----------------------------------------
$dup=Plan
[void]$dup.Add('Micah',4,1,0,$false)
[void]$dup.Add('micah',4,1,0,$false)
if($dup.Count -ne 1){throw ('A repeated reference consumed a second slot: ' + $dup.Count)}
$checks++

# --- Presented state ------------------------------------------------------
$done=Plan
[void]$done.Add('Psalms',1,3,0,$false)
[void]$done.Add('Micah',4,1,0,$true)
if(-not $done.Items[1].Presented){throw 'A presented scripture was not marked'}
if($done.Items[0].Presented){throw 'An unread scripture was marked presented'}
$checks++

# --- Deictic reference parsing -------------------------------------------
$refCases=@(
  @('so now let us go to the next verse i called in Psalms','Next'),
  @('the final scripture','Last'),
  @('the last passage','Last'),
  @('the first one i called','First'),
  @('the second scripture','Second'),
  @('now the third text','Third')
)
foreach($case in $refCases){
  $ordinal=[EasyWorshipControllerMvp.PlanOrdinal]::None
  $book=''
  $ok=[EasyWorshipControllerMvp.PlanDirectives]::TryResolveReference($case[0],[ref]$ordinal,[ref]$book)
  if(-not $ok){throw ('No ordinal found in: ' + $case[0])}
  if($ordinal.ToString() -ne $case[1]){throw ('Ordinal for ' + $case[0] + ' was ' + $ordinal + ', expected ' + $case[1])}
}
$checks++

# A plain reference is not a deictic instruction.
foreach($text in @('Genesis chapter one verse one','let us read Hebrews ten two','turn to Micah four one')){
  $ordinal=[EasyWorshipControllerMvp.PlanOrdinal]::None
  $book=''
  if([EasyWorshipControllerMvp.PlanDirectives]::TryResolveReference($text,[ref]$ordinal,[ref]$book)){throw ('A plain reference was treated as deictic: ' + $text)}
}
$checks++

# --- Quote promotion ------------------------------------------------------
foreach($text in @('give me that scripture','show me that verse','what is that passage','pull up that text','find that one')){
  if(-not [EasyWorshipControllerMvp.PlanDirectives]::IsQuotePromotion($text)){throw ('Not a promotion request: ' + $text)}
}
foreach($text in @('Genesis chapter one','give me strength','that was good','next verse')){
  if([EasyWorshipControllerMvp.PlanDirectives]::IsQuotePromotion($text)){throw ('Wrongly a promotion request: ' + $text)}
}
$checks++

# --- Summary and reset ----------------------------------------------------
$summary=$p.Summary()
if($summary -notmatch '^3 announced'){throw ('Unexpected summary: ' + $summary)}
if($summary -notmatch 'Heb\. 10:2'){throw ('Summary omits an announced reference: ' + $summary)}
$p.Reset()
if($p.Count -ne 0 -or -not $p.Empty -or $null -ne $p.Active){throw 'Reset left plan state behind'}
$checks++

Write-Output ('PASS: ' + $checks + ' plan groups; the reported three-scripture scenario resolves, ambiguities abstain.')
