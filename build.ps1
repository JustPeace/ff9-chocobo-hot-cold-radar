param([Parameter(Mandatory=$true)][string]$GamePath)
$ErrorActionPreference = 'Stop'
[xml]$metadata = Get-Content (Get-ChildItem "$PSScriptRoot/*/ModDescription.xml").FullName -Raw
$mod = [string]$metadata.Mod.InstallationPath
$managed = Join-Path $GamePath 'x64/FF9_Data/Managed'
$refs = @('mscorlib','System','System.Core','Assembly-CSharp','Memoria.Prime','UnityEngine')
$arguments = @('-nologo','-noconfig','-nostdlib+','-langversion:5','-target:library','-deterministic+')
foreach ($name in $refs) {
    $reference = Join-Path $managed "$name.dll"
    if (!(Test-Path -LiteralPath $reference)) { throw "Missing game reference: $reference" }
    $arguments += "-r:$reference"
}
$sdkLines = @(& dotnet --list-sdks)
if ($LASTEXITCODE -ne 0) { throw 'Install a .NET SDK (8 or newer).' }
$compiler = $null
foreach ($line in $sdkLines) {
    if ($line -match '^([^ ]+) \[(.+)\]$' -and $Matches[1] -notmatch '-') {
        $candidate = Join-Path $Matches[2] "$($Matches[1])/Roslyn/bincore/csc.dll"
        if (Test-Path -LiteralPath $candidate) { $compiler = $candidate }
    }
}
if (!$compiler) { throw 'No stable .NET SDK compiler found. Install .NET SDK 8 or newer.' }
$output = Join-Path $PSScriptRoot "$mod/StreamingAssets/Scripts/Memoria.Scripts.$mod.dll"
New-Item -ItemType Directory -Force (Split-Path $output) | Out-Null
$arguments += "-out:$output"
$arguments += @(Get-ChildItem "$PSScriptRoot/Source/*.cs" | ForEach-Object { $_.FullName })
& dotnet exec $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw "Compilation failed: $LASTEXITCODE" }
Write-Host "Built $output"