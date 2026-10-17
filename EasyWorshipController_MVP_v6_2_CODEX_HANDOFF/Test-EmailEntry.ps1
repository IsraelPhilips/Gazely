$ErrorActionPreference='Stop'
$assembly=[Reflection.Assembly]::LoadFrom((Join-Path $PSScriptRoot 'Gazely.mvp-candidate.exe'))
$flags=[Reflection.BindingFlags]'NonPublic,Public,Static,Instance'
$build=$assembly.GetType('EasyWorshipControllerMvp.GazelyCloudBuild',$true)
if(-not $build.GetField('EmailEntry',$flags).GetValue($null) -or
   $build.GetField('Required',$flags).GetValue($null) -or
   -not $build.GetProperty('LoginRequired',$flags).GetValue($null,$null)) {
    throw 'Candidate is not the required email-entry build.'
}
$type=$assembly.GetType('EasyWorshipControllerMvp.GazelyCloudAccess',$true)
$folder=Join-Path $PSScriptRoot ('email-gate-test-'+[guid]::NewGuid().ToString('N'))
$type.GetProperty('PathOverride',$flags).SetValue($null,$folder,$null)
try {
    $constructor=$type.GetConstructor($flags,$null,[Type[]]@(),$null)
    $access=$constructor.Invoke(@())
    $args=[object[]]@($null)
    if($type.GetMethod('CanStart',$flags).Invoke($access,$args)) { throw 'New installation was allowed without email.' }
    $email='gazely-gate-'+[guid]::NewGuid().ToString('N')+'@example.invalid'
    $task=$type.GetMethod('EnterEmail',$flags).Invoke($access,@($email))
    $task.GetAwaiter().GetResult()
    $args=[object[]]@($null)
    if(-not $type.GetMethod('CanStart',$flags).Invoke($access,$args)) { throw 'Recorded email did not unlock listening.' }
    $reloaded=$constructor.Invoke(@())
    $args=[object[]]@($null)
    if(-not $type.GetMethod('CanStart',$flags).Invoke($reloaded,$args)) { throw 'Saved email did not unlock offline listening.' }
    Write-Output "PASS: email gate, Supabase recording, local reload. Test address: $email"
} finally {
    $type.GetProperty('PathOverride',$flags).SetValue($null,$null,$null)
    $file=Join-Path $folder 'account.dat'
    if(Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file }
    if(Test-Path -LiteralPath $folder) { Remove-Item -LiteralPath $folder }
}
