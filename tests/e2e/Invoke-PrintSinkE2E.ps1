param(
    [string] $PackagePath,
    [switch] $SkipPackageInstall,
    [switch] $Cleanup
)

$ErrorActionPreference = 'Stop'

$isWindowsPlatform = [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
    [System.Runtime.InteropServices.OSPlatform]::Windows)

if (-not $isWindowsPlatform) {
    throw 'PrintSink E2E tests require Windows.'
}

$expectedQueues = @(
    'PrintSink - PDF',
    'PrintSink - XPS',
    'PrintSink - PostScript',
    'PrintSink - Cloud',
    'PrintSink - PWG Raster'
)

if (-not $SkipPackageInstall) {
    if ([string]::IsNullOrWhiteSpace($PackagePath)) {
        throw 'Pass -PackagePath or use -SkipPackageInstall when the package is already installed.'
    }

    if (-not (Test-Path -LiteralPath $PackagePath)) {
        throw "Package path was not found: $PackagePath"
    }

    Add-AppxPackage -Path $PackagePath -ForceApplicationShutdown -ForceUpdateFromAnyVersion
}

$headlessLog = Join-Path $env:TEMP 'PrintSink.App.headless.log'
Remove-Item $headlessLog -ErrorAction SilentlyContinue

$alias = Get-Command printsink-app.exe -ErrorAction SilentlyContinue
if ($null -eq $alias) {
    throw 'printsink-app.exe was not registered. Install the signed MSIX package before running E2E.'
}

& printsink-app.exe --install-virtual-printers
if ($LASTEXITCODE -ne 0) {
    $diagnostic = if (Test-Path $headlessLog) {
        Get-Content $headlessLog -Raw
    }
    else {
        'No headless diagnostic log was written.'
    }

    throw "Headless virtual-printer provisioning failed with exit code $LASTEXITCODE. $diagnostic"
}

$printers = Get-Printer
$installedNames = @($printers | ForEach-Object Name)
$missingQueues = @($expectedQueues | Where-Object { $installedNames -notcontains $_ })

if ($missingQueues.Count -gt 0) {
    throw "Missing PrintSink queues: $($missingQueues -join ', ')"
}

$result = [ordered]@{
    windowsVersion = [Environment]::OSVersion.Version.ToString()
    architecture = [Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
    queues = @($expectedQueues)
}

if ($Cleanup) {
    Remove-Item $headlessLog -ErrorAction SilentlyContinue
    & printsink-app.exe --remove-virtual-printers
    if ($LASTEXITCODE -ne 0) {
        $diagnostic = if (Test-Path $headlessLog) {
            Get-Content $headlessLog -Raw
        }
        else {
            'No headless diagnostic log was written.'
        }

        throw "Headless virtual-printer cleanup failed with exit code $LASTEXITCODE. $diagnostic"
    }
}

$result | ConvertTo-Json -Depth 4
