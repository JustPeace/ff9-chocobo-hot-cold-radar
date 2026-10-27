param([Parameter(Mandatory=$true)][string]$GamePath)
$ErrorActionPreference = 'Stop'
& "$PSScriptRoot/build.ps1" -GamePath $GamePath
[xml]$metadata = Get-Content (Get-ChildItem "$PSScriptRoot/*/ModDescription.xml").FullName -Raw
$mod = [string]$metadata.Mod.InstallationPath
$version = [string]$metadata.Mod.Version
foreach ($document in @('README.md','README.zh-CN.md','LICENSE','RELEASE-CHECKLIST.md')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $document) -Destination (Join-Path $PSScriptRoot "$mod/$document") -Force
}
$dist = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Force $dist | Out-Null
$zip = Join-Path $dist "$mod-v$version.zip"
if (Test-Path -LiteralPath $zip) { throw "Package exists; rename or remove it before packaging: $zip" }
# Keep the mod's top-level folder so extraction into the game directory works.
Compress-Archive -LiteralPath (Join-Path $PSScriptRoot $mod) -DestinationPath $zip
Get-FileHash -LiteralPath $zip -Algorithm SHA256 | ForEach-Object {
    "$($_.Hash.ToLowerInvariant())  $([IO.Path]::GetFileName($zip))" |
        Set-Content -LiteralPath "$zip.sha256" -Encoding Ascii
}
Write-Host "Packaged $zip"