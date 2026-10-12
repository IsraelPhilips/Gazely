param([string]$TranscriptCsv = '')
$ErrorActionPreference = 'Stop'
$source = Get-Content (Join-Path $PSScriptRoot 'EasyWorshipController.cs') -Raw -Encoding UTF8
$start = $source.IndexOf('    internal sealed class ScriptureDetection')
$end = $source.IndexOf('    internal sealed class UtteranceSegmenter')
$core = 'using System; using System.Collections.Generic; using System.Text; using System.Text.RegularExpressions; ' + $source.Substring($start, $end-$start).Replace('internal sealed class', 'public sealed class')
Add-Type -TypeDefinition $core
$clock = [Diagnostics.Stopwatch]::StartNew()
$index = New-Object ScriptureQuoteIndex (Join-Path $PSScriptRoot 'corpus/kjv.tsv')
$loadMs = $clock.ElapsedMilliseconds
$speechParser=New-Object ScriptureDetector
$announced=$speechParser.Feed('in Genesis chapter 49 verses 3 and verse 4')
$first=[SpeechPresentationPolicy]::Resolve($announced,$null,$false)
if($announced.VerseEnd -ne 4 -or $first.ReferenceText -ne 'Genesis 49:3' -or ![SpeechPresentationPolicy]::CanAutomate($first)){throw 'Spoken range must start at its first verse without mutating parsed range'}
$next=[SpeechPresentationPolicy]::Resolve($speechParser.Feed('Next verse'),$first,$true)
if($next.ReferenceText -ne 'Genesis 49:4' -or ![SpeechPresentationPolicy]::CanAutomate($next)){throw 'Clear contextual next verse should be eligible'}
$unanchored=[SpeechPresentationPolicy]::Resolve($speechParser.Feed('Next verse'),$null,$false)
if([SpeechPresentationPolicy]::CanAutomate($unanchored)){throw 'Unanchored navigation became automatic'}
foreach($text in @('Genesis one one','John 3:16')){if(![SpeechPresentationPolicy]::CanAutomate([SpeechPresentationPolicy]::Resolve((New-Object ScriptureDetector).Feed($text),$null,$false))){throw 'Clear numeric reference blocked'}}
$quote=$index.Find('Unstable as water thou shalt not excel')
if([SpeechPresentationPolicy]::CanAutomate($quote)){throw 'Uncertain quotation became automatic'}
Write-Output 'PASS: speech ranges start at first verse; anchored clear navigation and numeric references eligible; uncertain matches remain review-only.'
$splitParser=New-Object ScriptureDetector
$null=$splitParser.Feed('in a genesis chapter forty nine')
$splitRange=$splitParser.Feed('verses three and verse four')
$splitFirst=[SpeechPresentationPolicy]::Resolve($splitRange,$null,$false)
if($splitFirst.ReferenceText -ne 'Genesis 49:3' -or !$splitFirst.RequiresReview -or $splitRange.VerseEnd -ne 4){throw 'Split contextual range must start at first verse and retain review requirement'}
$spokenNext=[SpeechPresentationPolicy]::Resolve($splitParser.Feed("that's a problem next verse"),$splitFirst,$true)
if($spokenNext.ReferenceText -ne 'Genesis 49:4' -or [SpeechPresentationPolicy]::CanAutomate($spokenNext)){throw 'Punctuation-free continuation should be selectable, never automatically presented'}
$chapterParser=New-Object ScriptureDetector
if($chapterParser.Feed("The transcription of shoot to you, Isaiah 51, I want to don't forget it.")){throw 'Unknown verse invented'}
if($chapterParser.UnresolvedBook -ne 'Isaiah' -or $chapterParser.UnresolvedChapter -ne 51){throw 'Explicit chapter lost when verse was unclear'}
$chapterCases=@(
 @('Hearken to me ye that follow after righteousness ye that seek the Lord look unto the rock whence ye are hewn','Isaiah 51:1'),
 @('Look unto Abraham your father and unto Sarah that bare you for I called him alone and blessed him and increased him','Isaiah 51:2'),
 @('You should eat your food and play your games at home.',''),
 @('The microphone is broken and we should fix the projector.',''),
 @('God will bless the church and everyone in the community.','')
)
foreach($case in $chapterCases){$match=$index.FindInChapter($case[0],'Isaiah',51);$actual=if($match){$match.ReferenceText}else{''};if($actual -ne $case[1]){throw "Scoped quote expected $($case[1]), got $actual"};if($match -and !$match.RequiresReview){throw 'Scoped quotation became automatic'}}
Write-Output 'PASS: Isaiah 51 draft survives uncertain verse; clear readings identify verses 1 and 2 with review; ordinary speech abstains.'
if($index.GetVerseText('Genesis',49,3,4) -notmatch 'Reuben.*Unstable as water'){throw 'Range preview lost or reordered scripture text'}
if($index.GetVerseText('John',3,16,0) -notmatch 'For God so loved the world'){throw 'Single verse preview is incorrect'}
if($index.GetVerseText('Genesis',999,1,0)){throw 'Invalid reference returned preview text'}
$cases = @(
 @('Unstable as water thou shalt not excel', 'Genesis 49:4'),
 @('Seek ye first the kingdom and all these other things shall be added unto you', 'Matthew 6:33'),
 @('Thou art my firstborn my might and the beginning of my strength', 'Genesis 49:3'),
 @('Thy word is a lamp unto my feet and a light unto my path', 'Psalms 119:105'),
 @('For all have sinned and come short of the glory of God', 'Romans 3:23'),
 @('Trust in the Lord with all thine heart and lean not unto thine own understanding', 'Proverbs 3:5'),
 @('Put on the whole armour of God that ye may be able to stand against the wiles of the devil', 'Ephesians 6:11'),
 @('The Lord is my shepherd I shall not want', 'Psalms 23:1'),
 @('We need to fix the projector before the service starts', ''),
 @('God has given us many opportunities to serve people in our community', ''),
 @('Please go to the next slide and turn up the microphone', ''),
 @('You are highly talented and gifted and able to do several things well', ''),
 @('He went to his house and saw his library and all kinds of books there', ''),
 @('The church meeting is next Tuesday and everyone should bring food', ''),
 @('[Music]', ''), @('', ''), @('Isaiah Luke Amen', ''),
 @('Jesus wept', ''), @('Praise the Lord', '')
)
$failures = @()
$times = @()
foreach ($case in $cases) {
 $clock.Restart()
 $result = $index.Find($case[0])
 $times += $clock.Elapsed.TotalMilliseconds
 $actual = if ($null -eq $result) { '' } else { $result.ReferenceText }
 if ($actual -ne $case[1]) { $failures += "'$($case[0])': expected '$($case[1])', got '$actual'" }
 if ($result -and (!$result.RequiresReview -or $result.EvidenceKind -ne 'quotation' -or !$result.MatchedText -or $result.SourceText -ne $case[0])) { throw 'Quotation evidence/review policy failed' }
}
if ($failures.Count) { throw ($failures -join "`n") }
$window = New-Object QuotationContext
$time = [datetime]'2026-01-01'
$null = $window.Append('Unstable as water.', $time)
$joined = $window.Append('Thou shalt not excel.', $time.AddSeconds(3))
if ($index.Find($joined).ReferenceText -ne 'Genesis 49:4') { throw 'Split quotation lost' }
if ($window.Append('ordinary conversation', $time.AddSeconds(30)) -ne 'ordinary conversation') { throw 'Expired quotation context retained' }
$window.Reset()
if ($window.Append('new session', $time) -ne 'new session') { throw 'Quotation context reset failed' }
$null = $window.Append('second', $time.AddSeconds(1))
$null = $window.Append('third', $time.AddSeconds(2))
if ($window.Append('fourth', $time.AddSeconds(3)) -ne 'new session second third fourth') { throw 'Quotation context lost earlier evidence' }
for($i=0;$i -lt 20;$i++){$joined=$window.Append("phrase$i",$time.AddSeconds(4+$i))}
if(($joined -split ' ').Count -gt 12){throw 'Quotation context is unbounded'}
$burst='Unstable as water thou shalt not excel. Seek ye first the kingdom and all these other things shall be added unto you. Thy word is a lamp unto my feet and a light unto my path.'
$found=@($index.FindAll($burst) | ForEach-Object ReferenceText)
foreach($expected in @('Genesis 49:4','Matthew 6:33','Psalms 119:105')){if($expected -notin $found){throw "Multi-quote burst lost $expected"}}
if(@($index.FindAll('Please turn up the microphone and fix the projector before Tuesday')).Count){throw 'Ordinary speech produced quotation candidates'}
Write-Output 'PASS: multiple quotations in one burst retained; negative speech abstains.'
$ten=@($cases[0..7] | ForEach-Object {$_[0]})+@('I can do all things through Christ which strengtheneth me','For God so loved the world that he gave his only begotten Son')
$tenFound=@($index.FindAll(($ten -join '. ')) | ForEach-Object ReferenceText)
foreach($expected in (@($cases[0..7] | ForEach-Object {$_[1]})+@('Philippians 4:13','John 3:16'))){if($expected -notin $tenFound){throw "Ten-quote burst lost $expected"}}
Write-Output 'PASS: ten separate quoted passages survive one transcript burst.'
$parallel=@($index.FindAll('the mountain of the house of the Lord shall be established in the top of the mountains'))
foreach($expected in @('Micah 4:1','Isaiah 2:2')){if($expected -notin @($parallel | ForEach-Object ReferenceText)){throw "Shared quotation hid $expected"}}
foreach($match in $parallel){if(!$match.RequiresReview -or [SpeechPresentationPolicy]::CanAutomate($match)){throw 'Ambiguous parallel quotation became automatic'}}
$correctedParser=New-Object ScriptureDetector
$null=$correctedParser.Feed('Malachi 4:2')
$correction=$correctedParser.Feed('No Micah')
if([SpeechPresentationPolicy]::CanAutomate($correction)){throw 'Book correction became automatic'}
Write-Output 'PASS: parallel Micah/Isaiah wording retained for review without changing single-best or automatic policy.'
$parser=New-Object ScriptureDetector
$mentioned=@($parser.FindMentionedReferences('Psalm 23 verse 1 and first Corinthians chapter 13 verse 4 and John 3 verse 16') | ForEach-Object ReferenceText)
foreach($expected in @('Psalms 23:1','1 Corinthians 13:4','John 3:16')){if($expected -notin $mentioned){throw "Multi-reference call lost $expected"}}
Write-Output 'PASS: multiple explicit references preserve spoken book aliases and numbered books.'
Write-Output ("PASS: {0} quotation cases; {1} verses; load {2} ms; max lookup {3:N1} ms" -f $cases.Count,$index.Count,$loadMs,($times|Measure-Object -Maximum).Maximum)
if ($TranscriptCsv) {
 $context = New-Object QuotationContext
 $detector = New-Object ScriptureDetector
 $lastQuote = ''
 foreach ($row in (Import-Csv $TranscriptCsv)) {
  if ($detector.Feed($row.text)) { $context.Reset(); $lastQuote = ''; continue }
  $combined = $context.Append($row.text,([datetime]'2026-01-01').AddMilliseconds([double]$row.audio_end_ms))
  $result = $index.Find($row.text)
  if (!$result) { $result = $index.Find($combined) }
  if ($result -and $result.ReferenceText -ne $lastQuote) {
   $lastQuote = $result.ReferenceText
   Write-Output ("{0}: {1}" -f $result.ReferenceText,$result.SourceText)
  }
 }
}
