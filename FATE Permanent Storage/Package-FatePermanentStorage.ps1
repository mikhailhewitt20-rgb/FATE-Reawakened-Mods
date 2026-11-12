param([string]$OutputDirectory)
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$workspace=Split-Path $root -Parent
$name='FATE_PermanentStorage'
$version='0.7.33'
$output=if($OutputDirectory){[IO.Path]::GetFullPath($OutputDirectory)}else{Join-Path $root "releases\$name-$version"}
if(Test-Path -LiteralPath $output){throw 'Preserve the existing output before creating a new build'}
$dll=Join-Path $root "src\FatePermanentStorage\bin\Release\netstandard2.1\mikhailkillszombies.FatePermanentStorage.dll"
if(!(Test-Path -LiteralPath $dll)){throw 'Build the plugin first'}
$source=Join-Path $root 'distribution\fate-permanent-storage'
foreach($required in @('README.md','CHANGELOG.md','LICENSE','icon.png')){if(!(Test-Path -LiteralPath (Join-Path $source $required))){throw "Missing distribution file $required"}}
$stage=Join-Path $output $name
$plugin=Join-Path $stage 'BepInEx\plugins\FATE_PermanentStorage'
New-Item -ItemType Directory -Path $plugin -Force | Out-Null
Copy-Item -LiteralPath $dll -Destination $plugin
Copy-Item -LiteralPath (Join-Path $source 'README.md'),(Join-Path $source 'CHANGELOG.md'),(Join-Path $source 'LICENSE'),(Join-Path $source 'icon.png') -Destination $stage
$baked=Join-Path $source 'ceiling_offsets.baked.json'; if(!(Test-Path -LiteralPath $baked)){$baked=Join-Path $env:APPDATA 'r2modmanPlus-local\FateReawakened\profiles\Default\BepInEx\plugins\FATE_PermanentStorage-FATE_PermanentStorage\FATE_PermanentStorage\ceiling_offsets.baked.json'}; if(Test-Path -LiteralPath $baked){$bakedData=Get-Content -LiteralPath $baked -Raw | ConvertFrom-Json; if($bakedData.tiles -and @($bakedData.tiles).Count -gt 0){Copy-Item -LiteralPath $baked -Destination $plugin}else{Write-Warning 'Found an empty ceiling bake; it was not included in the package.'}}
$wallBake=Join-Path $source 'wall_layout.baked.json'; if(!(Test-Path -LiteralPath $wallBake)){$wallBake=Join-Path $env:APPDATA 'r2modmanPlus-local\FateReawakened\profiles\Default\BepInEx\plugins\FATE_PermanentStorage-FATE_PermanentStorage\FATE_PermanentStorage\wall_layout.baked.json'}; if(Test-Path -LiteralPath $wallBake){$wallData=Get-Content -LiteralPath $wallBake -Raw | ConvertFrom-Json; if($wallData.walls -and @($wallData.walls).Count -gt 0){Copy-Item -LiteralPath $wallBake -Destination $plugin}else{Write-Warning 'Found an empty wall layout bake; it was not included in the package.'}}
$manifest=[ordered]@{
 name=$name
 version_number=$version
 website_url=''
 description='A permanent, player-owned storage room behind the Grove fishing dock, reached through a storage entrance, with persistent storage chests and cycleable dungeon wall and floor sets.'
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
$testTool=Join-Path $workspace 'FATE Shared Workspace\tools\Test-FateModPackage.ps1'; if(!(Test-Path -LiteralPath $testTool)){$testTool='D:\AI Projects\Modding\FATE Reawakened\FATE Shared Workspace\tools\Test-FateModPackage.ps1'}
& $testTool -PackagePath $zip
Write-Output $zip
