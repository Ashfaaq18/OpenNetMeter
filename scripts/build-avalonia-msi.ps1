param(
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",
    [ValidateSet("win-x64")]
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

$propsPath = Join-Path $repoRoot "Directory.Build.props"
[xml]$propsXml = Get-Content $propsPath
$productVersion = $propsXml.Project.PropertyGroup.ProductVersion
$productName = $propsXml.Project.PropertyGroup.ProductName

if ([string]::IsNullOrWhiteSpace($productVersion)) {
    throw "ProductVersion not found in $propsPath"
}

if ([string]::IsNullOrWhiteSpace($productName)) {
    throw "ProductName not found in $propsPath"
}

Write-Host "Publishing Avalonia app..."
Write-Host "Cleaning Avalonia project..."
dotnet clean ".\OpenNetMeter\OpenNetMeter.csproj" --configuration $Configuration

Write-Host "Cleaning installer project..."
dotnet clean ".\Installer\OpenNetMeter-Installer.wixproj" --configuration $Configuration

$publishOutput = Join-Path $repoRoot "_rc\avalonia\$Runtime"
if (Test-Path $publishOutput) {
    Write-Host "Removing previous publish output: $publishOutput"
    Remove-Item $publishOutput -Recurse -Force
}

& ".\scripts\publish-avalonia-rc.ps1" -Runtime $Runtime -Configuration $Configuration

$publishedExe = Join-Path $repoRoot "_rc\avalonia\$Runtime\$productName.exe"
if (-not (Test-Path $publishedExe)) {
    throw "Expected published exe not found: $publishedExe"
}

Write-Host "Verified publish output: $publishedExe"
Write-Host "Building WiX installer..."
dotnet build ".\Installer\OpenNetMeter-Installer.wixproj" --configuration $Configuration -m:1

$candidateMsiPaths = @(
    (Join-Path $repoRoot "Installer\bin\$Configuration\en-us\$productName-$productVersion.msi"),
    (Join-Path $repoRoot "Installer\bin\x64\$Configuration\en-us\$productName-$productVersion.msi")
)

$msiPath = $candidateMsiPaths | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $msiPath) {
    throw "MSI build completed but the expected MSI file was not found."
}

Write-Host "MSI ready: $msiPath"
