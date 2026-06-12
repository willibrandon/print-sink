param(
    [string] $PackageName = 'PrintSink',
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

$expectedVirtualPrinters = @(
    [ordered]@{
        printerUri = 'printsink:print-to-pdf'
        preferredInputFormat = 'application/oxps'
        outputFileTypes = 'pdf'
        pdcFile = 'Config\PrinterPdf.pdc.xml'
        pdrFile = 'Config\PrinterPdf.pdr.xml'
        supportedFormats = @(
            [ordered]@{ type = 'application/pdf'; maxVersion = '1.7' }
        )
    },
    [ordered]@{
        printerUri = 'printsink:print-to-xps'
        preferredInputFormat = 'application/oxps'
        outputFileTypes = 'xps;oxps'
        pdcFile = 'Config\PrinterXps.pdc.xml'
        pdrFile = 'Config\PrinterXps.pdr.xml'
        supportedFormats = @(
            [ordered]@{ type = 'application/oxps'; maxVersion = '1.0' },
            [ordered]@{ type = 'application/vnd.ms-xpsdocument'; maxVersion = '1.0' }
        )
    },
    [ordered]@{
        printerUri = 'printsink:print-to-ps'
        preferredInputFormat = 'application/postscript'
        outputFileTypes = 'ps'
        pdcFile = 'Config\PrinterPostScript.pdc.xml'
        pdrFile = 'Config\PrinterPostScript.pdr.xml'
        supportedFormats = @(
            [ordered]@{ type = 'application/postscript'; maxVersion = '3.0' }
        )
    },
    [ordered]@{
        printerUri = 'printsink:print-to-cloud'
        preferredInputFormat = 'application/oxps'
        outputFileTypes = ''
        pdcFile = 'Config\PrinterCloud.pdc.xml'
        pdrFile = 'Config\PrinterCloud.pdr.xml'
        supportedFormats = @(
            [ordered]@{ type = 'application/pdf'; maxVersion = '1.7' }
        )
    },
    [ordered]@{
        printerUri = 'printsink:print-to-pwgr'
        preferredInputFormat = 'application/oxps'
        outputFileTypes = 'pwg'
        pdcFile = 'Config\PrinterPwgRaster.pdc.xml'
        pdrFile = 'Config\PrinterPwgRaster.pdr.xml'
        supportedFormats = @()
    }
)

function Get-InstalledPackage {
    param(
        [string] $Name
    )

    $package = Get-AppxPackage -Name $Name |
        Sort-Object -Property Version -Descending |
        Select-Object -First 1

    if ($null -eq $package) {
        throw "Package '$Name' is not installed for the current user."
    }

    return $package
}

function Join-PackagePath {
    param(
        [string] $PackageRoot,
        [string] $RelativePath
    )

    $fullRoot = [System.IO.Path]::GetFullPath($PackageRoot)
    $fullRootWithSeparator = $fullRoot.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    $candidate = [System.IO.Path]::GetFullPath((Join-Path $PackageRoot $RelativePath))
    if (-not $candidate.StartsWith($fullRootWithSeparator, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Package-relative path escapes the package root: $RelativePath"
    }

    return $candidate
}

function Assert-PackageFile {
    param(
        [string] $PackageRoot,
        [string] $RelativePath
    )

    $path = Join-PackagePath -PackageRoot $PackageRoot -RelativePath $RelativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Package file is missing: $RelativePath"
    }
}

function New-AppxNamespaceManager {
    param(
        [xml] $Manifest
    )

    $namespaceManager = [System.Xml.XmlNamespaceManager]::new($Manifest.NameTable)
    $namespaceManager.AddNamespace('appx', 'http://schemas.microsoft.com/appx/manifest/foundation/windows10') | Out-Null
    $namespaceManager.AddNamespace('uap3', 'http://schemas.microsoft.com/appx/manifest/uap/windows10/3') | Out-Null
    $namespaceManager.AddNamespace('desktop', 'http://schemas.microsoft.com/appx/manifest/desktop/windows10') | Out-Null
    $namespaceManager.AddNamespace('printsupport', 'http://schemas.microsoft.com/appx/manifest/printsupport/windows10') | Out-Null
    $namespaceManager.AddNamespace('printsupport2', 'http://schemas.microsoft.com/appx/manifest/printsupport/windows10/2') | Out-Null
    return ,$namespaceManager
}

function Assert-ManifestNode {
    param(
        [xml] $Manifest,
        [System.Xml.XmlNamespaceManager] $NamespaceManager,
        [string] $XPath,
        [string] $Description
    )

    $node = $Manifest.SelectSingleNode($XPath, $NamespaceManager)
    if ($null -eq $node) {
        throw "Package manifest is missing $Description."
    }

    return $node
}

function Get-InstalledPackageManifestPath {
    param(
        [string] $PackageRoot
    )

    $candidateNames = @('Package.appxmanifest', 'AppxManifest.xml')
    foreach ($candidateName in $candidateNames) {
        $candidatePath = Join-PackagePath -PackageRoot $PackageRoot -RelativePath $candidateName
        if (Test-Path -LiteralPath $candidatePath -PathType Leaf) {
            return $candidatePath
        }
    }

    throw "Installed package manifest was not found under $PackageRoot."
}

function Get-ExpectedVirtualPrinterValue {
    param(
        [System.Collections.Specialized.OrderedDictionary] $ExpectedPrinter,
        [string] $AttributeName
    )

    switch ($AttributeName) {
        'PreferredInputFormat' { return $ExpectedPrinter.preferredInputFormat }
        'PdcFile' { return $ExpectedPrinter.pdcFile }
        'PdrFile' { return $ExpectedPrinter.pdrFile }
        default { throw "Unsupported virtual-printer attribute: $AttributeName" }
    }
}

function Assert-InstalledPackageShape {
    param(
        $Package,
        [object[]] $ExpectedVirtualPrinters
    )

    if ($Package.IsDevelopmentMode) {
        throw "Package '$($Package.PackageFullName)' is registered in development mode. Install a signed MSIX with -PackagePath for E2E provisioning."
    }

    $installLocation = $Package.InstallLocation
    if ([string]::IsNullOrWhiteSpace($installLocation) -or -not (Test-Path -LiteralPath $installLocation -PathType Container)) {
        throw "Package install location is unavailable for $($Package.PackageFullName)."
    }

    $manifestPath = Get-InstalledPackageManifestPath -PackageRoot $installLocation
    [xml] $manifest = Get-Content -LiteralPath $manifestPath -Raw
    [System.Xml.XmlNamespaceManager] $namespaceManager = New-AppxNamespaceManager -Manifest $manifest
    Assert-ManifestNode -Manifest $manifest -NamespaceManager $namespaceManager -XPath '//uap3:Extension[@Category="windows.appExecutionAlias"]/uap3:AppExecutionAlias/desktop:ExecutionAlias[@Alias="printsink-app.exe"]' -Description 'the printsink-app.exe execution alias' | Out-Null
    Assert-ManifestNode -Manifest $manifest -NamespaceManager $namespaceManager -XPath '//printsupport:Extension[@Category="windows.printSupportWorkflow" and @EntryPoint="PrintSink.Tasks.PrintSupportWorkflowBackgroundTask"]' -Description 'the print support workflow extension' | Out-Null
    Assert-ManifestNode -Manifest $manifest -NamespaceManager $namespaceManager -XPath '//printsupport:Extension[@Category="windows.printSupportExtension" and @EntryPoint="PrintSink.Tasks.PrintSupportExtensionBackgroundTask"]' -Description 'the print support extension background task' | Out-Null
    Assert-ManifestNode -Manifest $manifest -NamespaceManager $namespaceManager -XPath '//printsupport:Extension[@Category="windows.printSupportSettingsUI" and @EntryPoint="PrintSink.App.App"]' -Description 'the settings UI extension' | Out-Null
    Assert-ManifestNode -Manifest $manifest -NamespaceManager $namespaceManager -XPath '//printsupport:Extension[@Category="windows.printSupportJobUI" and @EntryPoint="PrintSink.App.App"]' -Description 'the job UI extension' | Out-Null

    Assert-PackageFile -PackageRoot $installLocation -RelativePath 'WinRT.Host.dll'
    Assert-PackageFile -PackageRoot $installLocation -RelativePath 'PrintSink.Tasks.winmd'
    Assert-PackageFile -PackageRoot $installLocation -RelativePath 'PrintSink.Xps.dll'

    $activationClasses = @(
        'PrintSink.Tasks.PrintSupportWorkflowBackgroundTask',
        'PrintSink.Tasks.PrintSupportExtensionBackgroundTask',
        'PrintSink.Tasks.VirtualPrinterBackgroundTask',
        'PrintSink.Xps.XpsPageWatermarker',
        'PrintSink.Xps.XpsSequentialDocument'
    )

    foreach ($activationClass in $activationClasses) {
        Assert-ManifestNode -Manifest $manifest -NamespaceManager $namespaceManager -XPath "//appx:ActivatableClass[@ActivatableClassId=`"$activationClass`"]" -Description "activatable class $activationClass" | Out-Null
    }

    $printerNodes = @($manifest.SelectNodes('//printsupport2:PrintSupportVirtualPrinter', $namespaceManager))
    if ($printerNodes.Count -ne $ExpectedVirtualPrinters.Count) {
        throw "Expected $($ExpectedVirtualPrinters.Count) virtual-printer manifest entries but found $($printerNodes.Count)."
    }

    $reportedPrinters = @()
    foreach ($expectedPrinter in $ExpectedVirtualPrinters) {
        $printerNode = $printerNodes |
            Where-Object { $_.GetAttribute('PrinterUri') -eq $expectedPrinter.printerUri } |
            Select-Object -First 1
        if ($null -eq $printerNode) {
            throw "Package manifest is missing virtual printer '$($expectedPrinter.printerUri)'."
        }

        foreach ($attributeName in @('PreferredInputFormat', 'PdcFile', 'PdrFile')) {
            $actual = $printerNode.GetAttribute($attributeName)
            $expected = Get-ExpectedVirtualPrinterValue -ExpectedPrinter $expectedPrinter -AttributeName $attributeName
            if ($actual -ne $expected) {
                throw "Virtual printer '$($expectedPrinter.printerUri)' has $attributeName '$actual'; expected '$expected'."
            }
        }

        $actualOutputFileTypes = $printerNode.GetAttribute('OutputFileTypes')
        if ($actualOutputFileTypes -ne $expectedPrinter.outputFileTypes) {
            throw "Virtual printer '$($expectedPrinter.printerUri)' has OutputFileTypes '$actualOutputFileTypes'; expected '$($expectedPrinter.outputFileTypes)'."
        }

        Assert-PackageFile -PackageRoot $installLocation -RelativePath $expectedPrinter.pdcFile
        Assert-PackageFile -PackageRoot $installLocation -RelativePath $expectedPrinter.pdrFile

        $supportedFormatNodes = @($printerNode.SelectNodes('printsupport2:SupportedFormats/printsupport2:SupportedFormat', $namespaceManager))
        $actualSupportedFormats = @($supportedFormatNodes | ForEach-Object {
            [pscustomobject]@{
                type = $_.GetAttribute('Type')
                maxVersion = $_.GetAttribute('MaxVersion')
            }
        } | Sort-Object -Property type)
        $expectedSupportedFormats = @($expectedPrinter.supportedFormats | ForEach-Object {
            [pscustomobject]@{
                type = $_['type']
                maxVersion = $_['maxVersion']
            }
        } | Sort-Object -Property type)

        if ($actualSupportedFormats.Count -ne $expectedSupportedFormats.Count) {
            throw "Virtual printer '$($expectedPrinter.printerUri)' supported format count differs. Actual: $($actualSupportedFormats.Count); expected: $($expectedSupportedFormats.Count)."
        }

        foreach ($expectedFormat in $expectedSupportedFormats) {
            $actualFormat = $actualSupportedFormats |
                Where-Object { $_.type -eq $expectedFormat.type } |
                Select-Object -First 1
            if ($null -eq $actualFormat) {
                throw "Virtual printer '$($expectedPrinter.printerUri)' is missing supported format '$($expectedFormat.type)'."
            }

            if ($actualFormat.maxVersion -ne $expectedFormat.maxVersion) {
                throw "Virtual printer '$($expectedPrinter.printerUri)' supported format '$($expectedFormat.type)' has MaxVersion '$($actualFormat.maxVersion)'; expected '$($expectedFormat.maxVersion)'."
            }
        }

        $reportedPrinters += [ordered]@{
            printerUri = $expectedPrinter.printerUri
            preferredInputFormat = $printerNode.GetAttribute('PreferredInputFormat')
            outputFileTypes = $actualOutputFileTypes
            pdcFile = $expectedPrinter.pdcFile
            pdrFile = $expectedPrinter.pdrFile
            supportedFormats = $actualSupportedFormats
        }
    }

    return [ordered]@{
        manifestPath = $manifestPath
        virtualPrinters = $reportedPrinters
        activationClasses = $activationClasses
    }
}

function Invoke-PrintSinkAppCommand {
    param(
        [string[]] $Arguments,
        [string] $Description
    )

    $headlessLog = Join-Path $env:TEMP 'PrintSink.App.headless.log'
    Remove-Item $headlessLog -ErrorAction SilentlyContinue

    & printsink-app.exe @Arguments
    if ($LASTEXITCODE -ne 0) {
        $diagnostic = if (Test-Path $headlessLog) {
            Get-Content $headlessLog -Raw
        }
        else {
            'No headless diagnostic log was written.'
        }

        throw "$Description failed with exit code $LASTEXITCODE. $diagnostic"
    }
}

if (-not $SkipPackageInstall) {
    if ([string]::IsNullOrWhiteSpace($PackagePath)) {
        throw 'Pass -PackagePath or use -SkipPackageInstall when the package is already installed.'
    }

    if (-not (Test-Path -LiteralPath $PackagePath)) {
        throw "Package path was not found: $PackagePath"
    }

    Add-AppxPackage -Path $PackagePath -ForceApplicationShutdown -ForceUpdateFromAnyVersion
}

$package = Get-InstalledPackage -Name $PackageName
$packageShape = Assert-InstalledPackageShape -Package $package -ExpectedVirtualPrinters $expectedVirtualPrinters

$alias = Get-Command printsink-app.exe -ErrorAction SilentlyContinue
if ($null -eq $alias) {
    throw 'printsink-app.exe was not registered. Install the signed MSIX package before running E2E.'
}

Invoke-PrintSinkAppCommand -Arguments @('--disable-job-ui') -Description 'Disabling foreground job UI'
try {
    Invoke-PrintSinkAppCommand -Arguments @('--install-virtual-printers') -Description 'Headless virtual-printer provisioning'

    $printers = Get-Printer
    $installedNames = @($printers | ForEach-Object Name)
    $missingQueues = @($expectedQueues | Where-Object { $installedNames -notcontains $_ })

    if ($missingQueues.Count -gt 0) {
        throw "Missing PrintSink queues: $($missingQueues -join ', ')"
    }

    $result = [ordered]@{
        windowsVersion = [Environment]::OSVersion.Version.ToString()
        architecture = [Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
        package = [ordered]@{
            name = $package.Name
            fullName = $package.PackageFullName
            familyName = $package.PackageFamilyName
            version = $package.Version.ToString()
            installLocation = $package.InstallLocation
        }
        packageShape = $packageShape
        queues = @($expectedQueues)
    }

    if ($Cleanup) {
        Invoke-PrintSinkAppCommand -Arguments @('--remove-virtual-printers') -Description 'Headless virtual-printer cleanup'
    }

    $result | ConvertTo-Json -Depth 4
}
finally {
    Invoke-PrintSinkAppCommand -Arguments @('--enable-job-ui') -Description 'Restoring foreground job UI'
}
