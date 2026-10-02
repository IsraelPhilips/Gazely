$ErrorActionPreference='Stop'
$source=Join-Path (Split-Path $PSScriptRoot -Parent) 'Brand Assets/07_App_Icons'
$target=Join-Path $PSScriptRoot 'assets'
if(!(Test-Path -LiteralPath $source)) {
 if((Test-Path -LiteralPath (Join-Path $target 'gazely.png')) -and (Test-Path -LiteralPath (Join-Path $target 'Gazely.ico'))){return}
 throw 'Gazely brand PNGs are missing. Restore the supplied Brand Assets folder or the packaged assets folder.'
}
New-Item -ItemType Directory -Path $target -Force | Out-Null
# Package the approved PNG pixels without redrawing, cropping or resampling them.
Copy-Item -LiteralPath (Join-Path $source 'Gazely_AppIcon_256.png') -Destination (Join-Path $target 'gazely.png') -Force
$sizes=@(16,24,32,48,64,128,256)
$images=@($sizes | ForEach-Object { ,([IO.File]::ReadAllBytes((Join-Path $source "Gazely_AppIcon_$_.png"))) })
$stream=New-Object IO.MemoryStream
$writer=New-Object IO.BinaryWriter($stream)
try {
 $writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]$sizes.Count)
 $offset=6+16*$sizes.Count
 for($i=0;$i -lt $sizes.Count;$i++) {
  $dimension=if($sizes[$i] -eq 256){0}else{$sizes[$i]}
  $writer.Write([byte]$dimension);$writer.Write([byte]$dimension);$writer.Write([byte]0);$writer.Write([byte]0)
  $writer.Write([uint16]1);$writer.Write([uint16]32);$writer.Write([uint32]$images[$i].Length);$writer.Write([uint32]$offset)
  $offset+=$images[$i].Length
 }
 foreach($bytes in $images){$writer.Write([byte[]]$bytes)}
 $writer.Flush();[IO.File]::WriteAllBytes((Join-Path $target 'Gazely.ico'),$stream.ToArray())
} finally { $writer.Dispose();$stream.Dispose() }
