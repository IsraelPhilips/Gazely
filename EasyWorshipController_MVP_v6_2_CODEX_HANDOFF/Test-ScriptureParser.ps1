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
Assert-Reference $detector "that's a problem next verse" 'Genesis 49:4'
Assert-Reference $detector 'do not move now next verse' ''
Assert-Reference $detector 'the problem next verse is difficult' ''
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
$detector.Reset()
Assert-Reference $detector 'Malachi chapter four verse two' 'Malachi 4:2'
$corrected=$detector.Feed('No, Micah, Micah.')
if($corrected.ReferenceText -ne 'Micah 4:2' -or !$corrected.RequiresReview -or $corrected.EvidenceKind -ne 'correction'){throw 'Book-only correction did not preserve numbers as review-required evidence'}
Assert-Reference $detector 'verse one' 'Micah 4:1'
$detector.Reset()
Assert-Reference $detector 'Malachi chapter four verses one and two. No, Micah.' 'Micah 4:1-2'
$detector.Reset()
Assert-Reference $detector 'No Micah' ''
Assert-Reference $detector 'Malachi 4:2' 'Malachi 4:2'
Assert-Reference $detector 'Micah' ''
$detector.Reset()
Assert-Reference $detector 'Psalms 119:105' 'Psalms 119:105'
Assert-Reference $detector 'No Micah' ''
$detector.Reset()
Assert-Reference $detector 'Malachi 4:2' 'Malachi 4:2'
$null=$detector.Preview('No Micah')
Assert-Reference $detector 'verse three' 'Malachi 4:3'
Assert-Reference $detector 'No Micah is not what I meant' ''
$detector.Reset()
Assert-Reference $detector 'Malachi 4:2' 'Malachi 4:2'
$detector.GetType().GetField('contextUpdatedUtc',[Reflection.BindingFlags]'Instance,NonPublic').SetValue($detector,[datetime]::UtcNow.AddSeconds(-31))
Assert-Reference $detector 'No Micah' ''
Write-Host 'PASS: book correction retains valid numbers/ranges, updates verse context, rejects stale/invalid/unanchored cases and isolates previews.'
$detector.Reset()
Assert-Reference $detector "Now if you go to Leviticus, let's jump a bit." ''
Assert-Reference $detector "Let's jump in, let's see what the vertical says. Sixth verse 13." 'Leviticus 6:13'
Assert-Reference $detector "It says, go to verse 11. Let's start from verse 11. Okay." 'Leviticus 6:11'
Assert-Reference $detector 'Okay, verse 12.' 'Leviticus 6:12'
$detector.Reset()
Assert-Reference $detector 'Leviticus 6th verse 13' 'Leviticus 6:13'
Assert-Reference $detector 'Leviticus sixth verse thirteen' 'Leviticus 6:13'
Assert-Reference $detector "Now if you go to Leviticus, let's jump a bit. Let's see what Leviticus says. 6th verse 13." 'Leviticus 6:13'
$detector.Reset()
Assert-Reference $detector 'Sixth verse 13' ''
Assert-Reference $detector 'Let us go to Leviticus, let us jump a bit.' ''
Assert-Reference $detector 'John 3 verse 16' 'John 3:16'
$detector.Reset()
Assert-Reference $detector 'Let us go to Leviticus, let us jump a bit.' ''
$detector.GetType().GetField('unresolvedUtc',[Reflection.BindingFlags]'Instance,NonPublic').SetValue($detector,[datetime]::UtcNow.AddSeconds(-21))
Assert-Reference $detector 'Sixth verse 13' ''
$detector.Reset()
Assert-Reference $detector 'We finished sixth in the race and drove west eleven miles' ''
Write-Host 'PASS: Leviticus split call, chapter ordinals and contextual verse commands; missing/expired anchors and ordinary ordinal speech abstain.'
$detector.Reset()
Assert-Reference $detector 'Leviticus' ''
Assert-Reference $detector "11 let's start from verse 11 okay and he pulled off his garment" ''
if($detector.UnresolvedChapter -ne 0){throw 'Verse directive became a chapter'}
Assert-Reference $detector 'Okay verse 12' ''
$detector.Reset()
Assert-Reference $detector 'Leviticus chapter six verse thirteen' 'Leviticus 6:13'
Assert-Reference $detector 'Go to VES 11' 'Leviticus 6:11'
Assert-Reference $detector 'Okay verse 12' 'Leviticus 6:12'
Write-Host 'PASS: repeated verse number cannot invent a missing chapter; VES uses existing chapter only.'
$detector.Reset()
Assert-Reference $detector "Let's see what Leviticus is. 6th verse 13." 'Leviticus 6:13'
$detector.Reset()
Assert-Reference $detector 'Leviticus is important for 6 people and 13 families' ''

$active=New-Object ActiveReferenceState
$selected=New-Object ScriptureDetection
$selected.Book='Leviticus';$selected.Chapter=2;$selected.Verse=3;$selected.EvidenceKind='quotation'
$book=New-Object ScriptureDetection
$book.Book='Leviticus';$book.RequiresReview=$true
$active.PreserveSelectedPassage($selected,$book)
$active.Preview($book)
$active.Confirm($book)
if($active.Current.ReferenceText -ne 'Leviticus 2:3' -or !$active.Current.RequiresReview){throw 'Book emphasis erased or silently confirmed a selected quotation'}
$other=New-Object ScriptureDetection
$other.Book='Ephesians'
$active.PreserveSelectedPassage($selected,$other)
$active.Confirm($other)
if($active.Current.Book -ne 'Ephesians' -or $active.Current.Chapter -ne 0){throw 'New book inherited old quotation numbers'}
$live=New-Object LiveTranscriptBuffer
$live.Update('1','Genesis forty',$false,'10:00:00')
$live.Update('1','Genesis forty nine',$false,'10:00:01')
if($live.Text -ne '[10:00:00] Genesis forty nine'){throw 'Partial text duplicated or timestamp jumped'}
$live.Update('1','Genesis 49',$true,'10:00:02')
$live.Update('2','verse three',$false,'10:00:03')
$live.Update('2','verse four',$true,'10:00:04')
if($live.Text -ne "[10:00:00] Genesis 49`r`n[10:00:03] verse four`r`n"){throw 'Final did not replace live words in place'}
$live.Update('4','new live words',$false,'10:00:05')
$live.Update('3','older final',$true,'10:00:06')
if(!$live.Text.EndsWith('[10:00:05] new live words')){throw 'Older final erased newer live words'}
$live.DiscardPartial()
if($live.Text.Contains('new live words')){throw 'Restart kept unfinished words'}
$live.Clear()
if($live.Text.Length -ne 0){throw 'Clear retained transcript'}
Write-Host 'PASS: same-book selected quote retention and in-place transcript revisions/finals/restart.'

# Translation context belongs to the active passage, not the entire session.
$versions=New-Object ScriptureDetector
$null=$versions.Feed('John 3:16 in NIV')
foreach($phrase in @('verse seventeen','next verse')) {
 $v=$versions.Feed($phrase)
 if($v.Translation -ne 'NIV' -or !$v.TranslationExplicit){throw "Version lost on $phrase"}
}
foreach($phrase in @('use the Message','give me that in NLT','read it in the amplified bible','NIV')) {
 $v=$versions.Feed($phrase)
 if($null -eq $v -or $v.ReferenceText -ne 'John 3:18' -or !$v.TranslationExplicit -or !$v.RequiresReview -or $v.EvidenceKind -ne 'translation'){throw "Translation-only request failed: $phrase"}
}
$null=$versions.Preview('use the Message')
$v=$versions.Feed('verse nineteen')
if($v.Translation -ne 'NIV' -or !$v.TranslationExplicit){throw 'Partial translation changed finalized context'}
foreach($phrase in @('this message is important','do not use NIV','we often read the new living translation','use the Message tomorrow')) {
 if($null -ne $versions.Feed($phrase)){throw "Non-directive changed translation: $phrase"}
}
$v=$versions.Feed('Romans 8:1')
if($v.TranslationExplicit -or ![string]::IsNullOrEmpty($v.Translation)){throw 'New reference inherited previous version'}
$versions.Reset()
if($null -ne $versions.Feed('use NIV')){throw 'Translation invented an unanchored passage'}
$null=$versions.Feed('John chapter three in NIV')
$v=$versions.Feed('verse sixteen')
if($v.Translation -ne 'NIV' -or !$v.TranslationExplicit){throw 'Split chapter/verse lost requested version'}
$null=$versions.Feed('John 3:16 in NIV')
$v=$versions.Feed('next verse in NLT')
if($v.Translation -ne 'NLT' -or !$v.TranslationExplicit){throw 'Navigation ignored a new explicit version'}
Write-Output 'PASS: translation-only directives, verse/navigation inheritance, split references, preview isolation, new-reference KJV reset and negative commands.'
