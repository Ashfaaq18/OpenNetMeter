param(
    [ValidateSet("win-x64", "win-arm64", "win-x86", "linux-x64")]
    [string]$Runtime = "win-x64",
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "OpenNetMeter.Avalonia\OpenNetMeter.Avalonia.csproj"
$output = Join-Path $repoRoot "_rc\avalonia\$Runtime\"

Write-Host "Publishing Avalonia RC..."
Write-Host "Project: $project"
Write-Host "Runtime: $Runtime"
Write-Host "Configuration: $Configuration"
Write-Host "Output: $output"

dotnet publish $project `
  --configuration $Configuration `
  --runtime $Runtime `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  --output $output

Write-Host "Done. RC files are in $output"
