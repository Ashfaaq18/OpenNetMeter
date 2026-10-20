param(
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",
    [ValidateSet("win-x64")]
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

# Without an explicit platform WiX builds an x86 package, which installs the 64-bit app into
# Program Files (x86) (Product.wxs uses ProgramFiles6432Folder, which follows the package platform).
$installerPlatforms = @{ "win-x64" = "x64" }
$installerPlatform = $installerPlatforms[$Runtime]

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
dotnet clean ".\Installer\OpenNetMeter-Installer.wixproj" --configuration $Configuration -p:Platform=$installerPlatform

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
dotnet build ".\Installer\OpenNetMeter-Installer.wixproj" --configuration $Configuration -p:Platform=$installerPlatform -m:1

# Only look in the platform folder: an older x86 build in Installer\bin\$Configuration is not cleaned
# by the platform-specific clean above and must not be picked up by mistake.
$msiPath = Join-Path $repoRoot "Installer\bin\$installerPlatform\$Configuration\en-us\$productName-$productVersion.msi"
if (-not (Test-Path $msiPath)) {
    throw "MSI build completed but the expected MSI file was not found: $msiPath"
}

Write-Host "MSI ready: $msiPath"
