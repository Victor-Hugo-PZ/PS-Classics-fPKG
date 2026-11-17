<#
.SYNOPSIS
  Builds the Windows release: one folder with the app, PkgCheck and the Tools folder, zipped.

.EXAMPLE
  .\scripts\build-release.ps1 -Version 2.2.1

.NOTES
  Each program is published as ONE self-contained .exe (the .NET runtime and the native libraries are inside it),
  with only the English and Portuguese resources, so the folder the user opens holds the programs and Tools.
  Requires the .NET 10 SDK and the git-ignored Tools folder in the repository root (see README).
  The output is dist\PS.Classics.fPKG.Builder.v<Version>.Windows.zip; the zip holds one folder named
  "PS Classics fPKG Builder v<Version>".
#>
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$OutDir = (Join-Path $PSScriptRoot '..\dist'),
    [switch]$SkipTools,
    # Smaller download, but needs the .NET 10 runtime installed on the computer
    [switch]$FrameworkDependent
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$folderName = "PS Classics fPKG Builder v$Version"
$stage = Join-Path $OutDir $folderName
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
# Single-file options: one .exe per program and no debug symbols (the languages are set in the project files)
$singleFile = @(
    '-p:PublishSingleFile=true',
    '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-p:EnableCompressionInSingleFile=true',
    '-p:DebugType=None',
    '-p:CopyToolsToOutput=false'
)

Write-Host "Publishing the app (single file, self-contained: $selfContained)..."
dotnet publish (Join-Path $repo 'Windows\PS Classics fPKG Builder\PS Classics fPKG Builder.vbproj') -c Release -r win-x64 --self-contained $selfContained -o $stage "-p:Version=$Version" @singleFile --nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'Publishing the app failed.' }

Write-Host "Publishing PkgCheck..."
dotnet publish (Join-Path $repo 'Windows\PkgCheck\PkgCheck.vbproj') -c Release -r win-x64 --self-contained $selfContained -o $stage "-p:Version=$Version" @singleFile --nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'Publishing PkgCheck failed.' }

# The Tools folder is copied as it is (every file), not through the build
if (-not $SkipTools) {
    Copy-Item -Path (Join-Path $repo 'Tools') -Destination $stage -Recurse -Force
}

# Leftovers of the publish that the user does not need
Get-ChildItem $stage -Filter '*.pdb' -ErrorAction SilentlyContinue | Remove-Item -Force

Copy-Item (Join-Path $PSScriptRoot 'release-readme.txt') (Join-Path $stage 'README.txt')

# Zip with 7-Zip: keeps forward slashes in the entry names and puts the folder at the top of the zip
$sevenZip = Join-Path $repo 'Tools\7z.exe'
if (-not (Test-Path $sevenZip)) { throw '7z.exe was not found in the Tools folder: it is needed to build the zip.' }
Push-Location $OutDir
& $sevenZip a -tzip -mx=7 $zip $folderName | Out-Null
$exit = $LASTEXITCODE
Pop-Location
if ($exit -ne 0) { throw '7-Zip failed.' }

Write-Host ("Done: {0} ({1:N1} MB, {2} files in the folder)" -f $zip, ((Get-Item $zip).Length / 1MB), (Get-ChildItem $stage -Recurse -File).Count)
