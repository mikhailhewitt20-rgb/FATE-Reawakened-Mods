param([string]$OutputDirectory='D:\AI Projects\Modding\FATE Reawakened\FATE Aiming Mod\releases\0.5.6')
$ErrorActionPreference='Stop'
$root='D:\AI Projects\Modding\FATE Reawakened\FATE Aiming Mod'
$workspace='D:\AI Projects\Modding\FATE Reawakened\FATE Shared Workspace'
$project=Join-Path $root 'src\FATEAim\FATEAim.csproj'
$dotnet=Join-Path $workspace 'tools\dotnet\dotnet.exe'
$version='0.5.6'

& $dotnet build $project -c Release -v q --nologo
if($LASTEXITCODE -ne 0){throw "Build failed with exit code $LASTEXITCODE"}

$dll=Join-Path $root 'src\FATEAim\bin\Release\netstandard2.1\mikhailkillszombies.FateAiming.dll'
if(-not (Test-Path -LiteralPath $dll)){throw "Missing build output: $dll"}

$stage=Join-Path $OutputDirectory 'FATE_Aiming'
if(Test-Path -LiteralPath $stage){Remove-Item -LiteralPath $stage -Recurse -Force}
$plugins=Join-Path $stage 'BepInEx\plugins\FATEAim'
New-Item -ItemType Directory -Path $plugins -Force | Out-Null
Copy-Item -LiteralPath $dll -Destination $plugins

Copy-Item -LiteralPath (Join-Path $root 'distribution\README.md') -Destination (Join-Path $stage 'README.md')
Copy-Item -LiteralPath (Join-Path $root 'distribution\LICENSE') -Destination (Join-Path $stage 'LICENSE')

[ordered]@{
 name='FATE_Aiming'
 version_number=$version
 website_url=''
 description='Target-mode aiming: reticle cursor, mouse-turned camera and WASD-only movement, with reticle colours, circle thickness and a live preview in FATE Mod Menu. Requires FATE WASD and FATE Camera Turn.'
 dependencies=@(
  'BepInEx-BepInExPack-5.4.2305'
  'PartGhost-FATE_WASD-0.1.1'
  'PartGhost-FATE_CameraTurn-0.1.1'
 )
} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $stage 'manifest.json') -Encoding utf8

Add-Type -AssemblyName System.Drawing
$bitmap=[Drawing.Bitmap]::new(256,256)
$g=[Drawing.Graphics]::FromImage($bitmap)
$brush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(225,183,104))
$font=[Drawing.Font]::new('Georgia',25,[Drawing.FontStyle]::Bold)
try{
 $g.Clear([Drawing.Color]::FromArgb(27,22,18))
 $g.DrawString("FATE`nAiming`nMKZ",$font,$brush,12,38)
 $pen=[Drawing.Pen]::new([Drawing.Color]::FromArgb(225,183,104),5)
 $g.DrawEllipse($pen,86,168,84,84)
 $g.DrawLine($pen,128,168,128,252)
 $pen.Dispose()
 $bitmap.Save((Join-Path $stage 'icon.png'),[Drawing.Imaging.ImageFormat]::Png)
} finally {$font.Dispose();$brush.Dispose();$g.Dispose();$bitmap.Dispose()}

$zip=Join-Path $OutputDirectory "MikhailKillsZombies-FATE_Aiming-$version-Thunderstore.zip"
if(Test-Path -LiteralPath $zip){Remove-Item -LiteralPath $zip -Force}
# Compress-Archive writes backslash separators, which non-Windows extractors reject.
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[System.IO.Compression.ZipFile]::Open($zip,'Create')
try{
 foreach($file in Get-ChildItem -LiteralPath $stage -Recurse -File){
  $relative=$file.FullName.Substring($stage.Length+1).Replace('\','/')
  [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file.FullName,$relative,[System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
 }
}finally{$archive.Dispose()}
& (Join-Path $workspace 'tools\Test-FateModPackage.ps1') -PackagePath $zip
Write-Output $zip
