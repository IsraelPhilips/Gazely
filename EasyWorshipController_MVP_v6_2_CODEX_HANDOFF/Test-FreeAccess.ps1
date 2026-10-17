$ErrorActionPreference='Stop'
$assembly=[Reflection.Assembly]::LoadFrom((Join-Path $PSScriptRoot 'Gazely.mvp-candidate.exe'))
$flags=[Reflection.BindingFlags]'NonPublic,Public,Static,Instance'
$build=$assembly.GetType('EasyWorshipControllerMvp.GazelyCloudBuild',$true)
if(-not $build.GetField('FreeAccess',$flags).GetValue($null) -or
   $build.GetField('Required',$flags).GetValue($null) -or
   $build.GetField('EmailEntry',$flags).GetValue($null) -or
   $build.GetProperty('LoginRequired',$flags).GetValue($null,$null) -or
   $build.GetProperty('EndpointConfigured',$flags).GetValue($null,$null)) {
    throw 'Candidate is not an offline free-access build.'
}
$type=$assembly.GetType('EasyWorshipControllerMvp.GazelyCloudAccess',$true)
$folder=Join-Path $PSScriptRoot ('free-access-test-'+[guid]::NewGuid().ToString('N'))
$type.GetProperty('PathOverride',$flags).SetValue($null,$folder,$null)
try {
    $access=$type.GetConstructor($flags,$null,[Type[]]@(),$null).Invoke(@())
    $args=[object[]]@($null)
    if(-not $type.GetMethod('CanStart',$flags).Invoke($access,$args) -or
       -not $type.GetProperty('CanAutoPrepare',$flags).GetValue($access,$null) -or
       -not $type.GetProperty('CanAutomaticLive',$flags).GetValue($access,$null)) {
        throw 'Listening or automation is gated in the free release.'
    }
    Write-Output 'PASS: listening, Mini access and automation require no account or network configuration.'
} finally {
    $type.GetProperty('PathOverride',$flags).SetValue($null,$null,$null)
    if(Test-Path -LiteralPath $folder) { Remove-Item -LiteralPath $folder }
}
