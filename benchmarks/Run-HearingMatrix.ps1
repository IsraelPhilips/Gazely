param([Parameter(Mandatory=$true)][string]$Audio,[Parameter(Mandatory=$true)][string]$OutputDirectory,[ValidateSet('accelerated','realtime')][string]$Mode='accelerated')
$ErrorActionPreference='Stop'
if(Test-Path -LiteralPath $OutputDirectory){throw 'Choose a new results directory.'}
New-Item -ItemType Directory -Path $OutputDirectory|Out-Null
# Sequential ablations; never run recognition variants concurrently.
$variants=@(
 @{name='base-6500-400';model='base.en';window=6500;endpoint=400;beam=3},
 @{name='base-raw';model='base.en';window=6500;endpoint=400;beam=3;raw=$true},
 @{name='base-10000-400';model='base.en';window=10000;endpoint=400;beam=3},
 @{name='base-16000-400';model='base.en';window=16000;endpoint=400;beam=3},
 @{name='base-6500-700';model='base.en';window=6500;endpoint=700;beam=3},
 @{name='base-10000-700';model='base.en';window=10000;endpoint=700;beam=3},
 @{name='base-16000-700';model='base.en';window=16000;endpoint=700;beam=3},
 @{name='base-beam1';model='base.en';window=16000;endpoint=400;beam=1},
 @{name='small-beam1';model='small.en';window=16000;endpoint=400;beam=1},
 @{name='small-beam3';model='small.en';window=16000;endpoint=400;beam=3},
 @{name='base-vad';model='base.en';window=16000;endpoint=400;beam=3;vad=$true},
 @{name='base-refinement';model='base.en';window=6500;endpoint=400;beam=3;refinement=$true}
)
$variants|ConvertTo-Json|Set-Content (Join-Path $OutputDirectory 'matrix.json') -Encoding UTF8
foreach($v in $variants){
 Write-Output ('Evaluating '+$v.name)
 & (Join-Path $PSScriptRoot 'Run-LiveReplay.ps1') -Audio $Audio -Mode $Mode -Model $v.model -WindowMs $v.window -EndpointMs $v.endpoint -BeamSize $v.beam -RawAudio:([bool]$v.raw) -NeuralVad:([bool]$v.vad) -Refinement:([bool]$v.refinement) -Retrieval -OutputDirectory (Join-Path $OutputDirectory $v.name)
 if(!$?){throw ('Evaluation failed: '+$v.name)}
}
