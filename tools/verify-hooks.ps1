param(
    [Parameter(Mandatory=$true)][string]$GamePath,
    [string]$RadarDll,
    [switch]$ExpectLegacy
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (!$RadarDll) { $RadarDll = Join-Path $root 'ChocoboHotCold/StreamingAssets/Scripts/Memoria.Scripts.ChocoboHotCold.dll' }
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw 'This check requires Windows .NET Framework 4.x (64-bit).' }
$output = Join-Path $root 'dist/validation/HookResolutionCheck.exe'
New-Item -ItemType Directory -Force (Split-Path $output) | Out-Null
& $compiler /nologo /target:exe "/out:$output" (Join-Path $PSScriptRoot 'HookResolutionCheck.cs')
if ($LASTEXITCODE -ne 0) { throw 'Check compilation failed' }
$arguments = @((Resolve-Path -LiteralPath $GamePath).Path, (Resolve-Path -LiteralPath $RadarDll).Path)
if ($ExpectLegacy) { $arguments += '--expect-legacy' }
& $output @arguments
if ($LASTEXITCODE -ne 0) { throw 'Hook resolution check failed' }
