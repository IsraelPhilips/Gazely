$ErrorActionPreference = 'Stop'
$source = Get-Content (Join-Path $PSScriptRoot 'EasyWorshipController.cs') -Raw -Encoding UTF8
$start = $source.IndexOf('    internal sealed class ScriptureDetection')
$end = $source.IndexOf('    internal sealed class UtteranceSegmenter')
$parser = 'using System; using System.Collections.Generic; using System.Text; using System.Text.RegularExpressions; ' + $source.Substring($start, $end - $start).Replace('internal sealed class', 'public sealed class')
Add-Type -TypeDefinition $parser
$count = 0
function Assert-Reference($detector, [string]$phrase, [string]$expected, [string]$translation = '') {
    $result = $detector.Feed($phrase)
    $actual = if ($null -eq $result) { '' } else { $result.ReferenceText }
    if ($actual -ne $expected) { throw "'$phrase': expected '$expected', got '$actual'" }
    if ($translation -and $result.Translation -ne $translation) { throw "'$phrase': expected translation $translation, got $($result.Translation)" }
    $script:count++
}
$cases = @(
    @('Genesis five verse seven.', 'Genesis 5:7'),
    @('Genesis 5:7', 'Genesis 5:7'),
    @('John chapter three verse sixteen', 'John 3:16'),
    @('John three sixteen', 'John 3:16'),
    @('Romans eight twenty eight', 'Romans 8:28'),
    @('First Corinthians thirteen four', '1 Corinthians 13:4'),
    @('1 John 3:16', '1 John 3:16'),
    @('First John chapter three verse sixteen', '1 John 3:16'),
    @('2 John 1:6', '2 John 1:6'),
    @('Third John one four', '3 John 1:4'),
    @('Second Timothy chapter one verse seven', '2 Timothy 1:7'),
    @('Psalm twenty three verse one', 'Psalms 23:1'),
    @('Psalm one hundred and nineteen verse one hundred and five', 'Psalms 119:105'),
    @('Romans eight twenty-eight', 'Romans 8:28'),
    @('John 3:16 in amplified classic', 'John 3:16', 'AMPC'),
    @('John 3:16 in the Message', 'John 3:16', 'MSG'),
    @('John 3:16 in new king james version', 'John 3:16', 'NKJV'),
    @('John came to church today', ''),
    @('John said three things and sixteen people listened', ''),
    @('verse sixteen', ''),
    @('Romans', ''),
    @('Genesis', ''),
    @('[Music]', ''),
    @('[inaudible]', ''),
    @('John 3:16-18', 'John 3:16-18'),
    @('John chapter three verses sixteen to eighteen', 'John 3:16-18')
)
foreach ($case in $cases) {
    Assert-Reference (New-Object ScriptureDetector) $case[0] $case[1] $case[2]
}
$detector = New-Object ScriptureDetector
Assert-Reference $detector 'John 3:16' 'John 3:16'
Assert-Reference $detector 'verse seventeen' 'John 3:17'
Assert-Reference $detector 'We welcome everyone today' ''
$detector.Reset()
Assert-Reference $detector 'verse eighteen' ''
Assert-Reference $detector 'Turn to John chapter three' ''
Assert-Reference $detector 'verse sixteen' 'John 3:16'
Assert-Reference $detector 'Romans chapter eight' ''
Assert-Reference $detector 'verse twenty eight' 'Romans 8:28'
Assert-Reference $detector 'John came to church' ''
Assert-Reference $detector 'verse seventeen' ''
$moreCases = @(
    @('Genesis 1:1', 'Genesis 1:1'),
    @('Genesis one one', 'Genesis 1:1'),
    @('Genesis 1-1', 'Genesis 1:1'),
    @('Genesis 1.1', 'Genesis 1:1'),
    @('Genesis 1, 1', 'Genesis 1:1'),
    @('Genesis: 1:1', 'Genesis 1:1'),
    @('Genesis chapter1 verse1', 'Genesis 1:1'),
    @('Genesis 1:1 to begin our reading', 'Genesis 1:1'),
    @('Genesis 1:1 through faith we understand', 'Genesis 1:1'),
    @('Genesis 1:1 through 3', 'Genesis 1:1-3'),
    @('Luke one five', 'Luke 1:5'),
    @('Titus two eleven', 'Titus 2:11'),
    @('Romans 828', 'Romans 8:28'),
    @('John 316', 'John 3:16'),
    @('Luke 115', ''),
    @('Genesis 11', ''),
    @('John 99:400', ''),
    @('Titus 4:1', ''),
    @('Jude 2:1', ''),
    @('Genesis 1:32', ''),
    @('Look at one person', ''),
    @('The title is two things', ''),
    @('One hundred people arrived', ''),
    @('John 3:16 in King James Version', 'John 3:16', 'KJV')
)
foreach ($case in $moreCases) { Assert-Reference (New-Object ScriptureDetector) $case[0] $case[1] $case[2] }
$detector = New-Object ScriptureDetector
Assert-Reference $detector 'Genesis' ''
Assert-Reference $detector 'one one' 'Genesis 1:1'
$detector.Reset()
Assert-Reference $detector 'Luke' ''
Assert-Reference $detector 'How are you today' ''
Assert-Reference $detector 'one five' ''
Assert-Reference $detector 'John 3:16' 'John 3:16'
$null = $detector.Preview('Romans 8:28')
Assert-Reference $detector 'verse seventeen' 'John 3:17'
Assert-Reference $detector 'verse 17-19' 'John 3:17-19'
$detector.Reset()
Assert-Reference $detector 'Titus' ''
$null = $detector.Preview('two eleven')
Assert-Reference $detector 'two eleven' 'Titus 2:11'
$books = [regex]::Matches($source, 'AddBook\("([^"]+)"') | ForEach-Object { $_.Groups[1].Value }
foreach ($book in $books) {
    Assert-Reference (New-Object ScriptureDetector) ($book + ' 1:1') ($book + ' 1:1')
    Assert-Reference (New-Object ScriptureDetector) ($book + ' 999:1') ''
    Assert-Reference (New-Object ScriptureDetector) ($book + ' 1:999') ''
}
$detector = New-Object ScriptureDetector
$result = $detector.Feed('Genesis 1-1')
if ($result.Confidence -ge 0.90) { throw 'Inferred dash separator must never auto-present.' }
$result = $detector.Feed('Romans 828')
if ($result.Confidence -ge 0.90) { throw 'Compact numbers must never auto-present.' }
Assert-Reference (New-Object ScriptureDetector) ("Genesis" + [char]0x200B + " 1:1") 'Genesis 1:1'
$rangeCases=@(
 @('Genesis chapter 49 verses three and verse four','Genesis 49:3-4'),
 @('Genesis chapter 49 verse three and four','Genesis 49:3-4'),
 @('Genesis 49:3-4','Genesis 49:3-4'),
 @('Genesis 49:4-3',''),
 @('Genesis 49:3-34',''),
 @('Genesis 49:3-50:2',''),
 @('Genesis 49:3 and 5',''),
 @('Genesis 49:3 and verse 4 and verse 5',''),
 @('Genesis 49:3 to',''),
 @('Genesis 49:3 and',''),
 @('Genesis chapter 49 verse three sorry verse four','Genesis 49:4'),
 @('Genesis chapter 49 verse three sorry verse 99','')
)
foreach($case in $rangeCases){Assert-Reference (New-Object ScriptureDetector) $case[0] $case[1]}
$detector=New-Object ScriptureDetector
Assert-Reference $detector 'Genesis chapter 49 verses three and' ''
Assert-Reference $detector 'verse four' 'Genesis 49:3-4'
Assert-Reference $detector 'And the excellency of my power is important. Next verse.' 'Genesis 49:4'
Assert-Reference $detector 'next verse' 'Genesis 49:5'
Assert-Reference $detector 'previous verse' 'Genesis 49:4'
Assert-Reference $detector 'go back one verse' 'Genesis 49:3'
Assert-Reference $detector 'next week we will finish' ''
Assert-Reference $detector 'next' ''
Assert-Reference $detector 'Genesis 1:31' 'Genesis 1:31'
Assert-Reference $detector 'next verse' 'Genesis 2:1'
Assert-Reference $detector 'previous verse' 'Genesis 1:31'
Assert-Reference $detector 'Genesis 50:26' 'Genesis 50:26'
Assert-Reference $detector 'next verse' 'Exodus 1:1'
Assert-Reference $detector 'previous verse' 'Genesis 50:26'
Assert-Reference $detector 'Genesis 1:1' 'Genesis 1:1'
Assert-Reference $detector 'previous verse' ''
Assert-Reference $detector 'Revelation 22:21' 'Revelation 22:21'
Assert-Reference $detector 'next verse' ''
$detector.Reset()
Assert-Reference $detector 'next verse' ''
$display=New-Object ScriptureDetection
$display.Book='Genesis';$display.Chapter=49;$display.Verse=3
$detector.SetDisplayedContext($display,$true)
Assert-Reference $detector 'next' 'Genesis 49:4'
if(!$detector.Feed('next verse').RequiresReview){throw 'Relative intents require review until live state is verified by an adapter.'}
Assert-Reference $detector 'verse four' 'Genesis 49:4'
$null=$detector.Preview('John 3:16')
Assert-Reference $detector 'next verse' 'Genesis 49:4'
Assert-Reference $detector 'John 3:16' 'John 3:16'
Assert-Reference $detector 'verse seventeen' 'John 3:17'
$display.Verse=8
$detector.SetDisplayedContext($display,$true)
Assert-Reference $detector 'next verse' 'Genesis 49:9'
$detector.SetDisplayedContext($null,$false)
Assert-Reference $detector 'next verse' ''
$detector.Reset()
$null=$detector.FeedFinal('Genesis 49:3','session1:1')
$first=$detector.FeedFinal('next verse','session1:2')
$duplicate=$detector.FeedFinal('next verse','session1:2')
$next=$detector.FeedFinal('next verse','session1:3')
if($first.ReferenceText -ne 'Genesis 49:4' -or $null -ne $duplicate -or $next.ReferenceText -ne 'Genesis 49:5'){throw 'Final event IDs must prevent duplicate navigation but allow new instructions.'}
$range=$detector.Feed('Genesis 49:3-4')
if($range.VerseEnd -ne 4 -or !$range.RequiresReview){throw 'Range must retain its end and require operator review.'}
# Real user report: a repeated boundary word and seven seconds between final chunks.
$detector=New-Object ScriptureDetector
Assert-Reference $detector 'because it is a very, very important subject. In Genesis chapter' ''
$pendingTime=$detector.GetType().GetField('pendingUtc',[Reflection.BindingFlags]'Instance,NonPublic')
$pendingTime.SetValue($detector,[datetime]::UtcNow.AddSeconds(-7))
Assert-Reference $detector 'Chapter 49 verse 3 and verse 4 Jacob was pronounced in Blessing' 'Genesis 49:3-4'
$detector.Reset()
Assert-Reference $detector 'In Genesis chapter' ''
$null=$detector.Preview('Chapter 49 verse 3 and verse 4')
$joined=$detector.Feed('Chapter 49 verse 3 and verse 4')
if($joined.ReferenceText -ne 'Genesis 49:3-4' -or $joined.SourceText -ne ('In Genesis chapter'+[Environment]::NewLine+'Chapter 49 verse 3 and verse 4')){throw 'Split-reference evidence was lost or provisional parsing consumed final context.'}
$detector.Reset()
Assert-Reference $detector 'Genesis chapter' ''
Assert-Reference $detector '49 verse 3' 'Genesis 49:3'
$detector.Reset()
Assert-Reference $detector 'Genesis chapter' ''
Assert-Reference $detector 'chapter 49' ''
Assert-Reference $detector 'verse 3 and verse 4' 'Genesis 49:3-4'
$detector.Reset()
Assert-Reference $detector 'Genesis chapter' ''
Assert-Reference $detector 'Let us welcome everybody' ''
Assert-Reference $detector 'chapter 49 verse 3' ''
$detector.Reset()
Assert-Reference $detector 'Genesis chapter' ''
$pendingTime.SetValue($detector,[datetime]::UtcNow.AddSeconds(-16))
Assert-Reference $detector 'chapter 49 verse 3' ''
$detector.Reset()
Assert-Reference $detector 'Genesis chapter' ''
Assert-Reference $detector 'John chapter 3 verse 16' 'John 3:16'
$detector.Reset()
Assert-Reference $detector 'Genesis chapter is interesting to study' ''
Assert-Reference $detector 'chapter 49 verse 3' ''
Write-Host "PASS: $count reference cases plus range-policy and event-deduplication assertions. No EasyWorship or microphone access."
$detector.Reset()
$misheard='In the Genesis chapter for tonight, verse 8, 3, and verse 4.'
Assert-Reference $detector $misheard ''
if($detector.UnresolvedBook -ne 'Genesis' -or $detector.UnresolvedSource -ne $misheard){throw 'Unresolved scripture intent was hidden or source evidence changed.'}
Assert-Reference $detector 'Jacob was pronouncing blessings on his song.' ''
if($detector.UnresolvedBook -ne 'Genesis'){throw 'Unresolved cue disappeared during immediate continuation.'}
$null=$detector.Preview('John chapter 3 verse 16')
if($detector.UnresolvedBook -ne 'Genesis'){throw 'Preview mutated unresolved intent.'}
Assert-Reference $detector 'Genesis chapter 49 verse 3 and verse 4' 'Genesis 49:3-4'
if($detector.UnresolvedBook){throw 'Resolved reference left stale uncertainty.'}
$detector.Reset()
Assert-Reference $detector 'We are getting ready for tonight at eight thirty four' ''
if($detector.UnresolvedBook){throw 'Ordinary numbers created a scripture intent.'}
Assert-Reference $detector $misheard ''
$detector.GetType().GetField('unresolvedUtc',[Reflection.BindingFlags]'Instance,NonPublic').SetValue($detector,[datetime]::UtcNow.AddSeconds(-21))
Assert-Reference $detector 'Jacob was blessing his son' ''
if($detector.UnresolvedBook){throw 'Expired unresolved intent survived.'}
Write-Host "PASS: $count reference cases including unresolved-intent persistence, expiry, preview isolation and resolution."
