param([string]$SourcePath,[string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if(!$SourcePath){$SourcePath=Join-Path $root 'EasyWorshipController_MVP_v6_2_CODEX_HANDOFF/EasyWorshipController.cs'}
if(!$OutputDirectory){$OutputDirectory=Join-Path $PSScriptRoot 'runs/phase3-parser'}
New-Item -ItemType Directory -Path $OutputDirectory -Force|Out-Null
$source = Get-Content $SourcePath -Raw -Encoding UTF8
$start = $source.IndexOf('    internal sealed class ScriptureDetection')
$end = $source.IndexOf('    internal sealed class UtteranceSegmenter')
$parser = 'using System; using System.Collections.Generic; using System.Text; using System.Text.RegularExpressions; ' + $source.Substring($start, $end - $start).Replace('internal sealed class', 'public sealed class')
Add-Type -TypeDefinition $parser
$phrases = @(
 'In Genesis, chapter 49 verse CS3 and verse 4.',
 'In Genesis chapter 49 verses three and verse four',
 'In Genesis chapter 49 verse three and verse four',
 'Genesis 49:3-4',
 'Genesis chapter 49 verse three',
 'Seek ye first the kingdom and all these other things shall be added unto you',
 'Unstable as water thou shalt not excel'
)
$rows = foreach ($phrase in $phrases) {
 $detector = New-Object ScriptureDetector
 $result = $detector.Feed($phrase)
 [pscustomobject]@{Phrase=$phrase; Reference=$(if($result){$result.ReferenceText}else{''}); Explanation=$detector.LastExplanation}
}
$rows | Export-Csv (Join-Path $OutputDirectory 'parser-probes.csv') -NoTypeInformation -Encoding UTF8
$rows | Format-List
