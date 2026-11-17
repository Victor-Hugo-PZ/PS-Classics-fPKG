<#
.SYNOPSIS
  Builds the Windows release zip: the app, the PkgCheck tool and the Tools folder.

.EXAMPLE
  .\scripts\build-release.ps1 -Version 2.1.0

.NOTES
  Requires the .NET 10 SDK and the git-ignored Tools folder in the repository root (see README).
  The output is dist\PS.Classics.fPKG.Builder.v<Version>.Windows.zip.
#>
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$OutDir = (Join-Path $PSScriptRoot '..\dist'),
    [switch]$SkipTools,
    # By default the .NET runtime is included in the zip, so the .exe runs without installing anything (about +130 MB).
    [switch]$FrameworkDependent
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$name = "PS Classics fPKG Builder v$Version"
$stage = Join-Path $OutDir $name
$zip = Join-Path $OutDir "PS.Classics.fPKG.Builder.v$Version.Windows.zip"

if (-not $SkipTools -and -not (Test-Path (Join-Path $repo 'Tools\PS4\gengp4_patch.exe'))) {
    throw "The Tools folder was not found in '$repo'. Extract it from the original release (see README) or use -SkipTools."
}
if (-not (Test-Path (Join-Path $repo 'external\LibOrbisPkg\LibOrbisPkg\LibOrbisPkg.csproj'))) {
    throw "The LibOrbisPkg submodule is missing. Run: git submodule update --init"
}

if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
if (Test-Path $zip) { Remove-Item $zip -Force }
New-Item -ItemType Directory -Path $stage -Force | Out-Null

$selfContained = if ($FrameworkDependent) { 'false' } else { 'true' }

Write-Host "Publishing the app (self-contained: $selfContained)..."
$toolsProp = if ($SkipTools) { '-p:CopyToolsToOutput=false' } else { '-p:CopyToolsToOutput=true' }
dotnet publish (Join-Path $repo 'Windows\PS Classics fPKG Builder\PS Classics fPKG Builder.vbproj') -c Release -r win-x64 --self-contained $selfContained -o $stage "-p:Version=$Version" $toolsProp --nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'Publishing the app failed.' }

Write-Host "Publishing PkgCheck..."
dotnet publish (Join-Path $repo 'Windows\PkgCheck\PkgCheck.vbproj') -c Release -r win-x64 --self-contained $selfContained -o $stage "-p:Version=$Version" --nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'Publishing PkgCheck failed.' }

Copy-Item (Join-Path $PSScriptRoot 'release-readme.txt') (Join-Path $stage 'README.txt')

# Zip with 7-Zip: Compress-Archive of Windows PowerShell 5.1 writes backslashes in the entry names
$sevenZip = Join-Path $repo 'Tools\7z.exe'
if (Test-Path $sevenZip) {
    Push-Location $stage
    & $sevenZip a -tzip -mx=7 $zip '*' | Out-Null
    Pop-Location
    if ($LASTEXITCODE -ne 0) { throw '7-Zip failed.' }
} else {
    Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -CompressionLevel Optimal
}

Write-Host ("Done: {0} ({1:N1} MB)" -f $zip, ((Get-Item $zip).Length / 1MB))
