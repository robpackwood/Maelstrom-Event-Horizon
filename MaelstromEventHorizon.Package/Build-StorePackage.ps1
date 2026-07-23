[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$projectPath = Join-Path $PSScriptRoot 'MaelstromEventHorizon.Package.wapproj'
$assetScript = Join-Path $PSScriptRoot 'Generate-StoreAssets.ps1'
$msbuildPath = Join-Path ${env:ProgramFiles} `
    'Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe'

if (-not (Test-Path -LiteralPath $msbuildPath)) {
    throw "Visual Studio 2026 MSBuild was not found: $msbuildPath"
}

& $assetScript

& $msbuildPath $projectPath `
    /restore `
    /t:Rebuild `
    /p:Configuration=$Configuration `
    /p:Platform=x64 `
    /p:PublishSingleFile=false `
    /p:IncludeNativeLibrariesForSelfExtract=false `
    /p:EnableCompressionInSingleFile=false `
    /p:UapAppxPackageBuildMode=StoreUpload `
    /p:AppxBundle=Never `
    /p:AppxPackageSigningEnabled=false `
    /m

if ($LASTEXITCODE -ne 0) {
    throw "Store package build failed with exit code $LASTEXITCODE."
}

$package = Get-ChildItem -Path (Join-Path $PSScriptRoot 'AppPackages') -Recurse -Filter '*.msixupload' |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if ($null -eq $package) {
    $package = Get-ChildItem -Path (Join-Path $PSScriptRoot 'AppPackages') -Recurse -Filter '*.msix' |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
}

if ($null -eq $package) {
    throw 'MSBuild completed, but no MSIX upload artifact was found.'
}

Write-Output "Store upload artifact: $($package.FullName)"
