# Build EasyWorship Controller MVP
# Offline build using Windows PowerShell / .NET Framework.
# No internet, Visual Studio, NuGet, or dotnet SDK required.
#
# v2: resolves actual assembly file paths before compiling.
# This fixes "Metadata file 'WindowsBase.dll' could not be found"
# on systems where Add-Type cannot resolve WPF/UIAutomation assemblies by short name.

param([string]$OutputFileName = "EasyWorshipController.exe",[switch]$CloudRelease,[switch]$EmailEntry,[switch]$FreeAccess,[string]$CloudConfig)

$ErrorActionPreference = "Stop"

$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$source = Join-Path $here "EasyWorshipController.cs"
$output = Join-Path $here $OutputFileName
$temporaryOutput = Join-Path $here ("controller-build-" + [Guid]::NewGuid().ToString('N') + ".exe")
if(([int][bool]$CloudRelease + [int][bool]$EmailEntry + [int][bool]$FreeAccess) -gt 1) { throw 'Choose one release access mode.' }
if($OutputFileName -eq 'Gazely.mvp-candidate.exe' -and -not ($CloudRelease -or $EmailEntry -or $FreeAccess)) {
    throw 'Gazely.mvp-candidate.exe requires an explicit release mode.'
}

Write-Host ""
Write-Host "EasyWorship Controller MVP - Offline Builder v6"
Write-Host "================================================"
Write-Host ""

if (!(Test-Path $source)) {
    Write-Host "ERROR: EasyWorshipController.cs was not found."
    exit 1
}

function Resolve-AssemblyPath {
    param([Parameter(Mandatory=$true)][string]$Name)

    # Load the assembly into this PowerShell process first.
    Add-Type -AssemblyName $Name -ErrorAction Stop

    $asm = [AppDomain]::CurrentDomain.GetAssemblies() |
        Where-Object { $_.GetName().Name -eq $Name } |
        Select-Object -First 1

    if ($null -eq $asm) {
        throw "Assembly '$Name' loaded but could not be resolved."
    }

    if ([string]::IsNullOrWhiteSpace($asm.Location)) {
        throw "Assembly '$Name' has no file location."
    }

    return $asm.Location
}

try {
    Write-Host "Resolving Windows/.NET assemblies..."

    $assemblyNames = @(
        "System",
        "System.Core",
        "System.Drawing",
        "System.Windows.Forms",
        "System.Speech",
        "System.Security",
        "System.Web.Extensions",
        "WindowsBase",
        "UIAutomationClient",
        "UIAutomationTypes"
    )

    $refs = @()

    foreach ($name in $assemblyNames) {
        $path = Resolve-AssemblyPath $name
        $refs += $path
        Write-Host ("  OK  {0}" -f $name)
    }

    Write-Host ""
    Write-Host "Compiling controller..."
    & (Join-Path $here 'Package-BrandAssets.ps1')

    $code = Get-Content $source -Raw -Encoding UTF8
    $adapters = Get-Content (Join-Path $here 'PresentationAdapters.cs') -Raw -Encoding UTF8
    $interface = Get-Content (Join-Path $here 'GazelyInterface.cs') -Raw -Encoding UTF8
    $recovery = Get-Content (Join-Path $here 'ChapterPronunciationRecovery.cs') -Raw -Encoding UTF8
    $cloud = Get-Content (Join-Path $here 'GazelyCloud.cs') -Raw -Encoding UTF8
    if($CloudRelease -or $EmailEntry) {
        if([string]::IsNullOrWhiteSpace($CloudConfig) -or !(Test-Path -LiteralPath $CloudConfig)) { throw 'An email-enabled build requires -CloudConfig with the public project URL and publishable key.' }
        $cloudSettings=Get-Content -LiteralPath $CloudConfig -Raw | ConvertFrom-Json
        if($cloudSettings.environment -notin @('test','production')) { throw 'Cloud config environment must be test or production.' }
        if($cloudSettings.supabaseUrl -notmatch '^https://[a-z0-9-]+\.supabase\.co$' -or
           [string]::IsNullOrWhiteSpace($cloudSettings.publishableKey) -or
           ($CloudRelease -and [string]::IsNullOrWhiteSpace($cloudSettings.rsaModulus)) -or
           ($CloudRelease -and [string]::IsNullOrWhiteSpace($cloudSettings.rsaExponent)) -or
           ($CloudRelease -and $cloudSettings.environment -eq 'production' -and $cloudSettings.portalUrl -notmatch '^https://') -or
           (-not [string]::IsNullOrWhiteSpace($cloudSettings.portalUrl) -and $cloudSettings.portalUrl -notmatch '^https://')) {
            throw 'Cloud release public configuration is incomplete.'
        }
        $escaper=New-Object System.Web.Script.Serialization.JavaScriptSerializer
        $cloud=$cloud.Replace('"__GAZELY_SUPABASE_URL__"',$escaper.Serialize([string]$cloudSettings.supabaseUrl))
        $cloud=$cloud.Replace('"__GAZELY_SUPABASE_PUBLISHABLE_KEY__"',$escaper.Serialize([string]$cloudSettings.publishableKey))
        $cloud=$cloud.Replace('"__GAZELY_RSA_MODULUS__"',$escaper.Serialize([string]$cloudSettings.rsaModulus))
        $cloud=$cloud.Replace('"__GAZELY_RSA_EXPONENT__"',$escaper.Serialize([string]$cloudSettings.rsaExponent))
        $cloud=$cloud.Replace('"__GAZELY_PORTAL_URL__"',$escaper.Serialize([string]$cloudSettings.portalUrl))
    }
    # Shared imports already exist in the main source; combine namespace blocks.

    $parameters=New-Object System.CodeDom.Compiler.CompilerParameters
    $parameters.GenerateExecutable=$true
    $parameters.OutputAssembly=$temporaryOutput
    $parameters.CompilerOptions='/target:winexe /win32icon:"'+(Join-Path $here 'assets/Gazely.ico')+'"'+$(if($CloudRelease){' /define:GAZELY_CLOUD_RELEASE'}elseif($EmailEntry){' /define:GAZELY_EMAIL_ENTRY'}elseif($FreeAccess){' /define:GAZELY_FREE_ACCESS'}else{''})
    foreach($reference in $refs){$null=$parameters.ReferencedAssemblies.Add($reference)}
    $parameters.TreatWarningsAsErrors=$true
    $provider=New-Object Microsoft.CSharp.CSharpCodeProvider
    try {
        $compiled=$provider.CompileAssemblyFromSource($parameters,[string[]]@($code,$adapters,$interface,$recovery,$cloud))
        if($compiled.Errors.HasErrors){throw (($compiled.Errors | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine)}
    } finally { $provider.Dispose() }

    if (!(Test-Path $temporaryOutput)) {
        throw "Compilation returned without creating EasyWorshipController.exe."
    }
    # Preserve the last good executable if compilation fails. If the target is
    # open, Windows refuses replacement and the user can close it and retry.
    try { Move-Item -LiteralPath $temporaryOutput -Destination $output -Force }
    catch {
        if($OutputFileName -eq 'Gazely.exe' -and (Test-Path -LiteralPath $output)) {
            $pending=Join-Path $here 'Gazely.pending.exe'
            Move-Item -LiteralPath $temporaryOutput -Destination $pending -Force
            Write-Host 'BUILD SUCCESSFUL. Update staged as Gazely.pending.exe because Gazely.exe could not be replaced.'
            Write-Host 'Close Gazely and run RUN_GAZELY.bat to activate it.'
            exit 0
        }
        throw
    }

    Write-Host ""
    if($OutputFileName -eq 'Gazely.exe') {
        $obsoletePending=Join-Path $here 'Gazely.pending.exe'
        if(Test-Path -LiteralPath $obsoletePending){Remove-Item -LiteralPath $obsoletePending}
    }
    if($CloudRelease -or $EmailEntry -or $FreeAccess) {
        $releaseMarker=[pscustomobject]@{
            candidate_sha256=(Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash
            access_mode=$(if($FreeAccess){'free_offline'}elseif($EmailEntry){'email_entry'}else{'verified_email'})
            supabase_url=$(if($FreeAccess){''}else{[string]$cloudSettings.supabaseUrl})
            portal_url=$(if($FreeAccess -or $EmailEntry){''}else{[string]$cloudSettings.portalUrl})
            environment=$(if($FreeAccess){'offline'}else{[string]$cloudSettings.environment})
            built_utc=[DateTime]::UtcNow.ToString('o')
        }
        $releaseMarker | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $here 'cloud-release.json') -Encoding UTF8
    }
    Write-Host "BUILD SUCCESSFUL"
    Write-Host ""
    Write-Host "Created:"
    Write-Host $output
    Write-Host ""
    Write-Host "You can now run $OutputFileName."
    exit 0
}
catch {
    if (Test-Path -LiteralPath $temporaryOutput) {
        Remove-Item -LiteralPath $temporaryOutput -Force
    }
    Write-Host ""
    Write-Host "BUILD FAILED"
    Write-Host "------------"
    Write-Host $_.Exception.Message
    Write-Host ""
    Write-Host "Take a photo/copy of this error and send it back for adjustment."
    exit 1
}
