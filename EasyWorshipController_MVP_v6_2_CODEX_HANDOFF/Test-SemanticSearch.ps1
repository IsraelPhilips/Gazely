param([string]$OutputCsv=(Join-Path $PSScriptRoot '../benchmarks/semantic-search-results.csv'),[switch]$Diagnostics,[switch]$HeldOut,[switch]$ReportedSermon,[string]$ModelDirectory=(Join-Path $PSScriptRoot 'models/scripture-search'))
$ErrorActionPreference='Stop'
$cases=@(
 @('literal','Unstable as water thou shalt not excel','Genesis 49:4'),
 @('literal','Seek ye first the kingdom of God and his righteousness','Matthew 6:33'),
 @('paraphrase','Put Gods kingdom and doing what is right ahead of everything else and the things you need will follow','Matthew 6:33'),
 @('paraphrase','You are as unstable as water so you will not rise above the rest','Genesis 49:4'),
 @('paraphrase','Your words light up the path in front of my feet','Psalms 119:105'),
 @('paraphrase','Do not copy the way this world thinks let your mind be made new','Romans 12:2'),
 @('paraphrase','God did not give us a spirit of fear but power love and a sound mind','2 Timothy 1:7'),
 @('paraphrase','When you give generously it comes back pressed down shaken together and overflowing','Luke 6:38'),
 @('paraphrase','Everyone has done wrong and fallen short of Gods glory','Romans 3:23'),
 @('paraphrase','We live by believing rather than by what our eyes can see','2 Corinthians 5:7'),
 @('paraphrase','Give all your worries to him because he cares about you','1 Peter 5:7'),
 @('paraphrase','The one looking after me is the Lord so I have everything I need','Psalms 23:1'),
 @('paraphrase','Even if I speak every language without love I am just making noise','1 Corinthians 13:1'),
 @('negative','Please fix the projector before the service starts',''),
 @('negative','Our church meeting is next Tuesday and everyone should bring food',''),
 @('negative','He went to his house and saw his library and all kinds of books',''),
 @('negative','You are a talented person able to do several things well',''),
 @('negative','The microphone battery is dead and we need another cable','')
)
if($HeldOut){$cases=@(
 @('paraphrase','People who wait on the Lord will get fresh strength and rise up like eagles','Isaiah 40:31'),
 @('paraphrase','A gentle answer calms anger but harsh speech makes people more angry','Proverbs 15:1'),
 @('paraphrase','The tongue can bring death or give life','Proverbs 18:21'),
 @('paraphrase','Iron makes iron sharp and one person sharpens another','Proverbs 27:17'),
 @('paraphrase','God makes everything work together for the good of those who love him','Romans 8:28'),
 @('paraphrase','We can plant and water but it is God who makes it grow','1 Corinthians 3:6'),
 @('paraphrase','You cannot serve both God and money','Matthew 6:24'),
 @('paraphrase','Faith without any action behind it is dead','James 2:17'),
 @('paraphrase','Two people do better than one because they get a good return for their work','Ecclesiastes 4:9'),
 @('paraphrase','Love is patient and kind it does not envy or brag','1 Corinthians 13:4'),
 @('paraphrase','Jesus is the same yesterday today and forever','Hebrews 13:8'),
 @('paraphrase','People look at the outside but the Lord looks at the heart','1 Samuel 16:7'),
 @('paraphrase','A seed has to fall into the ground and die before it produces much fruit','John 12:24'),
 @('paraphrase','Do not get tired of doing what is good we will reap at the right time if we do not give up','Galatians 6:9'),
 @('paraphrase','Where you keep your treasure is where your heart will be','Matthew 6:21'),
 @('negative','The choir should practice the new song after the meeting',''),
 @('negative','The pastor is arriving at the airport at eight tonight',''),
 @('negative','Please put the announcement on the screen for everyone',''),
 @('negative','We should buy a replacement microphone this weekend',''),
 @('negative','This church has many talented musicians and excellent teachers',''),
 @('negative','I read a book about leadership and time management yesterday',''),
 @('negative','The weather is rainy so take an umbrella when you leave',''),
 @('negative','Our volunteers will arrange the chairs and clean the hall','')
)}
if($ReportedSermon){$cases=@(
 @('reported-tail','For I called him he Loon and blessed him and increased him.','Isaiah 51:2'),
 @('reported','I can to mean ye that follow of power righteousness, ye that seek the Lord. Look onto the rock once you are healed. And to the whole of the pit, once you are deep.','Isaiah 51:1'),
 @('reported','is a look onto your father Abraham and unto Sarah that hold you for I caught. For I called him he Loon and blessed him and increased him.','Isaiah 51:2'),
 @('split-tail','and all these other things shall be added onto you.','Matthew 6:33'),
 @('combined','Unless you choose. and single out the men of the inner, as Jesus said, seeking fresh the kingdom and all the and all these other things shall be added onto you.','Matthew 6:33'),
 @('combined','and he said on a stable as water. thou shalt not excel.','Genesis 49:4'),
 @('negative','In other words, you are chasing too many things.',''),
 @('negative','He said one day he went to his house and saw his library and saw vast all kinds of books there.','')
)}
$rows=Get-Content (Join-Path $PSScriptRoot 'corpus/kjv.tsv') -Encoding UTF8
$p=New-Object Diagnostics.Process
$p.StartInfo=New-Object Diagnostics.ProcessStartInfo
$p.StartInfo.FileName=Join-Path $PSScriptRoot 'search-runtime/Gazely.Search.exe'
$p.StartInfo.Arguments='"'+[IO.Path]::GetFullPath($ModelDirectory)+'" "'+(Join-Path $PSScriptRoot 'corpus/kjv.tsv')+'"'
if($Diagnostics){$p.StartInfo.Arguments+=' --diagnostics'}
$p.StartInfo.UseShellExecute=$false;$p.StartInfo.CreateNoWindow=$true
$p.StartInfo.RedirectStandardInput=$true;$p.StartInfo.RedirectStandardOutput=$true;$p.StartInfo.RedirectStandardError=$true
$clock=[Diagnostics.Stopwatch]::StartNew();$null=$p.Start();$errors=$p.StandardError.ReadToEndAsync()
function Read-Response {
 $read=$p.StandardOutput.ReadLineAsync()
 if(!$read.Wait(30000)){throw 'Semantic worker timeout'}
 if($null -eq $read.Result){throw ('Semantic worker stopped: '+$errors.Result)}
 return $read.Result
}
try {
 if((Read-Response) -ne 'READY'){throw 'Missing worker readiness'}
 $startup=$clock.ElapsedMilliseconds;$results=@()
 foreach($case in $cases){
  $clock.Restart();$p.StandardInput.WriteLine([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($case[1])));$p.StandardInput.Flush()
  $response=Read-Response;$elapsed=$clock.ElapsedMilliseconds
  if($response -eq 'ERROR'){throw 'Worker query failed'}
  $refs=@();foreach($record in ($response -split ';')){if(!$record){continue};$id=[int]($record -split ',')[0];$fields=$rows[$id] -split "`t",4;$refs+=($fields[0]+' '+$fields[1]+':'+$fields[2])}
  $results+=[pscustomobject]@{kind=$case[0];query=$case[1];expected=$case[2];candidates=($refs -join ';');hit=if($case[2]){$case[2] -in $refs}else{$refs.Count -eq 0};milliseconds=$elapsed;raw=$response}
 }
 $results|Export-Csv -LiteralPath $OutputCsv -NoTypeInformation -Encoding UTF8
 $p.Refresh();$times=@($results.milliseconds|Sort-Object)
 Write-Output "Startup: $startup ms; p95 query: $($times[[Math]::Ceiling($times.Count*.95)-1]) ms; peak working set: $([Math]::Round($p.PeakWorkingSet64/1MB)) MiB"
 $results|Select-Object kind,expected,candidates,hit,milliseconds|Format-Table -AutoSize
 Write-Output "Report: $OutputCsv"
} finally {if(!$p.HasExited){$p.StandardInput.Close();if(!$p.WaitForExit(3000)){$p.Kill()}};$p.Dispose()}
