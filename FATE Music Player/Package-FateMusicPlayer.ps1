param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$manifest = Get-Content -Raw (Join-Path $root 'manifest.json') | ConvertFrom-Json
$version = $manifest.version_number
$name = $manifest.name
$output = if ($OutputDirectory) { [IO.Path]::GetFullPath($OutputDirectory) } else { Join-Path $root "releases\$name-$version" }
if (Test-Path -LiteralPath $output) { throw 'Preserve the existing output before creating a new build' }

$dll = Join-Path $root 'build\FateMusicPlayer.dll'
if (!(Test-Path -LiteralPath $dll)) { throw 'Build the plugin first: src/FateMusicPlayer/FateMusicPlayer.csproj' }

$stage = Join-Path $output $name
$plugin = Join-Path $stage 'BepInEx\plugins\FateMusicPlayer'
New-Item -ItemType Directory -Path $plugin -Force | Out-Null
Copy-Item -LiteralPath $dll -Destination $plugin
Copy-Item -LiteralPath (Join-Path $root 'PACK_FORMAT.md') -Destination $plugin
Copy-Item -LiteralPath (Join-Path $root 'manifest.json'), (Join-Path $root 'README.md'), (Join-Path $root 'CHANGELOG.md') -Destination $stage
# A pack author needs the format doc beside the plugin, and the game must be able to
# create the packs folder on first run, so ship it empty rather than absent.
New-Item -ItemType Directory -Path (Join-Path $plugin 'packs') -Force | Out-Null

$zip = Join-Path $output "$name-$version-Thunderstore.zip"
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::Open($zip, 'Create')
try {
    foreach ($file in Get-ChildItem -LiteralPath $stage -Recurse -File) {
        $relative = $file.FullName.Substring($stage.Length + 1).Replace('\', '/')
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, $relative, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally { $archive.Dispose() }

# Validate the layout a mod manager installs into a profile.
Add-Type -AssemblyName System.IO.Compression.FileSystem
$check = [System.IO.Compression.ZipFile]::OpenRead($zip)
try {
    $entries = $check.Entries | ForEach-Object { $_.FullName }
    foreach ($required in @('manifest.json', 'README.md', 'BepInEx/plugins/FateMusicPlayer/FateMusicPlayer.dll')) {
        if ($entries -notcontains $required) { throw "Package is missing $required" }
    }
    if ($entries | Where-Object { $_ -like '*\*' }) { throw 'Package contains backslash separators' }
    Write-Output ("Package OK: {0} entries" -f $entries.Count)
} finally { $check.Dispose() }
Write-Output $zip
