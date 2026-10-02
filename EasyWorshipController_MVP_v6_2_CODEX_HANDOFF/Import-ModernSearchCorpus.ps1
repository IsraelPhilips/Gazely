$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=Join-Path $PSScriptRoot 'corpus/engwebp_vpl.zip'
$zip=[IO.Compression.ZipFile]::OpenRead($archive)
try{$reader=New-Object IO.StreamReader($zip.GetEntry('engwebp_vpl.txt').Open());try{$lines=$reader.ReadToEnd() -split '\r?\n'}finally{$reader.Dispose()}}finally{$zip.Dispose()}
$codes='GEN EXO LEV NUM DEU JOS JDG RUT 1SA 2SA 1KI 2KI 1CH 2CH EZR NEH EST JOB PSA PRO ECC SOL ISA JER LAM EZE DAN HOS JOE AMO OBA JON MIC NAH HAB ZEP HAG ZEC MAL MAT MAR LUK JOH ACT ROM 1CO 2CO GAL EPH PHI COL 1TH 2TH 1TI 2TI TIT PHM HEB JAM 1PE 2PE 1JO 2JO 3JO JUD REV' -split ' '
$kjv=Get-Content (Join-Path $PSScriptRoot 'corpus/kjv.tsv') -Encoding UTF8
$books=@($kjv | ForEach-Object {($_ -split "`t",4)[0]} | Select-Object -Unique)
if($books.Count -ne 66){throw 'Unexpected canonical books'}
$map=@{};for($i=0;$i -lt 66;$i++){$map[$codes[$i]]=$books[$i]}
$valid=@{};foreach($line in $kjv){$f=$line -split "`t",4;$valid[($f[0..2] -join "`t")]=1}
$output=New-Object 'Collections.Generic.List[string]';$unknown=New-Object 'Collections.Generic.HashSet[string]';$excluded=0
foreach($line in $lines){
 if($line -match '^(\S+) (\d+):(\d+) (.+)$'){
  if(!$map.ContainsKey($matches[1])){$null=$unknown.Add($matches[1]);continue}
  $key=$map[$matches[1]]+"`t"+$matches[2]+"`t"+$matches[3]
  if(!$valid.ContainsKey($key)){$excluded++;continue}
  $output.Add($key+"`t"+$matches[4])
 }elseif($line.Trim()){$excluded++}
}
if($unknown.Count){throw ('Unknown source book codes: '+($unknown -join ', '))}
if($output.Count -lt 30000){throw 'Modern retrieval corpus incomplete'}
$target=Join-Path $PSScriptRoot 'corpus/web-search.tsv'
[IO.File]::WriteAllLines($target,$output,(New-Object Text.UTF8Encoding($false)))
[pscustomobject]@{source='https://ebible.org/Scriptures/engwebp_vpl.zip';license='Public domain; World English Bible name is a trademark of eBible.org';purpose='Retrieval only; presentation remains KJV by default';archiveSha256=(Get-FileHash $archive).Hash;derivedSha256=(Get-FileHash $target).Hash;verses=$output.Count;excluded=$excluded;retrievedUtc=[datetime]::UtcNow.ToString('o')} | ConvertTo-Json | Set-Content (Join-Path $PSScriptRoot 'corpus/web-provenance.json') -Encoding UTF8
Write-Output "Imported $($output.Count) modern-language retrieval verses; excluded $excluded unmapped/non-verse lines."
