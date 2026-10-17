param(
    [string]$PackageArchive = (Join-Path (Split-Path $PSScriptRoot -Parent) 'releases/Gazely-Windows-MVP-20261015.zip'),
    [string]$OutputFile = (Join-Path (Split-Path $PSScriptRoot -Parent) 'releases/Gazely-Setup-20261015.exe')
)
$ErrorActionPreference = 'Stop'
$archive = [IO.Path]::GetFullPath($PackageArchive)
$output = [IO.Path]::GetFullPath($OutputFile)
if (-not (Test-Path -LiteralPath $archive)) { throw "Offline package archive is missing: $archive" }
if (Test-Path -LiteralPath $output) { throw "Installer already exists: $output" }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip=[IO.Compression.ZipFile]::OpenRead($archive)
try {
    $markerEntry=$zip.Entries | Where-Object { $_.FullName -match '(^|[\\/])cloud-release\.json$' } | Select-Object -First 1
    $exeEntry=$zip.Entries | Where-Object { $_.FullName -match '(^|[\\/])Gazely\.exe$' } | Select-Object -First 1
    if(-not $markerEntry -or -not $exeEntry) { throw 'Package lacks the cloud release marker or Gazely.exe.' }
    $reader=New-Object IO.StreamReader($markerEntry.Open())
    try { $marker=$reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
    if(-not (($marker.access_mode -eq 'free_offline' -and $marker.environment -eq 'offline' -and -not $marker.supabase_url -and -not $marker.portal_url) -or
              ($marker.access_mode -eq 'email_entry' -and $marker.environment -eq 'production' -and $marker.supabase_url -match '^https://') -or
              ($marker.access_mode -eq 'verified_email' -and $marker.environment -eq 'production' -and $marker.portal_url -match '^https://'))) {
        throw 'Installer requires an offline free build or a production cloud build.'
    }
    $stream=$exeEntry.Open();$sha=[Security.Cryptography.SHA256]::Create()
    try { $actual=([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-','') } finally { $stream.Dispose();$sha.Dispose() }
    if($actual -ne $marker.candidate_sha256) { throw 'Package Gazely.exe does not match the cloud release marker.' }
} finally { $zip.Dispose() }
$folder = Split-Path $output -Parent
New-Item -ItemType Directory -Path $folder -Force | Out-Null
$hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash
$source = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'GazelySetup.cs') -Raw -Encoding UTF8
$installScript = [IO.File]::ReadAllBytes((Join-Path $PSScriptRoot 'Install-Gazely.ps1'))
$source = $source.Replace('__ARCHIVE_SHA256__',$hash).Replace('__ARCHIVE_NAME__',[IO.Path]::GetFileName($archive)).Replace('__INSTALL_SCRIPT_BASE64__',[Convert]::ToBase64String($installScript))
$stub = Join-Path $folder ([IO.Path]::GetFileNameWithoutExtension($output) + '.stub.exe')
if (Test-Path -LiteralPath $stub) { throw "Installer stub already exists: $stub" }

Add-Type -AssemblyName System.Windows.Forms,System.Drawing,System.Core
$parameters = New-Object System.CodeDom.Compiler.CompilerParameters
$parameters.GenerateExecutable = $true
$parameters.OutputAssembly = $stub
$parameters.TreatWarningsAsErrors = $true
$parameters.CompilerOptions = '/target:winexe /win32icon:"' + (Join-Path $PSScriptRoot 'assets/Gazely.ico') + '"'
foreach ($name in @('System','System.Core','System.Drawing','System.Windows.Forms')) {
    $assembly = [AppDomain]::CurrentDomain.GetAssemblies() | Where-Object { $_.GetName().Name -eq $name } | Select-Object -First 1
    if (-not $assembly) { Add-Type -AssemblyName $name; $assembly = [AppDomain]::CurrentDomain.GetAssemblies() | Where-Object { $_.GetName().Name -eq $name } | Select-Object -First 1 }
    $null = $parameters.ReferencedAssemblies.Add($assembly.Location)
}
$provider = New-Object Microsoft.CSharp.CSharpCodeProvider
try {
    $compiled = $provider.CompileAssemblyFromSource($parameters,$source)
    if ($compiled.Errors.HasErrors) { throw (($compiled.Errors | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine) }
} finally { $provider.Dispose() }

$destination = [IO.File]::Open($output,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write)
try {
    foreach ($inputPath in @($stub,$archive)) {
        $inputFile = [IO.File]::OpenRead($inputPath)
        try { $inputFile.CopyTo($destination,1024*1024) }
        finally { $inputFile.Dispose() }
    }
    $footer = [BitConverter]::GetBytes([long](Get-Item -LiteralPath $archive).Length)
    $destination.Write($footer,0,$footer.Length)
} finally { $destination.Dispose() }
Write-Output "Installer: $output"
Write-Output "Bytes: $((Get-Item -LiteralPath $output).Length)"
Write-Output "SHA256: $((Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash)"
