param([string]$OutputDirectory)
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$workspace=Split-Path $root -Parent
$name='FATE_ModMenu'
$version='0.3.6'
$output=if($OutputDirectory){[IO.Path]::GetFullPath($OutputDirectory)}else{Join-Path $root "releases\$name-$version"}
if(Test-Path -LiteralPath $output){throw 'Preserve the existing output before creating a new build'}
# Thunderstore ships the BepInEx 5 (stable loader) variant only.
$dll=Join-Path $root "src\FateModMenu\bin\BepInEx5\Release\netstandard2.1\ZombieGhost.ModMenu.dll"
if(!(Test-Path -LiteralPath $dll)){throw 'Build the BepInEx 5 variant first'}
$source=Join-Path $root 'distribution\fate-mod-menu'
foreach($required in @('API.md','ModSettingsExample.cs','README.md','CHANGELOG.md','LICENSE','icon.png')){if(!(Test-Path -LiteralPath (Join-Path $source $required))){throw "Missing distribution file $required"}}
$stage=Join-Path $output $name
$plugin=Join-Path $stage 'BepInEx\plugins\FateModMenu'
New-Item -ItemType Directory -Path $plugin -Force | Out-Null
Copy-Item -LiteralPath $dll -Destination $plugin
Copy-Item -LiteralPath (Join-Path $source 'API.md'),(Join-Path $source 'ModSettingsExample.cs') -Destination $plugin
Copy-Item -LiteralPath (Join-Path $source 'README.md'),(Join-Path $source 'CHANGELOG.md'),(Join-Path $source 'LICENSE'),(Join-Path $source 'icon.png') -Destination $stage
$manifest=[ordered]@{
 name=$name
 version_number=$version
 website_url=''
 description='Adds a Mod Settings button to the pause menu, showing the options installed mods register in a native-style screen.'
 dependencies=@('BepInEx-BepInExPack-5.4.2305')
}
# UTF-8 without BOM: some JSON readers reject a leading byte-order mark.
[IO.File]::WriteAllText((Join-Path $stage 'manifest.json'),($manifest | ConvertTo-Json -Depth 5),(New-Object System.Text.UTF8Encoding($false)))
$zip=Join-Path $output "$name-$version-Thunderstore.zip"
# Compress-Archive writes backslash separators, which non-Windows extractors reject.
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[System.IO.Compression.ZipFile]::Open($zip,'Create')
try{
 foreach($file in Get-ChildItem -LiteralPath $stage -Recurse -File){
  $relative=$file.FullName.Substring($stage.Length+1).Replace('\','/')
  [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file.FullName,$relative,[System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
 }
}finally{$archive.Dispose()}
& (Join-Path $workspace 'FATE Shared Workspace\tools\Test-FateModPackage.ps1') -PackagePath $zip
Write-Output $zip
