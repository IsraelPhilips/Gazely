$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Speech
$planRaw=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'ScripturePlan.cs')) -replace '(?m)^using [^;]+;\r?\n',''
$plan=$planRaw.Substring($planRaw.IndexOf('{')+1,$planRaw.LastIndexOf('}')-$planRaw.IndexOf('{')-1)
$plan=$plan -replace 'internal sealed class','public sealed class' -replace 'internal static class','public static class' -replace 'internal enum','public enum'

# QuotePromotionPolicy sits above MainForm, so it is sliced separately from the
# ScriptureDetection..UtteranceSegmenter window.
$source=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'EasyWorshipController.cs'))
$q1=$source.IndexOf('    internal static class QuotePromotionPolicy')
$q2=$source.IndexOf('    internal sealed partial class MainForm')
$promo=$source.Substring($q1,$q2-$q1).Replace('internal static class','public static class')

$a=$source.IndexOf('    internal sealed class ScriptureDetection')
$b=$source.IndexOf('    internal sealed class UtteranceSegmenter')
$detection=$source.Substring($a,$b-$a).Replace('internal sealed class','public sealed class')

$bookRaw=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'BookPronunciationRecovery.cs')) -replace '(?m)^using [^;]+;\r?\n',''
$bookSource=$bookRaw.Substring($bookRaw.IndexOf('{')+1,$bookRaw.LastIndexOf('}')-$bookRaw.IndexOf('{')-1)

Add-Type -TypeDefinition ('using System;using System.Collections.Generic;using System.Text;using System.Text.RegularExpressions;using System.Speech.Synthesis;'+$promo+$detection+$plan+$bookSource) -ReferencedAssemblies ([AppDomain]::CurrentDomain.GetAssemblies()|Where-Object {$_.GetName().Name -eq 'System.Speech'}|Select-Object -First 1).Location

$checks=0

# --- Plan lifecycle -------------------------------------------------------
if([PlanLifecycle]::Expired(299)){throw 'A plan expired before the silence window'}
if(-not [PlanLifecycle]::Expired(300)){throw 'A plan survived the silence window'}
if([PlanLifecycle]::Expired(0)){throw 'A plan expired on the first utterance'}
$checks++

# A newly declared count starts a new section only when a plan already exists.
if(-not [PlanLifecycle]::StartsNewSection(3,$true)){throw 'A new declared count did not start a section'}
if([PlanLifecycle]::StartsNewSection(3,$false)){throw 'A first announcement wrongly started a new section'}
if([PlanLifecycle]::StartsNewSection(0,$true)){throw 'A phrase without a count started a new section'}
$checks++

if(-not [PlanLifecycle]::ExceedsCapacity(21)){throw 'Capacity was not enforced above the limit'}
if([PlanLifecycle]::ExceedsCapacity(20)){throw 'Capacity was enforced at the limit'}
$checks++

# --- Quote promotion ------------------------------------------------------
function Quote([string]$kind,[double]$score,[bool]$review){
  $d=New-Object ScriptureDetection
  $d.Book='Matthew';$d.Chapter=6;$d.Verse=33;$d.EvidenceKind=$kind
  $d.MatchScore=$score;$d.RequiresReview=$review
  return $d
}
$now=[DateTime]::UtcNow

# The strongest held quotation wins.
$held=New-Object 'System.Collections.Generic.List[ScriptureDetection]'
$held.Add((Quote 'quotation' 0.90 $true))
$held.Add((Quote 'quotation' 0.97 $true))
$held.Add((Quote 'quotation' 0.93 $true))
$reason=''
$chosen=[QuotePromotionPolicy]::Choose($held,$now,[ref]$reason)
if($null -eq $chosen -or $chosen.MatchScore -ne 0.97){throw 'The highest matched quotation was not promoted'}
$checks++

# A tie is never resolved automatically.
$tie=New-Object 'System.Collections.Generic.List[ScriptureDetection]'
$tie.Add((Quote 'quotation' 0.96 $true))
$tie.Add((Quote 'quotation' 0.955 $true))
$tieReason=''
if($null -ne [QuotePromotionPolicy]::Choose($tie,$now,[ref]$tieReason)){throw 'A tie was resolved automatically'}
if($tieReason -notmatch 'equally'){throw ('A tie did not explain itself: ' + $tieReason)}
$checks++

# Just outside the tie margin does resolve.
$close=New-Object 'System.Collections.Generic.List[ScriptureDetection]'
$close.Add((Quote 'quotation' 0.96 $true))
$close.Add((Quote 'quotation' 0.93 $true))
$closeReason=''
$picked=[QuotePromotionPolicy]::Choose($close,$now,[ref]$closeReason)
if($null -eq $picked -or $picked.MatchScore -ne 0.96){throw 'A clear winner outside the tie margin was not promoted'}
$checks++

# Paraphrases are never promoted by a spoken directive.
$para=New-Object 'System.Collections.Generic.List[ScriptureDetection]'
$para.Add((Quote 'paraphrase' 0.99 $true))
$para.Add((Quote 'quotation' 0.80 $true))
$paraReason=''
$paraPick=[QuotePromotionPolicy]::Choose($para,$now,[ref]$paraReason)
if($null -eq $paraPick -or $paraPick.EvidenceKind -ne 'quotation'){throw 'A paraphrase was promoted over a quotation'}
$checks++

# Nothing held means nothing promoted.
$empty=New-Object 'System.Collections.Generic.List[ScriptureDetection]'
$emptyReason=''
if($null -ne [QuotePromotionPolicy]::Choose($empty,$now,[ref]$emptyReason)){throw 'Something was promoted from an empty set'}
if($emptyReason.Length -eq 0){throw 'An empty set did not explain itself'}
$checks++

# Only paraphrases held: still nothing, since similarity alone is weakest evidence.
$onlyPara=New-Object 'System.Collections.Generic.List[ScriptureDetection]'
$onlyPara.Add((Quote 'paraphrase' 0.99 $true))
$onlyParaReason=''
if($null -ne [QuotePromotionPolicy]::Choose($onlyPara,$now,[ref]$onlyParaReason)){throw 'A paraphrase alone was promoted'}
$checks++

# A single quotation promotes, with no second candidate to tie against.
$single=New-Object 'System.Collections.Generic.List[ScriptureDetection]'
$single.Add((Quote 'quotation' 0.88 $true))
$singleReason=''
if($null -eq [QuotePromotionPolicy]::Choose($single,$now,[ref]$singleReason)){throw 'A single held quotation was not promoted'}
$checks++

# --- The promotion request itself ----------------------------------------
foreach($text in @('give me that scripture','show me that verse','what is that passage','pull up that text','where is that scripture')){
  if(-not [PlanDirectives]::IsQuotePromotion($text)){throw ('Not recognised as a promotion request: ' + $text)}
}
foreach($text in @('we are reading two scriptures','the final scripture','give me strength','next verse')){
  if([PlanDirectives]::IsQuotePromotion($text)){throw ('Wrongly a promotion request: ' + $text)}
}
$checks++

Write-Output ('PASS: ' + $checks + ' lifecycle and promotion groups; ties abstain, paraphrases never promote.')
