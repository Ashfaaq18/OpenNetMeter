param(
    # Defaults to Installer\bin\x64\Release\en-us\<ProductName>-<ProductVersion>.msi (see Directory.Build.props).
    # Relative paths are relative to the repo root.
    [string]$MsiPath,
    [string]$OutputDir = "Installer\bin\msix",
    # Registers the unpacked layout locally for testing (needs Developer Mode). Never used for Store builds.
    [switch]$Register
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

function Get-MsiProperty($database, [string]$name) {
    $view = $null
    $record = $null
    try {
        $view = $database.GetType().InvokeMember('OpenView', 'InvokeMethod', $null, $database, @("SELECT Value FROM Property WHERE Property='$name'"))
        $view.GetType().InvokeMember('Execute', 'InvokeMethod', $null, $view, $null) | Out-Null
        $record = $view.GetType().InvokeMember('Fetch', 'InvokeMethod', $null, $view, $null)
        if ($null -eq $record) {
            return $null
        }

        return $record.GetType().InvokeMember('StringData', 'GetProperty', $null, $record, 1)
    }
    finally {
        if ($null -ne $record) {
            [Runtime.InteropServices.Marshal]::ReleaseComObject($record) | Out-Null
        }
        if ($null -ne $view) {
            $view.GetType().InvokeMember('Close', 'InvokeMethod', $null, $view, $null) | Out-Null
            [Runtime.InteropServices.Marshal]::ReleaseComObject($view) | Out-Null
        }
    }
}

# Reads ProductName, ProductVersion and the platform (summary property 7, "Template") from the MSI.
function Get-MsiInfo([string]$path) {
    $installer = $null
    $database = $null
    $summary = $null
    try {
        $installer = New-Object -ComObject WindowsInstaller.Installer
        $database = $installer.GetType().InvokeMember('OpenDatabase', 'InvokeMethod', $null, $installer, @($path, 0))
        $summary = $database.GetType().InvokeMember('SummaryInformation', 'GetProperty', $null, $database, $null)

        return @{
            ProductName    = Get-MsiProperty $database 'ProductName'
            ProductVersion = Get-MsiProperty $database 'ProductVersion'
            Template       = $summary.GetType().InvokeMember('Property', 'GetProperty', $null, $summary, 7)
        }
    }
    finally {
        # Releases the file lock on the MSI.
        foreach ($com in @($summary, $database, $installer)) {
            if ($null -ne $com) {
                [Runtime.InteropServices.Marshal]::ReleaseComObject($com) | Out-Null
            }
        }
        [GC]::Collect()
        [GC]::WaitForPendingFinalizers()
    }
}

function Invoke-SdkTool([string]$tool, [string[]]$toolArguments) {
    Write-Host "> $(Split-Path -Leaf $tool) $($toolArguments -join ' ')"
    # makepri writes UTF-16 to a redirected stdout, which shows up as NUL-separated characters.
    & $tool @toolArguments | ForEach-Object { ([string]$_).Replace([string][char]0, '').TrimEnd() }
    if ($LASTEXITCODE -ne 0) {
        throw "$(Split-Path -Leaf $tool) failed with exit code $LASTEXITCODE"
    }
}

function Reset-Directory([string]$path) {
    if (Test-Path $path) {
        Remove-Item $path -Recurse -Force
    }
    New-Item -ItemType Directory -Path $path | Out-Null
}

# 1. Inputs
if ([string]::IsNullOrWhiteSpace($MsiPath)) {
    [xml]$propsXml = Get-Content (Join-Path $repoRoot "Directory.Build.props")
    $defaultVersion = $propsXml.Project.PropertyGroup.ProductVersion
    $defaultName = $propsXml.Project.PropertyGroup.ProductName
    if ([string]::IsNullOrWhiteSpace($defaultVersion) -or [string]::IsNullOrWhiteSpace($defaultName)) {
        throw "ProductVersion/ProductName not found in Directory.Build.props"
    }

    $MsiPath = Join-Path $repoRoot "Installer\bin\x64\Release\en-us\$defaultName-$defaultVersion.msi"
}
elseif (-not [IO.Path]::IsPathRooted($MsiPath)) {
    $MsiPath = Join-Path $repoRoot $MsiPath
}

if (-not (Test-Path $MsiPath)) {
    throw "MSI not found: $MsiPath. Build it first with scripts\build-avalonia-msi.ps1"
}

if (-not [IO.Path]::IsPathRooted($OutputDir)) {
    $OutputDir = Join-Path $repoRoot $OutputDir
}

# 2. Windows SDK tools
$sdkBin = Join-Path ${env:ProgramFiles(x86)} "Windows Kits\10\bin"
$sdkDir = $null
if (Test-Path $sdkBin) {
    $sdkDir = Get-ChildItem $sdkBin -Directory |
        Where-Object { $_.Name -match '^10\.\d+\.\d+\.\d+$' } |
        Sort-Object { [version]$_.Name } -Descending |
        Where-Object { (Test-Path (Join-Path $_.FullName "x64\makeappx.exe")) -and (Test-Path (Join-Path $_.FullName "x64\makepri.exe")) } |
        Select-Object -First 1
}

if ($null -eq $sdkDir) {
    throw "makeappx.exe/makepri.exe not found under $sdkBin. Install the Windows SDK (the 'MSIX Packaging Tools' / signing tools components)."
}

$makeappx = Join-Path $sdkDir.FullName "x64\makeappx.exe"
$makepri = Join-Path $sdkDir.FullName "x64\makepri.exe"
Write-Host "Using Windows SDK $($sdkDir.Name)"

# 3. MSI metadata
Write-Host "Reading $MsiPath"
$msi = Get-MsiInfo $MsiPath
$productName = $msi.ProductName
$productVersion = $msi.ProductVersion

if ([string]::IsNullOrWhiteSpace($productName)) {
    throw "ProductName not found in the MSI"
}

if ([string]$msi.Template -notlike 'x64;*') {
    throw "The MSI must be an x64 package (Template '$($msi.Template)'). The Store package is x64 only."
}

if ($productVersion -notmatch '^\d+\.\d+\.\d+$') {
    throw "Unexpected MSI ProductVersion '$productVersion' (expected major.minor.build)."
}

# The Store reserves the fourth part of the package version; it must be 0.
$packageVersion = "$productVersion.0"
Write-Host "Package: $productName $packageVersion (x64)"

# 4. Unpack the MSI without installing it (administrative image)
$workDir = Join-Path $OutputDir "work"
$extractDir = Join-Path $workDir "extract"
$extractLog = Join-Path $workDir "extract.log"
Reset-Directory $workDir

Write-Host "Extracting the MSI..."
$msiexecArguments = @('/a', "`"$MsiPath`"", '/qn', "TARGETDIR=`"$extractDir`"", '/l*v', "`"$extractLog`"")
$msiexec = Start-Process -FilePath "msiexec.exe" -ArgumentList $msiexecArguments -Wait -PassThru
if ($msiexec.ExitCode -ne 0) {
    throw "msiexec /a failed with exit code $($msiexec.ExitCode). See $extractLog"
}

$exeName = "$productName.exe"
$exes = @(Get-ChildItem $extractDir -Recurse -File -Filter $exeName)
if ($exes.Count -ne 1) {
    throw "Expected exactly one $exeName under $extractDir, found $($exes.Count)."
}

$appDir = $exes[0].DirectoryName
Write-Host "App folder: $appDir"

# 5. Layout: app files at the package root, logos and the rendered manifest
$layoutDir = Join-Path $OutputDir "layout"
Reset-Directory $layoutDir
Copy-Item -Path (Join-Path $appDir "*") -Destination $layoutDir -Recurse

$assetsSource = Join-Path $repoRoot "Installer\Msix\Assets"
if (-not (Test-Path $assetsSource)) {
    throw "Logo assets not found: $assetsSource (generate them with Installer\Msix\generate-assets.ps1)"
}

$layoutAssets = Join-Path $layoutDir "Assets"
New-Item -ItemType Directory -Force $layoutAssets | Out-Null
Copy-Item -Path (Join-Path $assetsSource "*") -Destination $layoutAssets -Recurse

$templatePath = Join-Path $repoRoot "Installer\Msix\AppxManifest.template.xml"
$manifestText = (Get-Content $templatePath -Raw -Encoding UTF8).Replace('$(Version)', $packageVersion)
$manifestPath = Join-Path $layoutDir "AppxManifest.xml"
[IO.File]::WriteAllText($manifestPath, $manifestText, (New-Object System.Text.UTF8Encoding($false)))

# 6. Validate the rendered manifest
[xml]$manifest = Get-Content $manifestPath -Raw -Encoding UTF8
$identity = $manifest.Package.Identity
if ($identity.Version -ne $packageVersion) {
    throw "Manifest Identity Version '$($identity.Version)' does not match $packageVersion."
}

foreach ($attribute in $manifest.SelectNodes('//@Executable')) {
    if (-not (Test-Path (Join-Path $layoutDir $attribute.Value))) {
        throw "Manifest Executable '$($attribute.Value)' does not exist in the layout."
    }
}

if ($manifest.SelectNodes("//*[local-name()='PackageDependency']").Count -gt 0) {
    throw "The manifest must not declare any PackageDependency (the app is self-contained)."
}

if ($manifestText -match 'VFS[\\/]') {
    throw "The manifest must not use VFS paths; the app files are at the package root."
}

# A logo such as Assets\StoreLogo.png is resolved from scale-/targetsize-qualified files, so match by base name.
foreach ($attribute in $manifest.SelectNodes('//@Square150x150Logo | //@Square44x44Logo')) {
    $logo = Split-Path -Leaf $attribute.Value
    $logoBase = [IO.Path]::GetFileNameWithoutExtension($logo)
    if (-not (Get-ChildItem $layoutAssets -File -Filter "$logoBase*.png")) {
        throw "No file for manifest logo '$($attribute.Value)' in the layout's Assets folder."
    }
}

Write-Host "Manifest OK: $($identity.Name) $($identity.Version)"

# 7. Resource index (resources.pri), built from the manifest and logos only
$priDir = Join-Path $workDir "pri"
Reset-Directory $priDir
Copy-Item $manifestPath $priDir
Copy-Item $layoutAssets (Join-Path $priDir "Assets") -Recurse

$priConfig = Join-Path $workDir "priconfig.xml"
Write-Host "Building resources.pri..."
Invoke-SdkTool $makepri @('createconfig', '/cf', $priConfig, '/dq', 'en-US', '/pv', '10.0.0', '/o')

# The default config splits scale-200 resources into a separate resources.scale-200.pri (resource packages),
# which a single-package submission doesn't use. Dropping <packaging> keeps everything in one resources.pri.
[xml]$priConfigXml = Get-Content $priConfig -Raw
$packagingNode = $priConfigXml.resources.SelectSingleNode('packaging')
if ($null -ne $packagingNode) {
    $priConfigXml.resources.RemoveChild($packagingNode) | Out-Null
    $priConfigXml.Save($priConfig)
}

Invoke-SdkTool $makepri @('new', '/pr', $priDir, '/cf', $priConfig, '/mn', (Join-Path $priDir "AppxManifest.xml"), '/of', (Join-Path $layoutDir "resources.pri"), '/o')

# 8. Pack
$msixPath = Join-Path $OutputDir "$productName-$packageVersion-x64.msix"
Write-Host "Packing the MSIX..."
Invoke-SdkTool $makeappx @('pack', '/d', $layoutDir, '/p', $msixPath, '/o')

$msix = Get-Item $msixPath
Write-Host ""
Write-Host "MSIX ready: $msixPath"
Write-Host ("Size: {0:N1} MB" -f ($msix.Length / 1MB))
Write-Host "SHA256: $((Get-FileHash $msixPath -Algorithm SHA256).Hash)"
Write-Host "The package is unsigned on purpose: the Microsoft Store signs it on submission."

# 9. Optional local registration for testing
if ($Register) {
    if (Get-Process -Name $productName -ErrorAction SilentlyContinue) {
        throw "Close $productName first: the packaged and installed copies share the capture session and single-instance lock."
    }

    $packageName = $identity.Name
    Write-Host "Registering $packageName from $manifestPath ..."
    Get-AppxPackage -Name $packageName | Remove-AppxPackage
    Add-AppxPackage -Register $manifestPath

    Write-Host "Registered. The layout folder must stay in place while the package is registered: $layoutDir"
    Write-Host "To unregister: Get-AppxPackage $packageName | Remove-AppxPackage"
}
