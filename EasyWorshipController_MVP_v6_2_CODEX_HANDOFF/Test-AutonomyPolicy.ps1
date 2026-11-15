$ErrorActionPreference='Stop'
$source=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'EasyWorshipController.cs'))

# AutonomyPolicy sits immediately above MainForm. The slice must end at the
# AutonomyPolicy class itself, not at MainForm: other types (QuotePromotionPolicy)
# were later added into that span and need dependencies this harness does not
# include.
$a=$source.IndexOf('    internal enum AutonomyLevel')
$b=$source.IndexOf('    internal sealed partial class MainForm')
$autonomyEnd=$source.IndexOf('    // Decides what "give me that scripture!" promotes')
if($autonomyEnd -lt 0){$autonomyEnd=$b}
$b=[Math]::Min($b,$autonomyEnd)
if($a -lt 0 -or $b -lt 0){throw 'AutonomyPolicy block not found'}
$body=$source.Substring($a,$b-$a).Replace('internal static class','public static class').Replace('internal enum','public enum')

Add-Type -TypeDefinition ('using System;'+$body)

$checks=0

# The approved mapping: Assist is auto prepare on, auto live off.
if([AutonomyPolicy]::AutoPrepare([AutonomyLevel]::Observe)){throw 'Observe must not auto prepare'}
if([AutonomyPolicy]::AutomaticLive([AutonomyLevel]::Observe)){throw 'Observe must not go live'}
if([AutonomyPolicy]::Prefill([AutonomyLevel]::Observe)){throw 'Observe must not pre-fill the target'}
$checks++

if(-not [AutonomyPolicy]::AutoPrepare([AutonomyLevel]::Assist)){throw 'Assist must auto prepare'}
if([AutonomyPolicy]::AutomaticLive([AutonomyLevel]::Assist)){throw 'Assist must NOT go live'}
if(-not [AutonomyPolicy]::Prefill([AutonomyLevel]::Assist)){throw 'Assist must pre-fill'}
$checks++

if(-not [AutonomyPolicy]::AutoPrepare([AutonomyLevel]::Auto)){throw 'Auto must auto prepare, so quotes still preview'}
if(-not [AutonomyPolicy]::AutomaticLive([AutonomyLevel]::Auto)){throw 'Auto must go live'}
if(-not [AutonomyPolicy]::Prefill([AutonomyLevel]::Auto)){throw 'Auto must pre-fill'}
$checks++

# Each level is a superset of the one below it.
foreach($pair in @(@('Observe','Assist'),@('Assist','Auto'))){
 $low=[AutonomyLevel]::$($pair[0]);$high=[AutonomyLevel]::$($pair[1])
 if([int]$low -ge [int]$high){throw 'Levels must be ordered Observe < Assist < Auto'}
}
$checks++

# Names are stable and human-readable.
if([AutonomyPolicy]::Label([AutonomyLevel]::Observe) -ne 'Observe' -or
   [AutonomyPolicy]::Label([AutonomyLevel]::Assist) -ne 'Assist' -or
   [AutonomyPolicy]::Label([AutonomyLevel]::Auto) -ne 'Auto'){throw 'Level labels changed'}
$checks++

# Pages are 1..3, matching the combo index used by the UI.
if([int][AutonomyLevel]::Observe -ne 0 -or [int][AutonomyLevel]::Assist -ne 1 -or [int][AutonomyLevel]::Auto -ne 2){
 throw 'Level ordinals must match the selector index'
}
$checks++

# A capability guard caps the level rather than silently granting it.
if([AutonomyPolicy]::Effective([AutonomyLevel]::Auto,$true,$true,$false) -ne [AutonomyLevel]::Assist){
 throw 'Auto without live permission must cap to Assist, not be granted'
}
if([AutonomyPolicy]::Effective([AutonomyLevel]::Auto,$false,$false,$false) -ne [AutonomyLevel]::Observe){
 throw 'No permission at all must cap to Observe'
}
if([AutonomyPolicy]::Effective([AutonomyLevel]::Assist,$false,$false,$false) -ne [AutonomyLevel]::Observe){
 throw 'Assist without prepare permission must cap to Observe'
}
$checks++

# A capped level never exceeds what was asked for.
if([AutonomyPolicy]::Effective([AutonomyLevel]::Observe,$true,$true,$true) -ne [AutonomyLevel]::Observe){
 throw 'A guard must never raise the operator choice'
}
$checks++

Write-Output ('PASS: ' + $checks + ' autonomy levels; Observe < Assist < Auto, Assist never goes live.')
