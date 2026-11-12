param([string]$OutputDirectory)
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
if(-not $OutputDirectory){$OutputDirectory=Join-Path $root 'releases\0.3.4'}
$workspace=if($env:FATE_SHARED_WORKSPACE){$env:FATE_SHARED_WORKSPACE}else{'D:\AI Projects\Modding\FATE Reawakened\FATE Shared Workspace'}
$project=Join-Path $root 'src\FATEQuickSpell\FATEQuickSpell.csproj'
$dotnet=Join-Path $workspace 'tools\dotnet\dotnet.exe'
$version='0.3.4'

# The csproj's LoaderCore is relative to the old D: layout; point it at the real workspace.
& $dotnet build $project -c Release -v q --nologo "-p:LoaderCore=$workspace\tools\bepinex\BepInEx\core"
if($LASTEXITCODE -ne 0){throw "Build failed with exit code $LASTEXITCODE"}

$dll=Join-Path $root 'src\FATEQuickSpell\bin\Release\netstandard2.1\mikhailkillszombies.FateQuickSpell.dll'
if(-not (Test-Path -LiteralPath $dll)){throw "Missing build output: $dll"}

$stage=Join-Path $OutputDirectory 'FATE_QuickSpell'
if(Test-Path -LiteralPath $stage){Remove-Item -LiteralPath $stage -Recurse -Force}
$plugins=Join-Path $stage 'BepInEx\plugins\FATEQuickSpell'
New-Item -ItemType Directory -Path $plugins -Force | Out-Null
Copy-Item -LiteralPath $dll -Destination $plugins

Copy-Item -LiteralPath (Join-Path $root 'distribution\README.md') -Destination (Join-Path $stage 'README.md')
Copy-Item -LiteralPath (Join-Path $root 'distribution\LICENSE') -Destination (Join-Path $stage 'LICENSE')

[ordered]@{
 name='FATE_QuickSpell'
 version_number=$version
 website_url=''
 description='Direction-based spell wheel: hold the key, flick towards a spell, release to activate it. The cursor is hidden while the wheel is up and nothing has to be clicked. With a game pad the game''s own wheel is kept, and the left stick still walks.'
 dependencies=@(
  'BepInEx-BepInExPack-5.4.2305'
 )
} | ConvertTo-Json -Depth 4 | ForEach-Object { [IO.File]::WriteAllText((Join-Path $stage 'manifest.json'), $_, (New-Object System.Text.UTF8Encoding($false))) }

Add-Type -AssemblyName System.Drawing
$bitmap=[Drawing.Bitmap]::new(256,256)
$g=[Drawing.Graphics]::FromImage($bitmap)
$brush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(225,183,104))
$font=[Drawing.Font]::new('Georgia',22,[Drawing.FontStyle]::Bold)
try{
 $g.Clear([Drawing.Color]::FromArgb(27,22,18))
 $g.DrawString("FATE`nQuick`nSpell",$font,$brush,12,26)
 $pen=[Drawing.Pen]::new([Drawing.Color]::FromArgb(225,183,104),4)
 $g.DrawEllipse($pen,88,168,84,84)
 # Six slots around the ring, one of them filled: the direction the mod picks by.
 $dot=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(225,183,104))
 try{
  for($i=0;$i -lt 6;$i++){
   $angle=(-90+$i*60)*[Math]::PI/180
   $x=130+[Math]::Cos($angle)*30
   $y=210+[Math]::Sin($angle)*30
   $radius=7
   if($i -eq 0){$radius=11}
   $g.FillEllipse($dot,[single]($x-$radius),[single]($y-$radius),$radius*2,$radius*2)
  }
 } finally {$dot.Dispose()}
 $pen.Dispose()
 $bitmap.Save((Join-Path $stage 'icon.png'),[Drawing.Imaging.ImageFormat]::Png)
} finally {$font.Dispose();$brush.Dispose();$g.Dispose();$bitmap.Dispose()}

$zip=Join-Path $OutputDirectory "MikhailKillsZombies-FATE_QuickSpell-$version-Thunderstore.zip"
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
