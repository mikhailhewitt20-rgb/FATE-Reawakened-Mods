param(
    [string]$GameManaged = 'D:\SteamLibrary\steamapps\common\FATE Reawakened\FateReawakened_Data\Managed',
    [string]$LoaderCore = 'D:\AI Projects\Modding\FATE Reawakened\FATE Shared Workspace\tools\bepinex\BepInEx\core'
)
# Local build without the .NET SDK: compiles the plugin with the .NET Framework
# compiler against the game's own assemblies. Use `dotnet build` on the csproj when
# an SDK is available; this script exists so the mod can be built on this machine.
$ErrorActionPreference = 'Stop'
$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$root = $PSScriptRoot
$output = Join-Path $root 'build\FateMusicPlayer.dll'
New-Item -ItemType Directory -Path (Split-Path $output) -Force | Out-Null

$sources = Get-ChildItem (Join-Path $root 'src\FateMusicPlayer') -Filter *.cs | ForEach-Object { $_.FullName }
$references = @(
    "$LoaderCore\BepInEx.dll",
    "$LoaderCore\0Harmony.dll",
    "$GameManaged\netstandard.dll",
    "$GameManaged\UnityEngine.dll",
    "$GameManaged\UnityEngine.CoreModule.dll",
    "$GameManaged\UnityEngine.AudioModule.dll",
    "$GameManaged\UnityEngine.UnityWebRequestModule.dll",
    "$GameManaged\UnityEngine.UnityWebRequestAudioModule.dll",
    "$GameManaged\System.Core.dll",
    "$GameManaged\System.dll",
    "$GameManaged\System.Xml.dll",
    "$GameManaged\System.Windows.Forms.dll",
    "$GameManaged\System.Runtime.Serialization.dll",
    "$GameManaged\System.IO.Compression.dll",
    "$GameManaged\System.IO.Compression.FileSystem.dll"
)
foreach ($reference in $references) {
    if (!(Test-Path -LiteralPath $reference)) { throw "Missing reference: $reference" }
}

$cscArgs = @('-nologo', '-noconfig', '-langversion:5', '-target:library', "-out:$output")
foreach ($reference in $references) { $cscArgs += "-r:$reference" }
$cscArgs += $sources
& $csc @cscArgs
if ($LASTEXITCODE -ne 0) { throw "csc failed with exit code $LASTEXITCODE" }
Write-Output $output
