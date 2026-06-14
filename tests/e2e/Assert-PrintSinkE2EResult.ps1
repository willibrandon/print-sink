param(
    [Parameter(Mandatory)]
    [string] $ResultPath,

    [switch] $RequireCleanup
)

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'PrintSinkFeatureMatrix.ps1')

$expectedQueues = @(
    'PrintSink - PDF',
    'PrintSink - XPS',
    'PrintSink - PostScript',
    'PrintSink - Cloud',
    'PrintSink - PWG Raster',
    'PrintSink - PCLm'
)
$expectedFileBackedOutputs = @(
    [pscustomobject]@{ queue = 'PrintSink - PDF'; format = 'pdf'; route = 'application/oxps -> Pdf; Convert; Convert XPS to PDF.'; contains = 'foo' },
    [pscustomobject]@{ queue = 'PrintSink - XPS'; format = 'oxps'; route = 'application/oxps -> Oxps; Copy; Endpoint supports passthrough.'; contains = 'foo' },
    [pscustomobject]@{ queue = 'PrintSink - PostScript'; format = 'postscript'; route = 'application/postscript -> PostScript; Copy; Endpoint supports passthrough.'; contains = '' },
    [pscustomobject]@{ queue = 'PrintSink - PWG Raster'; format = 'pwg'; route = 'application/oxps -> PwgRaster; Convert; Convert XPS to PWG Raster.'; contains = '' },
    [pscustomobject]@{ queue = 'PrintSink - PCLm'; format = 'pclm'; route = 'application/oxps -> Pclm; Convert; Convert XPS to PCLm.'; contains = '' }
)
$expectedConvertedOutputs = @(
    [pscustomobject]@{ queue = 'PrintSink - PDF'; format = 'pdf'; route = 'application/oxps -> Pdf; Convert; Convert XPS to PDF.'; contains = 'foo' },
    [pscustomobject]@{ queue = 'PrintSink - PWG Raster'; format = 'pwg'; route = 'application/oxps -> PwgRaster; Convert; Convert XPS to PWG Raster.'; contains = '' },
    [pscustomobject]@{ queue = 'PrintSink - PCLm'; format = 'pclm'; route = 'application/oxps -> Pclm; Convert; Convert XPS to PCLm.'; contains = '' }
)
$expectedXpsCopyOutput = [pscustomobject]@{
    queue = 'PrintSink - XPS'
    format = 'oxps'
    route = 'application/oxps -> Oxps; Copy; Endpoint supports passthrough.'
    contains = 'foo'
}

$requiredSnapshotContexts = @(
    'after provisioning',
    'after management UI check',
    'after extension capability refresh',
    'after user default print ticket update',
    'after virtual-printer attribute-read assertion',
    'after printing PrintSink - PDF',
    'after printing PrintSink - XPS',
    'after printing PrintSink - PostScript',
    'after printing PrintSink - PWG Raster',
    'after printing PrintSink - PCLm',
    'after printing PrintSink - Cloud',
    'after Notepad PDF print',
    'after concurrent real prints',
    'after PDF passthrough',
    'after WinRT source print',
    'after settings UI owner check',
    'after settings text watermark print',
    'after settings image watermark print',
    'after failed image watermark print',
    'after job UI watermark print',
    'after job UI cancel',
    'after IPP PSA association'
)

$minimumWindowsVersion = [Version]'10.0.26100.0'

$expectedMxdcQualityDetail = 'mxdcQuality=Text=Png,Draft=JpegHighCompression,Normal=JpegMediumCompression,High=JpegLowCompression,Photo=Png,Auto=JpegMediumCompression,Fax=JpegHighCompression'
$expectedPdcFeatureDetail = 'pdcFeatures=PageMediaSize,PageMediaType,JobInputBin,JobOutputBin,JobPageOrder,JobStapleAllDocuments,PageResolution,JobWatermarkMode'
$expectedPdcOptionDetail = 'pdcOptions=Receipt80Millimeter,ArchivePaper,ThermalReceiptMedia,AutomationInputBin,AutomationOutputBin,OddPagesThenEvenPages,StapleUpperLeft,Dpi600,Dpi1200,WatermarkOff,WatermarkText,WatermarkImage'
$expectedPdrResourceDetail = 'pdrResourceNames=ArchivePaper,AutomationInputBin,AutomationOutputBin,Dpi1200,Dpi600,JobWatermarkMode,OddPagesThenEvenPages,Receipt80Millimeter,StapleUpperLeft,ThermalReceiptMedia,WatermarkImage,WatermarkOff,WatermarkText'
$expectedPrinterSelectedDetailParts = @(
    'adaptiveCard=set',
    'adaptiveCardVersion=1.0',
    'adaptiveCardPrinter=PrintSink - PDF',
    'additionalFields=requested',
    'allowed=',
    'requested=3',
    'features=PageMediaType,PageOutputQuality',
    'parameters=JobCopiesAllDocuments'
)
$expectedVirtualPrinterDisplayNames = @(
    [pscustomobject]@{ printerUri = 'printsink:print-to-pdf'; displayName = 'ms-resource:PdfPrintDisplayName'; queue = 'PrintSink - PDF'; preferredInputFormat = 'application/oxps' },
    [pscustomobject]@{ printerUri = 'printsink:print-to-xps'; displayName = 'ms-resource:XpsPrintDisplayName'; queue = 'PrintSink - XPS'; preferredInputFormat = 'application/oxps' },
    [pscustomobject]@{ printerUri = 'printsink:print-to-ps'; displayName = 'ms-resource:PostScriptPrintDisplayName'; queue = 'PrintSink - PostScript'; preferredInputFormat = 'application/postscript' },
    [pscustomobject]@{ printerUri = 'printsink:print-to-cloud'; displayName = 'ms-resource:CloudPrintDisplayName'; queue = 'PrintSink - Cloud'; preferredInputFormat = 'application/oxps' },
    [pscustomobject]@{ printerUri = 'printsink:print-to-pwgr'; displayName = 'ms-resource:PwgRasterPrintDisplayName'; queue = 'PrintSink - PWG Raster'; preferredInputFormat = 'application/oxps' },
    [pscustomobject]@{ printerUri = 'printsink:print-to-pclm'; displayName = 'ms-resource:PclmPrintDisplayName'; queue = 'PrintSink - PCLm'; preferredInputFormat = 'application/oxps' }
)
$expectedVirtualPrinterSupportedFormats = @(
    [pscustomobject]@{ printerUri = 'printsink:print-to-pdf'; formats = @([pscustomobject]@{ type = 'application/pdf'; maxVersion = '1.7' }) },
    [pscustomobject]@{ printerUri = 'printsink:print-to-xps'; formats = @([pscustomobject]@{ type = 'application/oxps'; maxVersion = '1.0' }, [pscustomobject]@{ type = 'application/vnd.ms-xpsdocument'; maxVersion = '1.0' }) },
    [pscustomobject]@{ printerUri = 'printsink:print-to-ps'; formats = @([pscustomobject]@{ type = 'application/postscript'; maxVersion = '3.0' }) },
    [pscustomobject]@{ printerUri = 'printsink:print-to-cloud'; formats = @([pscustomobject]@{ type = 'application/pdf'; maxVersion = '1.7' }) },
    [pscustomobject]@{ printerUri = 'printsink:print-to-pwgr'; formats = @() },
    [pscustomobject]@{ printerUri = 'printsink:print-to-pclm'; formats = @() }
)

function Assert-Condition {
    param(
        [bool] $Condition,
        [string] $Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Assert-SupportedWindowsVersion {
    param(
        [string] $WindowsVersion
    )

    Assert-Condition (-not [string]::IsNullOrWhiteSpace($WindowsVersion)) 'The E2E result did not include windowsVersion.'
    $version = [Version]$WindowsVersion
    Assert-Condition ($version -ge $minimumWindowsVersion) "The E2E result came from Windows build $version; expected $minimumWindowsVersion or later."
}

function Assert-PackageEvidence {
    param(
        [object] $Package
    )

    Assert-Condition ($null -ne $Package) 'The E2E result did not include package evidence.'
    Assert-Condition ([string](Get-ResultProperty -Object $Package -Name 'name') -eq 'PrintSink') 'The E2E package evidence had the wrong package name.'
    Assert-Condition (-not [string]::IsNullOrWhiteSpace([string](Get-ResultProperty -Object $Package -Name 'fullName'))) 'The E2E package evidence omitted the full package name.'
    Assert-Condition (-not [string]::IsNullOrWhiteSpace([string](Get-ResultProperty -Object $Package -Name 'familyName'))) 'The E2E package evidence omitted the package family name.'
    Assert-Condition (-not [string]::IsNullOrWhiteSpace([string](Get-ResultProperty -Object $Package -Name 'version'))) 'The E2E package evidence omitted the package version.'
    Assert-Condition (-not [string]::IsNullOrWhiteSpace([string](Get-ResultProperty -Object $Package -Name 'installLocation'))) 'The E2E package evidence omitted the install location.'

    $sourcePath = [string](Get-ResultProperty -Object $Package -Name 'sourcePath')
    if (-not [string]::IsNullOrWhiteSpace($sourcePath)) {
        Assert-Condition ([System.IO.Path]::GetExtension($sourcePath) -eq '.msix') "The E2E package source path was not an MSIX: $sourcePath"
    }

    $buildConfiguration = [string](Get-ResultProperty -Object $Package -Name 'buildConfiguration')
    if (-not [string]::IsNullOrWhiteSpace($buildConfiguration)) {
        Assert-Condition ($buildConfiguration -in @('Debug', 'Release')) "The E2E package build configuration was invalid: $buildConfiguration"
    }

    $buildPlatform = [string](Get-ResultProperty -Object $Package -Name 'buildPlatform')
    if (-not [string]::IsNullOrWhiteSpace($buildPlatform)) {
        Assert-Condition ($buildPlatform -in @('x64', 'ARM64')) "The E2E package build platform was invalid: $buildPlatform"
    }
}

function Assert-SetEqual {
    param(
        [object[]] $Actual,
        [object[]] $Expected,
        [string] $Description
    )

    $actualValues = @($Actual | ForEach-Object { [string]$_ } | Sort-Object)
    $expectedValues = @($Expected | ForEach-Object { [string]$_ } | Sort-Object)
    $missing = @($expectedValues | Where-Object { $_ -notin $actualValues })
    $unexpected = @($actualValues | Where-Object { $_ -notin $expectedValues })
    $duplicates = @(
        $actualValues |
            Group-Object |
            Where-Object { $_.Count -gt 1 } |
            ForEach-Object { $_.Name }
    )

    if ($missing.Count -gt 0 -or $unexpected.Count -gt 0 -or $duplicates.Count -gt 0) {
        throw "$Description did not match. Missing: $($missing -join ', '); unexpected: $($unexpected -join ', '); duplicates: $($duplicates -join ', ')."
    }
}

function Get-ResultProperty {
    param(
        [object] $Object,
        [string] $Name
    )

    if ($null -eq $Object) {
        return $null
    }

    if ($Object -is [System.Collections.IDictionary]) {
        return $Object[$Name]
    }

    return $Object.$Name
}

function Get-ResultByQueue {
    param(
        [object[]] $Results,
        [string] $Queue
    )

    return @($Results |
        Where-Object {
            (Get-ResultProperty -Object $_ -Name 'queue') -eq $Queue `
                -or (Get-ResultProperty -Object $_ -Name 'name') -eq $Queue
        } |
        Select-Object -First 1)[0]
}

function Assert-DetailContainsParts {
    param(
        [string] $Detail,
        [string[]] $ExpectedParts,
        [string] $Description
    )

    foreach ($expectedPart in $ExpectedParts) {
        Assert-Condition ($Detail -like "*$expectedPart*") "$Description omitted required detail: $expectedPart"
    }
}

function Assert-PrinterSelectedDiagnostic {
    param(
        [object] $PrinterSelected,
        [string] $Description
    )

    Assert-Condition ($null -ne $PrinterSelected) "$Description did not include printer-selected evidence."
    Assert-Condition ([string](Get-ResultProperty -Object $PrinterSelected -Name 'message') -eq 'Printer selected') "$Description did not report the Printer selected diagnostic."
    Assert-Condition ([string](Get-ResultProperty -Object $PrinterSelected -Name 'endpoint') -eq 'PrintSink - PDF') "$Description did not target the PDF queue."
    Assert-DetailContainsParts `
        -Detail ([string](Get-ResultProperty -Object $PrinterSelected -Name 'detail')) `
        -ExpectedParts $expectedPrinterSelectedDetailParts `
        -Description $Description
}

function Assert-LocalizedQueueNameEvidence {
    param(
        [object] $Artifact
    )

    Assert-Condition ($null -ne $Artifact) 'Localized queue-name evidence did not include an artifact.'

    $manifestNames = @(Get-ResultProperty -Object $Artifact -Name 'manifestNames')
    $installedQueues = @(Get-ResultProperty -Object $Artifact -Name 'installedQueues')
    foreach ($expectedPrinter in $expectedVirtualPrinterDisplayNames) {
        $manifestName = @($manifestNames |
            Where-Object { [string](Get-ResultProperty -Object $_ -Name 'printerUri') -eq $expectedPrinter.printerUri } |
            Select-Object -First 1)[0]
        Assert-Condition ($null -ne $manifestName) "Localized queue-name evidence omitted manifest printer URI $($expectedPrinter.printerUri)."
        Assert-Condition (
            [string](Get-ResultProperty -Object $manifestName -Name 'displayName') -eq $expectedPrinter.displayName) `
            "Manifest printer URI $($expectedPrinter.printerUri) did not use $($expectedPrinter.displayName)."

        $installedQueue = @($installedQueues |
            Where-Object { [string](Get-ResultProperty -Object $_ -Name 'name') -eq $expectedPrinter.queue } |
            Select-Object -First 1)[0]
        Assert-Condition ($null -ne $installedQueue) "Localized queue-name evidence omitted installed queue $($expectedPrinter.queue)."
        Assert-Condition ([bool](Get-ResultProperty -Object $installedQueue -Name 'installed')) "Localized queue-name evidence reported $($expectedPrinter.queue) as not installed."
    }
}

function Assert-PreferredInputFormatEvidence {
    param(
        [object] $Artifact
    )

    Assert-Condition ($null -ne $Artifact) 'Preferred input format evidence did not include an artifact.'

    $manifestPreferredFormats = @(Get-ResultProperty -Object $Artifact -Name 'manifestPreferredFormats')
    $observedRoutes = @(Get-ResultProperty -Object $Artifact -Name 'observedRoutes')
    Assert-SetEqual `
        -Actual @($manifestPreferredFormats | ForEach-Object { Get-ResultProperty -Object $_ -Name 'printerUri' }) `
        -Expected @($expectedVirtualPrinterDisplayNames | ForEach-Object { $_.printerUri }) `
        -Description 'Preferred input format manifest printer URIs'
    Assert-SetEqual `
        -Actual @($observedRoutes | ForEach-Object { Get-ResultProperty -Object $_ -Name 'queue' }) `
        -Expected $expectedQueues `
        -Description 'Preferred input format observed queue routes'

    foreach ($expectedPrinter in $expectedVirtualPrinterDisplayNames) {
        $manifestEntry = @($manifestPreferredFormats |
            Where-Object { [string](Get-ResultProperty -Object $_ -Name 'printerUri') -eq $expectedPrinter.printerUri } |
            Select-Object -First 1)[0]
        Assert-Condition ($null -ne $manifestEntry) "Preferred input format evidence omitted manifest printer URI $($expectedPrinter.printerUri)."
        Assert-Condition (
            [string](Get-ResultProperty -Object $manifestEntry -Name 'preferredInputFormat') -eq $expectedPrinter.preferredInputFormat) `
            "Manifest printer URI $($expectedPrinter.printerUri) did not use $($expectedPrinter.preferredInputFormat)."

        $observedRoute = Get-ResultByQueue -Results $observedRoutes -Queue $expectedPrinter.queue
        Assert-Condition ($null -ne $observedRoute) "Preferred input format evidence omitted observed route for $($expectedPrinter.queue)."
        $route = [string](Get-ResultProperty -Object $observedRoute -Name 'route')
        Assert-Condition (
            $route.StartsWith("$($expectedPrinter.preferredInputFormat) ->", [System.StringComparison]::Ordinal)) `
            "Observed route for $($expectedPrinter.queue) did not start with $($expectedPrinter.preferredInputFormat): $route"
    }
}

function Get-SupportedFormatKeys {
    param(
        [object[]] $Formats
    )

    return @($Formats | ForEach-Object {
        "$([string](Get-ResultProperty -Object $_ -Name 'type')):$([string](Get-ResultProperty -Object $_ -Name 'maxVersion'))"
    })
}

function Assert-SupportedFormatEvidence {
    param(
        [object[]] $ManifestSupportedFormats
    )

    Assert-SetEqual `
        -Actual @($ManifestSupportedFormats | ForEach-Object { Get-ResultProperty -Object $_ -Name 'printerUri' }) `
        -Expected @($expectedVirtualPrinterSupportedFormats | ForEach-Object { $_.printerUri }) `
        -Description 'SupportedFormat manifest printer URIs'

    foreach ($expectedPrinter in $expectedVirtualPrinterSupportedFormats) {
        $manifestEntry = @($ManifestSupportedFormats |
            Where-Object { [string](Get-ResultProperty -Object $_ -Name 'printerUri') -eq $expectedPrinter.printerUri } |
            Select-Object -First 1)[0]
        Assert-Condition ($null -ne $manifestEntry) "SupportedFormat evidence omitted manifest printer URI $($expectedPrinter.printerUri)."

        Assert-SetEqual `
            -Actual (Get-SupportedFormatKeys -Formats @(Get-ResultProperty -Object $manifestEntry -Name 'supportedFormats')) `
            -Expected (Get-SupportedFormatKeys -Formats @($expectedPrinter.formats)) `
            -Description "SupportedFormat declarations for $($expectedPrinter.printerUri)"
    }
}

function Assert-PassthroughFormatEvidence {
    param(
        [object] $Artifact
    )

    Assert-Condition ($null -ne $Artifact) 'Passthrough format evidence did not include an artifact.'

    $manifestSupportedFormats = @(Get-ResultProperty -Object $Artifact -Name 'manifestSupportedFormats')
    Assert-SupportedFormatEvidence -ManifestSupportedFormats $manifestSupportedFormats

    $observedCopyRoutes = Get-ResultProperty -Object $Artifact -Name 'observedCopyRoutes'
    Assert-Condition ($null -ne $observedCopyRoutes) 'Passthrough format evidence omitted observed copy routes.'

    $pdf = Get-ResultProperty -Object $observedCopyRoutes -Name 'pdf'
    Assert-CompletedJob -Result $pdf -Queue 'PDF passthrough feature evidence'
    Assert-SourceApplication -Result $pdf -ExpectedSourceApplication 'printsink-app.exe' -Description 'PDF passthrough feature evidence'
    Assert-Route -Result $pdf -ExpectedRoute 'application/pdf -> Pdf; Copy; Endpoint supports passthrough.' -Description 'PDF passthrough feature evidence'
    Assert-Document -Format 'pdf' -Path $pdf.outputPath -ExpectedBytes $pdf.bytes -Contains 'foo'
    Assert-FilesEqual -ExpectedPath $pdf.sourcePath -ActualPath $pdf.outputPath -Description 'PDF passthrough feature evidence output'

    $xps = Get-ResultProperty -Object $observedCopyRoutes -Name 'xps'
    Assert-CompletedJob -Result $xps -Queue 'XPS passthrough feature evidence'
    Assert-Route -Result $xps -ExpectedRoute 'application/oxps -> Oxps; Copy; Endpoint supports passthrough.' -Description 'XPS passthrough feature evidence'
    Assert-Document -Format 'oxps' -Path $xps.outputPath -ExpectedBytes $xps.bytes -Contains 'foo'

    $postScript = Get-ResultProperty -Object $observedCopyRoutes -Name 'postScript'
    Assert-CompletedJob -Result $postScript -Queue 'PostScript passthrough feature evidence'
    Assert-Route -Result $postScript -ExpectedRoute 'application/postscript -> PostScript; Copy; Endpoint supports passthrough.' -Description 'PostScript passthrough feature evidence'
    Assert-Document -Format 'postscript' -Path $postScript.outputPath -ExpectedBytes $postScript.bytes
}

function Assert-FileResultSummary {
    param(
        [object] $Result,
        [object] $Expected,
        [string] $Description
    )

    Assert-Condition ($null -ne $Result) "$Description omitted $($Expected.queue)."
    Assert-Condition ([string](Get-ResultProperty -Object $Result -Name 'queue') -eq $Expected.queue) "$Description reported the wrong queue for $($Expected.queue)."
    Assert-Condition ([string](Get-ResultProperty -Object $Result -Name 'format') -eq $Expected.format) "$Description reported the wrong format for $($Expected.queue)."
    Assert-Condition ([string](Get-ResultProperty -Object $Result -Name 'route') -eq $Expected.route) "$Description reported the wrong route for $($Expected.queue)."

    $outputPath = [string](Get-ResultProperty -Object $Result -Name 'outputPath')
    $bytes = [long](Get-ResultProperty -Object $Result -Name 'bytes')
    if ([string]::IsNullOrWhiteSpace([string]$Expected.contains)) {
        Assert-Document -Format $Expected.format -Path $outputPath -ExpectedBytes $bytes
    }
    else {
        Assert-Document -Format $Expected.format -Path $outputPath -ExpectedBytes $bytes -Contains ([string]$Expected.contains)
    }
}

function Assert-FilePrinterSaveAsEvidence {
    param(
        [object] $Artifact
    )

    Assert-Condition ($null -ne $Artifact) 'File-printer Save As evidence did not include an artifact.'

    $harness = @(Get-ResultProperty -Object $Artifact -Name 'harness')
    Assert-SetEqual `
        -Actual @($harness | ForEach-Object { Get-ResultProperty -Object $_ -Name 'queue' }) `
        -Expected @($expectedFileBackedOutputs | ForEach-Object { $_.queue }) `
        -Description 'File-printer Save As harness queue names'

    foreach ($expectedOutput in $expectedFileBackedOutputs) {
        $result = Get-ResultByQueue -Results $harness -Queue $expectedOutput.queue
        Assert-FileResultSummary -Result $result -Expected $expectedOutput -Description 'File-printer Save As harness output'
    }

    $notepad = Get-ResultProperty -Object $Artifact -Name 'notepad'
    Assert-Condition ([string](Get-ResultProperty -Object $notepad -Name 'mode') -eq 'notepad-command-line-print') 'File-printer Save As evidence did not use the Notepad command-line print path.'
    Assert-CompletedJob -Result $notepad -Queue 'Notepad Save As feature evidence'
    Assert-SourceApplication -Result $notepad -ExpectedSourceApplication 'notepad.exe' -Description 'Notepad Save As feature evidence'
    Assert-Route -Result $notepad -ExpectedRoute 'application/oxps -> Pdf; Convert; Convert XPS to PDF.' -Description 'Notepad Save As feature evidence'
    Assert-NonEmptyFile -Path ([string](Get-ResultProperty -Object $notepad -Name 'sourcePath'))
    Assert-Document -Format 'pdf' -Path $notepad.outputPath -ExpectedBytes $notepad.bytes -Contains 'foo'
}

function Assert-CloudSinkEvidence {
    param(
        [object] $Artifact
    )

    Assert-Condition ($null -ne $Artifact) 'Cloud sink evidence did not include an artifact.'
    Assert-Condition ([string](Get-ResultProperty -Object $Artifact -Name 'queue') -eq 'PrintSink - Cloud') 'Cloud sink evidence reported the wrong queue.'
    Assert-Condition ([string](Get-ResultProperty -Object $Artifact -Name 'format') -eq 'cloud') 'Cloud sink evidence reported the wrong format.'
    Assert-CompletedJob -Result $Artifact -Queue 'Cloud sink feature evidence'
    Assert-SourceApplication -Result $Artifact -ExpectedSourceApplication 'powershell.exe' -Description 'Cloud sink feature evidence'
    Assert-Route -Result $Artifact -ExpectedRoute 'application/oxps -> Pdf; Convert; Convert XPS to PDF.' -Description 'Cloud sink feature evidence'
    Assert-Condition ([string]::IsNullOrWhiteSpace([string](Get-ResultProperty -Object $Artifact -Name 'outputPath'))) 'Cloud sink evidence unexpectedly reported a Save-As output path.'
    Assert-Condition ([long](Get-ResultProperty -Object $Artifact -Name 'bytes') -eq 0) 'Cloud sink evidence unexpectedly reported file-backed bytes.'

    $sinkArtifact = Get-ResultProperty -Object $Artifact -Name 'sinkArtifact'
    Assert-Condition ($null -ne $sinkArtifact) 'Cloud sink evidence omitted the package-local sink artifact.'
    Assert-Condition ([string](Get-ResultProperty -Object $sinkArtifact -Name 'contentType') -eq 'application/pdf') 'Cloud sink artifact content type was not application/pdf.'
    Assert-Condition (-not [string]::IsNullOrWhiteSpace([string](Get-ResultProperty -Object $sinkArtifact -Name 'path'))) 'Cloud sink artifact omitted the package-local source path.'
    Assert-Document `
        -Format 'pdf' `
        -Path ([string](Get-ResultProperty -Object $sinkArtifact -Name 'artifactCopyPath')) `
        -ExpectedBytes ([long](Get-ResultProperty -Object $sinkArtifact -Name 'bytes')) `
        -Contains 'foo'
}

function Assert-ConvertedOutputEvidence {
    param(
        [object] $Artifact
    )

    Assert-Condition ($null -ne $Artifact) 'Conversion evidence did not include an artifact.'
    $outputs = @(Get-ResultProperty -Object $Artifact -Name 'outputs')
    Assert-SetEqual `
        -Actual @($outputs | ForEach-Object { Get-ResultProperty -Object $_ -Name 'queue' }) `
        -Expected @($expectedConvertedOutputs | ForEach-Object { $_.queue }) `
        -Description 'Conversion feature output queue names'

    foreach ($expectedOutput in $expectedConvertedOutputs) {
        $result = Get-ResultByQueue -Results $outputs -Queue $expectedOutput.queue
        Assert-FileResultSummary -Result $result -Expected $expectedOutput -Description 'Conversion feature output'
        Assert-Condition ([string](Get-ResultProperty -Object $result -Name 'sourceApplication') -eq 'powershell.exe') "Conversion feature evidence reported the wrong source application for $($expectedOutput.queue)."
    }
}

function Assert-XpsCopyEvidence {
    param(
        [object[]] $Artifact
    )

    Assert-Condition ($Artifact.Count -eq 1) 'XPS copy evidence must include exactly one output artifact.'
    $result = Get-ResultByQueue -Results $Artifact -Queue 'PrintSink - XPS'
    Assert-FileResultSummary -Result $result -Expected $expectedXpsCopyOutput -Description 'XPS copy feature output'
    Assert-Condition ([string](Get-ResultProperty -Object $result -Name 'sourceApplication') -eq 'powershell.exe') 'XPS copy feature evidence reported the wrong source application.'
}

function Assert-PdfWatermarkResult {
    param(
        [object] $Result,
        [string] $Description,
        [string] $Contains,
        [string] $NotContains = '',
        [switch] $RequiresImage,
        [string] $ExpectedMode = ''
    )

    Assert-CompletedJob -Result $Result -Queue $Description
    Assert-SourceApplication -Result $Result -ExpectedSourceApplication 'powershell.exe' -Description $Description
    Assert-Route -Result $Result -ExpectedRoute 'application/oxps -> Pdf; Convert; Convert XPS to PDF.' -Description $Description
    Assert-Condition ([string](Get-ResultProperty -Object $Result -Name 'queue') -eq 'PrintSink - PDF') "$Description did not target the PDF queue."
    Assert-Condition ([string](Get-ResultProperty -Object $Result -Name 'format') -eq 'pdf') "$Description did not produce PDF output."

    if (-not [string]::IsNullOrWhiteSpace($ExpectedMode)) {
        Assert-Condition ([string](Get-ResultProperty -Object $Result -Name 'mode') -eq $ExpectedMode) "$Description did not report mode $ExpectedMode."
    }

    if ($RequiresImage) {
        Assert-Document `
            -Format 'pdf' `
            -Path ([string](Get-ResultProperty -Object $Result -Name 'outputPath')) `
            -ExpectedBytes ([long](Get-ResultProperty -Object $Result -Name 'bytes')) `
            -Contains $Contains `
            -NotContains $NotContains `
            -RequiresImage
    }
    else {
        Assert-Document `
            -Format 'pdf' `
            -Path ([string](Get-ResultProperty -Object $Result -Name 'outputPath')) `
            -ExpectedBytes ([long](Get-ResultProperty -Object $Result -Name 'bytes')) `
            -Contains $Contains `
            -NotContains $NotContains
    }
}

function Assert-WatermarkEvidence {
    param(
        [object] $Artifact
    )

    Assert-Condition ($null -ne $Artifact) 'Watermark evidence did not include an artifact.'

    $settingsText = Get-ResultProperty -Object $Artifact -Name 'settingsText'
    Assert-PdfWatermarkResult `
        -Result $settingsText `
        -Description 'Default text watermark feature evidence' `
        -Contains 'CI DEFAULT WATERMARK'

    $settingsImage = Get-ResultProperty -Object $Artifact -Name 'settingsImage'
    Assert-PdfWatermarkResult `
        -Result $settingsImage `
        -Description 'Default image watermark feature evidence' `
        -Contains 'foo' `
        -RequiresImage

    $jobUiText = Get-ResultProperty -Object $Artifact -Name 'jobUiText'
    Assert-PdfWatermarkResult `
        -Result $jobUiText `
        -Description 'Job UI text watermark feature evidence' `
        -Contains 'CI WATERMARK' `
        -NotContains 'ci-password' `
        -ExpectedMode 'job-ui-watermark'

    $jobUiPdl = Get-ResultProperty -Object $jobUiText -Name 'jobUiPdl'
    Assert-Condition ($null -ne $jobUiPdl) 'Watermark evidence omitted Job UI PDL metadata.'
    Assert-Condition ([string](Get-ResultProperty -Object $jobUiPdl -Name 'message') -eq 'Job UI PDL received') 'Watermark evidence did not record Job UI PDL receipt.'
    Assert-DetailContainsParts `
        -Detail ([string](Get-ResultProperty -Object $jobUiPdl -Name 'detail')) `
        -ExpectedParts @(
            'kind=virtual-printer',
            'jobTitle=PrintSink E2E Job UI Watermark',
            'source=powershell.exe',
            'contentType=application/oxps') `
        -Description 'Watermark Job UI PDL evidence'
}

function Assert-JobUiPreviewEvidence {
    param(
        [object] $Artifact
    )

    Assert-Condition ($null -ne $Artifact) 'Job UI preview evidence did not include an artifact.'
    Assert-PdfWatermarkResult `
        -Result $Artifact `
        -Description 'Job UI preview feature evidence' `
        -Contains 'CI WATERMARK' `
        -NotContains 'ci-password' `
        -ExpectedMode 'job-ui-watermark'

    Assert-Condition ([string](Get-ResultProperty -Object $Artifact -Name 'documentName') -eq 'PrintSink E2E Job UI Watermark') 'Job UI preview evidence reported the wrong document name.'
    Assert-Condition ([string](Get-ResultProperty -Object $Artifact -Name 'jobUiWindowTitle') -eq 'Job preview') 'Job UI preview evidence reported the wrong window title.'
    Assert-Condition ([bool](Get-ResultProperty -Object $Artifact -Name 'saveAsDialogObserved')) 'Job UI preview evidence did not prove the Save As dialog was observed.'
    Assert-Condition ([bool](Get-ResultProperty -Object $Artifact -Name 'watermarkToggleSet')) 'Job UI preview evidence did not prove the watermark toggle was set.'
    Assert-Condition ([string](Get-ResultProperty -Object $Artifact -Name 'watermarkText') -eq 'CI WATERMARK') 'Job UI preview evidence reported the wrong watermark text.'
    Assert-Condition ([bool](Get-ResultProperty -Object $Artifact -Name 'jobPasswordFieldUsed')) 'Job UI preview evidence did not prove the job-password field was used.'
    Assert-Condition ([bool](Get-ResultProperty -Object $Artifact -Name 'continueInvoked')) 'Job UI preview evidence did not prove Continue was invoked.'
    Assert-Condition ([bool](Get-ResultProperty -Object $Artifact -Name 'renderErrorAbsent')) 'Job UI preview evidence did not prove the Reactor surface rendered without error.'
    Assert-Condition ([string](Get-ResultProperty -Object $Artifact -Name 'jobPassword') -eq 'present-not-applicable') 'Job UI preview evidence did not record non-applicable password metadata.'
    Assert-Condition (-not [bool](Get-ResultProperty -Object $Artifact -Name 'jobPasswordSecretExposed')) 'Job UI preview evidence exposed the job-password secret.'

    $jobUiPdl = Get-ResultProperty -Object $Artifact -Name 'jobUiPdl'
    Assert-Condition ($null -ne $jobUiPdl) 'Job UI preview evidence omitted PDL metadata.'
    Assert-Condition ([string](Get-ResultProperty -Object $jobUiPdl -Name 'message') -eq 'Job UI PDL received') 'Job UI preview evidence did not record PDL receipt.'
    Assert-DetailContainsParts `
        -Detail ([string](Get-ResultProperty -Object $jobUiPdl -Name 'detail')) `
        -ExpectedParts @(
            'kind=virtual-printer',
            'jobTitle=PrintSink E2E Job UI Watermark',
            'source=powershell.exe',
            'contentType=application/oxps') `
        -Description 'Job UI preview PDL evidence'

    $diagnostic = Get-ResultProperty -Object $Artifact -Name 'diagnostic'
    Assert-Condition ([string](Get-ResultProperty -Object $diagnostic -Name 'detail') -like '*job-password=present-not-applicable*') 'Job UI preview evidence did not prove password metadata was consumed.'
    Assert-Condition ([string](Get-ResultProperty -Object $diagnostic -Name 'detail') -notlike '*ci-password*') 'Job UI preview evidence leaked the job-password secret in diagnostics.'
}

function Assert-PrintTicketValidationEvidence {
    param(
        [object[]] $Artifact
    )

    Assert-SetEqual `
        -Actual @($Artifact | ForEach-Object { Get-ResultProperty -Object $_ -Name 'queue' }) `
        -Expected $expectedQueues `
        -Description 'Print-ticket validation feature queue names'

    foreach ($queue in $expectedQueues) {
        $result = Get-ResultByQueue -Results $Artifact -Queue $queue
        Assert-Condition ($null -ne $result) "Print-ticket validation evidence omitted $queue."
        Assert-Condition (-not [string]::IsNullOrWhiteSpace([string](Get-ResultProperty -Object $result -Name 'documentName'))) "Print-ticket validation evidence omitted document name for $queue."
        $ticketValidation = Get-ResultProperty -Object $result -Name 'ticketValidation'
        Assert-Condition ($null -ne $ticketValidation) "Print-ticket validation evidence omitted the ticket validation diagnostic for $queue."
        Assert-Condition ([string](Get-ResultProperty -Object $ticketValidation -Name 'source') -eq 'PrintSupportExtensionBackgroundTask') "Print-ticket validation evidence used the wrong source for $queue."
        Assert-Condition ([string](Get-ResultProperty -Object $ticketValidation -Name 'message') -eq 'Print ticket validated') "Print-ticket validation evidence used the wrong message for $queue."
        Assert-Condition ([string](Get-ResultProperty -Object $ticketValidation -Name 'endpoint') -eq $queue) "Print-ticket validation evidence used the wrong endpoint for $queue."
        Assert-Condition ([string](Get-ResultProperty -Object $ticketValidation -Name 'detail') -eq 'status=Resolved') "Print-ticket validation evidence did not resolve $queue."
        Get-ResultTimestamp -Result $ticketValidation -Description "Print-ticket validation evidence for $queue" | Out-Null
    }
}

function Assert-PdcFeatureEvidence {
    param(
        [object] $Artifact
    )

    Assert-ExtensionCapabilities -ExtensionCapabilities $Artifact
    $detail = [string](Get-ResultProperty -Object $Artifact -Name 'detail')
    Assert-Condition ($detail -like '*features=PageMediaSize,PageMediaType,JobInputBin,JobOutputBin,JobPageOrder,JobStapleAllDocuments,PageResolution,JobWatermarkMode*') 'PDC feature evidence did not report the applied feature list.'
    Assert-Condition ($detail -like "*$expectedPdcFeatureDetail*") 'PDC feature evidence did not report the PDC feature list.'
    Assert-Condition ($detail -like "*$expectedPdcOptionDetail*") 'PDC feature evidence did not report the PDC option list.'
}

function Assert-PdrFeatureEvidence {
    param(
        [object] $Artifact
    )

    Assert-ExtensionCapabilities -ExtensionCapabilities $Artifact
    $detail = [string](Get-ResultProperty -Object $Artifact -Name 'detail')
    Assert-Condition ($detail -like '*pdr=updated*') 'PDR feature evidence did not report a PDR update.'
    Assert-Condition ($detail -like '*pdrResources=13*') 'PDR feature evidence did not report the expected resource count.'
    Assert-Condition ($detail -like "*$expectedPdrResourceDetail*") 'PDR feature evidence did not report the localized resource names.'
}

function Assert-CapabilityRefreshEvidence {
    param(
        [object] $Artifact
    )

    Assert-Condition ($null -ne $Artifact) 'Capability-refresh evidence did not include an artifact.'

    $command = Get-ResultProperty -Object $Artifact -Name 'command'
    Assert-ExtensionCapabilities -ExtensionCapabilities $command

    $managementUi = Get-ResultProperty -Object $Artifact -Name 'managementUi'
    Assert-Condition ($null -ne $managementUi) 'Capability-refresh evidence omitted management UI evidence.'
    $requestStartedUtc = [string](Get-ResultProperty -Object $managementUi -Name 'requestStartedUtc')
    $requestTimestamp = [DateTimeOffset]::MinValue
    Assert-Condition ([DateTimeOffset]::TryParse($requestStartedUtc, [ref]$requestTimestamp)) "Capability-refresh evidence had an invalid request timestamp: $requestStartedUtc"

    $completion = Get-ResultProperty -Object $managementUi -Name 'completion'
    Assert-Condition ([string](Get-ResultProperty -Object $completion -Name 'source') -eq 'ManagementScreen') 'Capability-refresh evidence completion came from the wrong source.'
    Assert-Condition ([string](Get-ResultProperty -Object $completion -Name 'message') -eq 'Management UI capabilities refreshed') 'Capability-refresh evidence did not record management completion.'
    Assert-Condition ([string](Get-ResultProperty -Object $completion -Name 'endpoint') -eq 'PrintSink - PDF') 'Capability-refresh evidence completion targeted the wrong endpoint.'
    Assert-Condition ([string](Get-ResultProperty -Object $completion -Name 'detail') -like '*Capabilities refreshed for PrintSink - PDF*') 'Capability-refresh evidence completion omitted the PDF queue.'

    $extension = Get-ResultProperty -Object $managementUi -Name 'extension'
    Assert-ExtensionCapabilities -ExtensionCapabilities $extension
    Assert-ResultTimestampIsNotBefore `
        -Later $extension `
        -Earlier $managementUi `
        -Description 'Capability-refresh feature extension diagnostic' `
        -EarlierTimestampName 'requestStartedUtc'
}

function Assert-UserDefaultPrintTicketEvidence {
    param(
        [object] $Artifact
    )

    Assert-Condition ($null -ne $Artifact) 'User-default print-ticket evidence did not include an artifact.'

    $command = Get-ResultProperty -Object $Artifact -Name 'command'
    $managementUi = Get-ResultProperty -Object $Artifact -Name 'managementUi'
    Assert-UserDefaultPrintTicketDiagnostic `
        -Diagnostic (Get-ResultProperty -Object $command -Name 'set') `
        -ExpectedSource 'VirtualPrinterCommandLine' `
        -ExpectedCopies 2 `
        -Description 'command set'
    Assert-UserDefaultPrintTicketDiagnostic `
        -Diagnostic (Get-ResultProperty -Object $command -Name 'restore') `
        -ExpectedSource 'VirtualPrinterCommandLine' `
        -ExpectedCopies 1 `
        -Description 'command restore'
    Assert-UserDefaultPrintTicketDiagnostic `
        -Diagnostic (Get-ResultProperty -Object $managementUi -Name 'set') `
        -ExpectedSource 'ManagementScreen' `
        -ExpectedCopies 2 `
        -Description 'management UI set'
    Assert-UserDefaultPrintTicketDiagnostic `
        -Diagnostic (Get-ResultProperty -Object $managementUi -Name 'restore') `
        -ExpectedSource 'ManagementScreen' `
        -ExpectedCopies 1 `
        -Description 'management UI restore'
}

function Assert-UserDefaultPrintTicketDiagnostic {
    param(
        [object] $Diagnostic,
        [string] $ExpectedSource,
        [int] $ExpectedCopies,
        [string] $Description
    )

    Assert-Condition ($null -ne $Diagnostic) "User-default print-ticket evidence omitted $Description."
    Assert-Condition ([string](Get-ResultProperty -Object $Diagnostic -Name 'source') -eq $ExpectedSource) "User-default print-ticket $Description used the wrong source."
    $expectedMessage = if ($ExpectedSource -eq 'ManagementScreen') {
        'Management UI default copies updated'
    }
    else {
        'User default print ticket updated'
    }
    Assert-Condition ([string](Get-ResultProperty -Object $Diagnostic -Name 'message') -eq $expectedMessage) "User-default print-ticket $Description used the wrong message."
    Assert-Condition ([string](Get-ResultProperty -Object $Diagnostic -Name 'endpoint') -eq 'PrintSink - PDF') "User-default print-ticket $Description used the wrong endpoint."
    $detail = [string](Get-ResultProperty -Object $Diagnostic -Name 'detail')
    Assert-Condition ($detail -like "*User default print ticket updated for PrintSink - PDF*") "User-default print-ticket $Description omitted the PDF queue."
    Assert-Condition ($detail -like "*copies=$ExpectedCopies*") "User-default print-ticket $Description did not request $ExpectedCopies copies."
    Assert-Condition ($detail -like "*verifiedCopies=$ExpectedCopies*") "User-default print-ticket $Description did not verify $ExpectedCopies copies."
    Get-ResultTimestamp -Result $Diagnostic -Description "User-default print-ticket $Description" | Out-Null
}

function Assert-MxdcFeatureEvidence {
    param(
        [object] $Artifact
    )

    Assert-ExtensionCapabilities -ExtensionCapabilities $Artifact
    $detail = [string](Get-ResultProperty -Object $Artifact -Name 'detail')
    Assert-Condition ($detail -like '*mxdc=configured*') 'MXDC feature evidence did not report MXDC configuration.'
    Assert-Condition ($detail -like "*$expectedMxdcQualityDetail*") 'MXDC feature evidence did not report the full output-quality mapping.'
}

function Assert-IppAssociationEvidence {
    param(
        [object] $Artifact
    )

    Assert-Condition ($null -ne $Artifact) 'IPP association evidence did not include an artifact.'
    $printer = [string](Get-ResultProperty -Object $Artifact -Name 'printer')
    Assert-Condition ($printer.StartsWith('PrintSink-E2E-IPP-', [System.StringComparison]::Ordinal)) "IPP association evidence used an unexpected printer name: $printer"
    Assert-Condition ([string](Get-ResultProperty -Object $Artifact -Name 'hardwareId') -eq 'PSA_PrintSinkE2E_IPP_Pri21CF') 'IPP association evidence used the wrong hardware ID.'
    Assert-Condition (-not [string]::IsNullOrWhiteSpace([string](Get-ResultProperty -Object $Artifact -Name 'ippHost'))) 'IPP association evidence omitted the IPP host.'
    Assert-Condition ([string](Get-ResultProperty -Object $Artifact -Name 'aumid') -like 'PrintSink_*!App') 'IPP association evidence omitted the packaged app AUMID.'
    Assert-Condition (-not [string]::IsNullOrWhiteSpace([string](Get-ResultProperty -Object $Artifact -Name 'deviceInstanceId'))) 'IPP association evidence omitted the device instance ID.'
    Assert-Condition (-not [string]::IsNullOrWhiteSpace([string](Get-ResultProperty -Object $Artifact -Name 'publishedDriver'))) 'IPP association evidence omitted the published driver name.'
    Assert-Condition ([string](Get-ResultProperty -Object $Artifact -Name 'certificateThumbprint') -match '^[0-9A-Fa-f]{40}$') 'IPP association evidence omitted the driver-signing certificate thumbprint.'
    Assert-NonEmptyFile -Path ([string](Get-ResultProperty -Object $Artifact -Name 'ippEvidencePath'))
    Assert-Condition ([int](Get-ResultProperty -Object $Artifact -Name 'ippRequestCount') -gt 0) 'IPP association evidence did not record IPP requests.'
    Assert-SetEqual `
        -Actual @(Get-ResultProperty -Object $Artifact -Name 'ippOperations') `
        -Expected @('GetPrinterAttributes') `
        -Description 'IPP association operations'
    Assert-Condition ([int](Get-ResultProperty -Object $Artifact -Name 'ippJobCount') -eq 0) 'IPP association evidence unexpectedly recorded IPP jobs for the association probe.'

    $stateProbe = Get-ResultProperty -Object $Artifact -Name 'printerStateProbe'
    Assert-Condition ($null -ne $stateProbe) 'IPP association evidence omitted printer-state probe evidence.'
    Assert-Condition ([string](Get-ResultProperty -Object $stateProbe -Name 'printer') -like 'PrintSink-E2E-IPP-State-*') 'IPP state probe used the wrong printer name.'
    Assert-NonEmptyFile -Path ([string](Get-ResultProperty -Object $stateProbe -Name 'ippEvidencePath'))
    Assert-Condition ([int](Get-ResultProperty -Object $stateProbe -Name 'ippRequestCount') -gt 0) 'IPP state probe did not record IPP requests.'
    Assert-SetEqual -Actual @(Get-ResultProperty -Object $stateProbe -Name 'state') -Expected @('5') -Description 'IPP state probe printer-state'
    Assert-SetEqual -Actual @(Get-ResultProperty -Object $stateProbe -Name 'stateReasons') -Expected @('paused') -Description 'IPP state probe printer-state-reasons'
    Assert-SetEqual -Actual @(Get-ResultProperty -Object $stateProbe -Name 'acceptingJobs') -Expected @('False') -Description 'IPP state probe printer-is-accepting-jobs'

    $ticketValidation = Get-ResultProperty -Object $Artifact -Name 'ticketValidation'
    Assert-Condition ([string](Get-ResultProperty -Object $ticketValidation -Name 'source') -eq 'PrintSupportExtensionBackgroundTask') 'IPP association ticket validation used the wrong source.'
    Assert-Condition ([string](Get-ResultProperty -Object $ticketValidation -Name 'message') -eq 'Print ticket validated') 'IPP association did not validate a print ticket.'
    Assert-Condition ([string](Get-ResultProperty -Object $ticketValidation -Name 'endpoint') -eq $printer) 'IPP association ticket validation targeted the wrong endpoint.'
    Assert-Condition ([string](Get-ResultProperty -Object $ticketValidation -Name 'detail') -eq 'status=Resolved') 'IPP association ticket validation did not resolve.'
    Get-ResultTimestamp -Result $ticketValidation -Description 'IPP association ticket validation' | Out-Null

    $workflowActivationPrint = Get-ResultProperty -Object $Artifact -Name 'workflowActivationPrint'
    Assert-Condition ([string](Get-ResultProperty -Object $workflowActivationPrint -Name 'printer') -eq $printer) 'IPP workflow activation used the wrong printer.'
    Assert-Condition ([string](Get-ResultProperty -Object $workflowActivationPrint -Name 'sourceApplication') -eq 'powershell.exe') 'IPP workflow activation used the wrong source application.'
    Assert-Condition ([string](Get-ResultProperty -Object $workflowActivationPrint -Name 'documentName') -eq 'PrintSink E2E IPP Workflow') 'IPP workflow activation used the wrong document name.'
    Assert-IppWorkflowStartEvidence -Artifact (Get-ResultProperty -Object $workflowActivationPrint -Name 'workflowStart')

    $workflowStatus = [string](Get-ResultProperty -Object $workflowActivationPrint -Name 'workflowStatus')
    Assert-Condition ($workflowStatus -in @('pdl-modification-delivered', 'pdl-modification-not-delivered')) "IPP workflow activation reported unexpected status: $workflowStatus"
    $workflow = Get-ResultProperty -Object $workflowActivationPrint -Name 'workflow'
    if ($workflowStatus -eq 'pdl-modification-delivered') {
        Assert-Condition ($null -ne $workflow) 'IPP workflow activation reported delivered status without workflow evidence.'
        Assert-Condition ([string](Get-ResultProperty -Object $workflow -Name 'source') -eq 'PrintSupportWorkflowBackgroundTask') 'IPP workflow evidence used the wrong source.'
        Assert-Condition ([string](Get-ResultProperty -Object $workflow -Name 'message') -eq 'Workflow job passed through') 'IPP workflow evidence did not pass through the job.'
        Assert-Condition ([string](Get-ResultProperty -Object $workflow -Name 'endpoint') -eq $printer) 'IPP workflow evidence targeted the wrong endpoint.'
        Assert-DetailContainsParts `
            -Detail ([string](Get-ResultProperty -Object $workflow -Name 'detail')) `
            -ExpectedParts @('source=application/pdf', 'target=system', 'job-password=absent', 'passthroughWithAttributes=') `
            -Description 'IPP workflow pass-through evidence'
    }
    else {
        Assert-Condition (-not [string]::IsNullOrWhiteSpace([string](Get-ResultProperty -Object $workflowActivationPrint -Name 'workflowDetail'))) 'IPP workflow non-delivery evidence omitted the failure detail.'
    }

    $printServiceEvents = @(Get-ResultProperty -Object $workflowActivationPrint -Name 'printServiceEvents')
    Assert-Condition ($printServiceEvents.Count -gt 0) 'IPP workflow activation omitted PrintService events.'
    Assert-Condition (@($printServiceEvents | Where-Object { [int](Get-ResultProperty -Object $_ -Name 'id') -eq 300 }).Count -gt 0) 'IPP workflow activation did not include the PrintService printer-created event.'
}

function Assert-VirtualPrinterAttributeReadEvidence {
    param(
        [object] $Artifact
    )

    Assert-Condition ($null -ne $Artifact) 'Virtual-printer attribute-read evidence did not include an artifact.'
    Assert-Condition ([string](Get-ResultProperty -Object $Artifact -Name 'source') -eq 'VirtualPrinterCommandLine') 'Virtual-printer attribute-read evidence used the wrong source.'
    Assert-Condition ([string](Get-ResultProperty -Object $Artifact -Name 'message') -eq 'Virtual printer attribute read matched platform behavior') 'Virtual-printer attribute-read evidence used the wrong message.'
    Assert-Condition ([string](Get-ResultProperty -Object $Artifact -Name 'endpoint') -eq 'PrintSink - PDF') 'Virtual-printer attribute-read evidence targeted the wrong endpoint.'
    Assert-DetailContainsParts `
        -Detail ([string](Get-ResultProperty -Object $Artifact -Name 'detail')) `
        -ExpectedParts @(
            'Virtual printer attribute read matched platform behavior for PrintSink - PDF',
            'document-format-default=<unsupported>',
            'document-format-supported=<unsupported>') `
        -Description 'Virtual-printer attribute-read evidence'
    Get-ResultTimestamp -Result $Artifact -Description 'Virtual-printer attribute-read evidence' | Out-Null
}

function Assert-ConcurrentPrintEvidence {
    param(
        [object] $Artifact
    )

    Assert-Condition ($null -ne $Artifact) 'Concurrent print evidence did not include an artifact.'
    Get-ResultTimestamp -Result $Artifact -Description 'Concurrent print evidence' -Name 'startedUtc' | Out-Null
    Assert-Condition ([bool](Get-ResultProperty -Object $Artifact -Name 'overlapped')) 'Concurrent print evidence did not report overlapping jobs.'
    $jobs = @(Get-ResultProperty -Object $Artifact -Name 'jobs')
    Assert-SetEqual `
        -Actual @($jobs | ForEach-Object { Get-ResultProperty -Object $_ -Name 'queue' }) `
        -Expected @('PrintSink - PCLm', 'PrintSink - Cloud') `
        -Description 'Concurrent print queues'

    $pclm = Get-ResultByQueue -Results $jobs -Queue 'PrintSink - PCLm'
    Assert-CompletedJob -Result $pclm -Queue 'Concurrent PCLm feature evidence'
    Assert-SourceApplication -Result $pclm -ExpectedSourceApplication 'powershell.exe' -Description 'Concurrent PCLm feature evidence'
    Assert-Route -Result $pclm -ExpectedRoute 'application/oxps -> Pclm; Convert; Convert XPS to PCLm.' -Description 'Concurrent PCLm feature evidence'
    Assert-Condition ([int](Get-ResultProperty -Object $pclm -Name 'pageCount') -eq 48) 'Concurrent PCLm feature evidence reported the wrong page count.'
    Assert-Document -Format 'pclm' -Path $pclm.outputPath -ExpectedBytes $pclm.bytes

    $cloud = Get-ResultByQueue -Results $jobs -Queue 'PrintSink - Cloud'
    Assert-CompletedJob -Result $cloud -Queue 'Concurrent cloud feature evidence'
    Assert-SourceApplication -Result $cloud -ExpectedSourceApplication 'powershell.exe' -Description 'Concurrent cloud feature evidence'
    Assert-Route -Result $cloud -ExpectedRoute 'application/oxps -> Pdf; Convert; Convert XPS to PDF.' -Description 'Concurrent cloud feature evidence'
    Assert-Condition ([int](Get-ResultProperty -Object $cloud -Name 'pageCount') -eq 96) 'Concurrent cloud feature evidence reported the wrong page count.'
    Assert-Condition ([string]::IsNullOrWhiteSpace([string](Get-ResultProperty -Object $cloud -Name 'outputPath'))) 'Concurrent cloud feature evidence unexpectedly reported a Save-As output path.'
    Assert-Condition ([long](Get-ResultProperty -Object $cloud -Name 'bytes') -eq 0) 'Concurrent cloud feature evidence unexpectedly reported file-backed bytes.'
    $sinkArtifact = Get-ResultProperty -Object $cloud -Name 'sinkArtifact'
    Assert-Condition ([string](Get-ResultProperty -Object $sinkArtifact -Name 'contentType') -eq 'application/pdf') 'Concurrent cloud sink artifact reported the wrong content type.'
    Assert-Document -Format 'pdf' -Path $sinkArtifact.artifactCopyPath -ExpectedBytes $sinkArtifact.bytes -Contains 'foo concurrent cloud'
}

function Assert-GracefulCancelAndFailEvidence {
    param(
        [object] $Artifact
    )

    Assert-Condition ($null -ne $Artifact) 'Graceful cancel/fail evidence did not include an artifact.'

    $failed = Get-ResultProperty -Object $Artifact -Name 'failed'
    Assert-Condition ([string](Get-ResultProperty -Object $failed -Name 'queue') -eq 'PrintSink - PDF') 'Failed-job evidence targeted the wrong queue.'
    Assert-Condition ([string](Get-ResultProperty -Object $failed -Name 'sourceApplication') -eq 'powershell.exe') 'Failed-job evidence used the wrong source application.'
    Assert-Condition ([string](Get-ResultProperty -Object $failed -Name 'documentName') -eq 'PrintSink E2E Failed Image Watermark') 'Failed-job evidence used the wrong document name.'
    Assert-Condition ([string](Get-ResultProperty -Object $failed -Name 'mode') -eq 'failed-image-watermark') 'Failed-job evidence used the wrong mode.'
    Assert-Condition ([long](Get-ResultProperty -Object $failed -Name 'bytes') -eq 0) 'Failed-job evidence reported non-zero bytes.'
    Assert-EmptyOrMissingFile -Path ([string](Get-ResultProperty -Object $failed -Name 'outputPath')) -Description 'Failed image watermark feature evidence'
    $failedDiagnostic = Get-ResultProperty -Object $failed -Name 'diagnostic'
    Assert-Condition ([string](Get-ResultProperty -Object $failedDiagnostic -Name 'message') -eq 'Job failed') 'Failed-job evidence did not report Job failed.'
    Assert-DetailContainsParts `
        -Detail ([string](Get-ResultProperty -Object $failedDiagnostic -Name 'detail')) `
        -ExpectedParts @('COMException', '0x88982F07', 'route=application/oxps -> Pdf; Convert; Convert XPS to PDF.') `
        -Description 'Failed-job evidence'

    $canceled = Get-ResultProperty -Object $Artifact -Name 'canceled'
    Assert-Condition ([string](Get-ResultProperty -Object $canceled -Name 'queue') -eq 'PrintSink - PDF') 'Canceled-job evidence targeted the wrong queue.'
    Assert-Condition ([string](Get-ResultProperty -Object $canceled -Name 'sourceApplication') -eq 'powershell.exe') 'Canceled-job evidence used the wrong source application.'
    Assert-Condition ([string](Get-ResultProperty -Object $canceled -Name 'documentName') -eq 'PrintSink E2E Job UI Cancel') 'Canceled-job evidence used the wrong document name.'
    Assert-Condition ([string](Get-ResultProperty -Object $canceled -Name 'mode') -eq 'job-ui-cancel') 'Canceled-job evidence used the wrong mode.'
    Assert-Condition ([long](Get-ResultProperty -Object $canceled -Name 'bytes') -eq 0) 'Canceled-job evidence reported non-zero bytes.'
    Assert-EmptyOrMissingFile -Path ([string](Get-ResultProperty -Object $canceled -Name 'outputPath')) -Description 'Job UI cancel feature evidence'
    $jobUiPdl = Get-ResultProperty -Object $canceled -Name 'jobUiPdl'
    Assert-Condition ([string](Get-ResultProperty -Object $jobUiPdl -Name 'source') -eq 'JobPreviewScreen') 'Canceled-job evidence did not come through JobPreviewScreen.'
    Assert-Condition ([string](Get-ResultProperty -Object $jobUiPdl -Name 'message') -eq 'Job UI PDL received') 'Canceled-job evidence omitted Job UI PDL receipt.'
    Assert-DetailContainsParts `
        -Detail ([string](Get-ResultProperty -Object $jobUiPdl -Name 'detail')) `
        -ExpectedParts @('kind=virtual-printer', 'jobTitle=PrintSink E2E Job UI Cancel', 'source=powershell.exe', 'contentType=application/oxps') `
        -Description 'Canceled-job PDL evidence'
    $canceledDiagnostic = Get-ResultProperty -Object $canceled -Name 'diagnostic'
    Assert-Condition ([string](Get-ResultProperty -Object $canceledDiagnostic -Name 'source') -eq 'VirtualPrinterBackgroundTask') 'Canceled-job evidence used the wrong diagnostic source.'
    Assert-Condition ([string](Get-ResultProperty -Object $canceledDiagnostic -Name 'message') -eq 'Job canceled') 'Canceled-job evidence did not report Job canceled.'
    Assert-Condition ([string](Get-ResultProperty -Object $canceledDiagnostic -Name 'endpoint') -eq 'PrintSink - PDF') 'Canceled-job evidence used the wrong endpoint.'
    Assert-Condition ([string](Get-ResultProperty -Object $canceledDiagnostic -Name 'detail') -eq 'User canceled from Job UI.') 'Canceled-job evidence used the wrong diagnostic detail.'
}

function Assert-JobPasswordEvidence {
    param(
        [object] $Artifact
    )

    Assert-Condition ($null -ne $Artifact) 'Job-password evidence did not include an artifact.'
    Assert-Condition ([string](Get-ResultProperty -Object $Artifact -Name 'queue') -eq 'PrintSink - PDF') 'Job-password evidence targeted the wrong queue.'
    Assert-Condition ([string](Get-ResultProperty -Object $Artifact -Name 'mode') -eq 'job-ui-watermark') 'Job-password evidence used the wrong mode.'
    Assert-Condition ([string](Get-ResultProperty -Object $Artifact -Name 'jobPassword') -eq 'present-not-applicable') 'Job-password evidence did not record present-not-applicable.'
    Assert-Condition (-not [bool](Get-ResultProperty -Object $Artifact -Name 'jobPasswordSecretExposed')) 'Job-password evidence exposed the secret.'
    $diagnostic = Get-ResultProperty -Object $Artifact -Name 'diagnostic'
    Assert-Condition ([string](Get-ResultProperty -Object $diagnostic -Name 'message') -eq 'Job completed') 'Job-password evidence did not complete the job.'
    Assert-Condition ([string](Get-ResultProperty -Object $diagnostic -Name 'route') -eq 'application/oxps -> Pdf; Convert; Convert XPS to PDF.') 'Job-password evidence used the wrong route.'
    Assert-Condition ([string](Get-ResultProperty -Object $diagnostic -Name 'detail') -like '*job-password=present-not-applicable*') 'Job-password evidence did not prove metadata consumption.'
    Assert-Condition ([string](Get-ResultProperty -Object $diagnostic -Name 'detail') -notlike '*ci-password*') 'Job-password evidence leaked the password secret.'
}

function Assert-IppWorkflowStartEvidence {
    param(
        [object] $Artifact
    )

    Assert-Condition ($null -ne $Artifact) 'IPP workflow-start evidence did not include an artifact.'
    Assert-Condition ([string](Get-ResultProperty -Object $Artifact -Name 'source') -eq 'PrintSupportWorkflowBackgroundTask') 'IPP workflow-start evidence used the wrong source.'
    Assert-Condition ([string](Get-ResultProperty -Object $Artifact -Name 'message') -eq 'Workflow job starting') 'IPP workflow-start evidence used the wrong message.'
    Assert-Condition ([string]::IsNullOrWhiteSpace([string](Get-ResultProperty -Object $Artifact -Name 'endpoint'))) 'IPP workflow-start evidence should not have a virtual-printer endpoint.'
    Assert-DetailContainsParts `
        -Detail ([string](Get-ResultProperty -Object $Artifact -Name 'detail')) `
        -ExpectedParts @('skipSystemRendering=default', 'ippCompression=') `
        -Description 'IPP workflow-start evidence'
    Assert-Condition ([string](Get-ResultProperty -Object $Artifact -Name 'detail') -notlike '*ippCompression=error*') 'IPP workflow-start evidence reported an IPP compression probe error.'
    Get-ResultTimestamp -Result $Artifact -Description 'IPP workflow-start evidence' | Out-Null
}

function Get-ResultTimestamp {
    param(
        [object] $Result,
        [string] $Description,
        [string] $Name = 'timestamp'
    )

    $timestampText = [string](Get-ResultProperty -Object $Result -Name $Name)
    $timestamp = [DateTimeOffset]::MinValue
    Assert-Condition ([DateTimeOffset]::TryParse($timestampText, [ref]$timestamp)) "$Description did not include a valid timestamp: $timestampText"

    return $timestamp
}

function Assert-ResultTimestampIsNotBefore {
    param(
        [object] $Later,
        [object] $Earlier,
        [string] $Description,
        [string] $EarlierTimestampName = 'timestamp'
    )

    $laterTimestamp = Get-ResultTimestamp -Result $Later -Description $Description
    $earlierTimestamp = Get-ResultTimestamp `
        -Result $Earlier `
        -Description "$Description lower bound" `
        -Name $EarlierTimestampName
    Assert-Condition ($laterTimestamp -ge $earlierTimestamp) "$Description was stale. Later timestamp: $laterTimestamp; lower bound: $earlierTimestamp."
}

function Assert-NonEmptyFile {
    param(
        [string] $Path,
        [long] $ExpectedBytes = -1
    )

    Assert-Condition (-not [string]::IsNullOrWhiteSpace($Path)) 'Expected a file path, but the result field was empty.'
    Assert-Condition (Test-Path -LiteralPath $Path -PathType Leaf) "Expected output file was missing: $Path"

    $file = Get-Item -LiteralPath $Path
    Assert-Condition ($file.Length -gt 0) "Expected output file was empty: $Path"
    if ($ExpectedBytes -ge 0) {
        Assert-Condition ($file.Length -eq $ExpectedBytes) "Output file byte count differed for ${Path}. Result: $ExpectedBytes; actual: $($file.Length)."
    }
}

function Assert-EmptyOrMissingFile {
    param(
        [string] $Path,
        [string] $Description
    )

    if ([string]::IsNullOrWhiteSpace($Path) -or -not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return
    }

    $file = Get-Item -LiteralPath $Path
    Assert-Condition ($file.Length -eq 0) "$Description produced non-empty output: $Path ($($file.Length) byte(s))."
}

function Assert-Document {
    param(
        [string] $Format,
        [string] $Path,
        [long] $ExpectedBytes = -1,
        [string] $Contains = '',
        [string] $NotContains = '',
        [switch] $RequiresImage
    )

    Assert-NonEmptyFile -Path $Path -ExpectedBytes $ExpectedBytes

    $arguments = @(
        'run',
        '--project',
        (Join-Path $PSScriptRoot '..\PrintSink.E2E.Assertions\PrintSink.E2E.Assertions.csproj'),
        '--configuration',
        'Debug',
        '--',
        '--format',
        $Format,
        '--path',
        $Path
    )

    if (-not [string]::IsNullOrWhiteSpace($Contains)) {
        $arguments += @('--contains', $Contains)
    }

    if (-not [string]::IsNullOrWhiteSpace($NotContains)) {
        $arguments += @('--not-contains', $NotContains)
    }

    if ($RequiresImage) {
        $arguments += @('--requires-image', 'true')
    }

    $assertionOutput = & dotnet @arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Document assertion failed for $Path. $($assertionOutput -join [Environment]::NewLine)"
    }
}

function Assert-FilesEqual {
    param(
        [string] $ExpectedPath,
        [string] $ActualPath,
        [string] $Description
    )

    Assert-NonEmptyFile -Path $ExpectedPath
    Assert-NonEmptyFile -Path $ActualPath

    $expectedBytes = [System.IO.File]::ReadAllBytes($ExpectedPath)
    $actualBytes = [System.IO.File]::ReadAllBytes($ActualPath)
    Assert-Condition ($expectedBytes.Length -eq $actualBytes.Length) "$Description byte lengths differed."

    for ($index = 0; $index -lt $expectedBytes.Length; $index++) {
        if ($expectedBytes[$index] -ne $actualBytes[$index]) {
            throw "$Description differed at byte offset $index."
        }
    }
}

function Assert-CompletedJob {
    param(
        [object] $Result,
        [string] $Queue
    )

    Assert-Condition ($null -ne $Result) "Missing E2E result for $Queue."
    $diagnostic = Get-ResultProperty -Object $Result -Name 'diagnostic'
    Assert-Condition ($null -ne $diagnostic) "Missing completion diagnostic for $Queue."
    Assert-Condition ((Get-ResultProperty -Object $diagnostic -Name 'message') -eq 'Job completed') "Expected completed diagnostic for $Queue."
    Assert-Condition (-not [string]::IsNullOrWhiteSpace([string](Get-ResultProperty -Object $diagnostic -Name 'route'))) "Missing route diagnostic for $Queue."
}

function Assert-Route {
    param(
        [object] $Result,
        [string] $ExpectedRoute,
        [string] $Description
    )

    $diagnostic = Get-ResultProperty -Object $Result -Name 'diagnostic'
    Assert-Condition ($null -ne $diagnostic) "$Description did not include a diagnostic."

    $route = [string](Get-ResultProperty -Object $diagnostic -Name 'route')
    Assert-Condition ($route -eq $ExpectedRoute) "$Description route was '$route'; expected '$ExpectedRoute'."
}

function Assert-SourceApplication {
    param(
        [object] $Result,
        [string] $ExpectedSourceApplication,
        [string] $Description
    )

    $sourceApplication = [string](Get-ResultProperty -Object $Result -Name 'sourceApplication')
    Assert-Condition ($sourceApplication -eq $ExpectedSourceApplication) "$Description reported sourceApplication '$sourceApplication'; expected '$ExpectedSourceApplication'."

    $documentName = [string](Get-ResultProperty -Object $Result -Name 'documentName')
    Assert-Condition (-not [string]::IsNullOrWhiteSpace($documentName)) "$Description did not report a documentName."
}

function Assert-InstalledQueueSnapshot {
    param(
        [object] $Snapshot
    )

    $context = [string](Get-ResultProperty -Object $Snapshot -Name 'context')
    $queues = @(Get-ResultProperty -Object $Snapshot -Name 'queues')
    foreach ($queue in $expectedQueues) {
        $entry = Get-ResultByQueue -Results $queues -Queue $queue
        Assert-Condition ($null -ne $entry) "Queue snapshot '$context' omitted $queue."
        Assert-Condition ([bool](Get-ResultProperty -Object $entry -Name 'installed')) "Queue snapshot '$context' reported $queue as not installed."
    }
}

function Assert-FeatureEvidence {
    param(
        [object[]] $FeatureEvidence,
        [object[]] $DeferredFeatureEvidence
    )

    $expectedSupportedFeatures = Get-PrintSinkSupportedFeatureMap
    $expectedDeferredFeatures = Get-PrintSinkDeferredFeatureMap
    $supportedNumbers = @($expectedSupportedFeatures.Keys | ForEach-Object { [int]$_ } | Sort-Object)
    $actualNumbers = @($FeatureEvidence | ForEach-Object { [int](Get-ResultProperty -Object $_ -Name 'number') })
    Assert-SetEqual -Actual $actualNumbers -Expected $supportedNumbers -Description 'Supported feature evidence numbers'

    foreach ($evidence in $FeatureEvidence) {
        $number = [int](Get-ResultProperty -Object $evidence -Name 'number')
        $feature = [string](Get-ResultProperty -Object $evidence -Name 'feature')
        $expectedFeature = $expectedSupportedFeatures[[string]$number]
        $passed = Get-ResultProperty -Object $evidence -Name 'passed'
        $evidenceText = [string](Get-ResultProperty -Object $evidence -Name 'evidence')
        $artifact = Get-ResultProperty -Object $evidence -Name 'artifact'
        Assert-Condition ($feature -eq $expectedFeature) "Feature evidence #$number had name '$feature'; expected '$expectedFeature'."
        Assert-Condition ([bool]$passed) "Feature evidence #$number was not marked as passed."
        Assert-Condition (-not [string]::IsNullOrWhiteSpace($evidenceText)) "Feature evidence #$number had no evidence description."
        Assert-Condition ($null -ne $artifact) "Feature evidence #$number had no artifact."
        if ($artifact -is [System.Array]) {
            Assert-Condition ($artifact.Length -gt 0) "Feature evidence #$number had an empty artifact."
        }

        if ($number -eq 3) {
            Assert-PreferredInputFormatEvidence -Artifact $artifact
        }

        if ($number -eq 4) {
            Assert-PassthroughFormatEvidence -Artifact $artifact
        }

        if ($number -eq 5) {
            Assert-FilePrinterSaveAsEvidence -Artifact $artifact
        }

        if ($number -eq 6) {
            Assert-CloudSinkEvidence -Artifact $artifact
        }

        if ($number -eq 7) {
            Assert-ConvertedOutputEvidence -Artifact $artifact
        }

        if ($number -eq 8) {
            Assert-XpsCopyEvidence -Artifact @($artifact)
        }

        if ($number -eq 9) {
            Assert-WatermarkEvidence -Artifact $artifact
        }

        if ($number -eq 10) {
            Assert-JobUiPreviewEvidence -Artifact $artifact
        }

        if ($number -eq 11) {
            Assert-SettingsUiOwner -SettingsUiOwner $artifact
        }

        if ($number -eq 12) {
            Assert-PrintTicketValidationEvidence -Artifact @($artifact)
        }

        if ($number -eq 13) {
            Assert-PdcFeatureEvidence -Artifact $artifact
        }

        if ($number -eq 14) {
            Assert-PdrFeatureEvidence -Artifact $artifact
        }

        if ($number -eq 15) {
            Assert-CapabilityRefreshEvidence -Artifact $artifact
        }

        if ($number -eq 16) {
            Assert-UserDefaultPrintTicketEvidence -Artifact $artifact
        }

        if ($number -eq 17) {
            Assert-IppAssociationEvidence -Artifact $artifact
        }

        if ($number -eq 18) {
            Assert-MxdcFeatureEvidence -Artifact $artifact
        }

        if ($number -eq 19) {
            Assert-PrinterSelectedDiagnostic -PrinterSelected $artifact -Description 'Feature evidence #19 artifact'
        }

        if ($number -eq 20) {
            Assert-VirtualPrinterAttributeReadEvidence -Artifact $artifact
        }

        if ($number -eq 21) {
            Assert-ConcurrentPrintEvidence -Artifact $artifact
        }

        if ($number -eq 23) {
            Assert-GracefulCancelAndFailEvidence -Artifact $artifact
        }

        if ($number -eq 24) {
            Assert-JobPasswordEvidence -Artifact $artifact
        }

        if ($number -eq 25) {
            Assert-LocalizedQueueNameEvidence -Artifact $artifact
        }

        if ($number -eq 27) {
            Assert-IppWorkflowStartEvidence -Artifact $artifact
        }
    }

    $deferredNumbers = @($DeferredFeatureEvidence | ForEach-Object { [int](Get-ResultProperty -Object $_ -Name 'number') })
    Assert-SetEqual `
        -Actual $deferredNumbers `
        -Expected @($expectedDeferredFeatures.Keys | ForEach-Object { [int]$_ } | Sort-Object) `
        -Description 'Deferred feature evidence numbers'

    foreach ($evidence in $DeferredFeatureEvidence) {
        $number = [int](Get-ResultProperty -Object $evidence -Name 'number')
        $feature = [string](Get-ResultProperty -Object $evidence -Name 'feature')
        $expectedFeature = $expectedDeferredFeatures[[string]$number]
        $status = [string](Get-ResultProperty -Object $evidence -Name 'status')
        $evidenceText = [string](Get-ResultProperty -Object $evidence -Name 'evidence')
        Assert-Condition ($feature -eq $expectedFeature) "Deferred feature evidence #$number had name '$feature'; expected '$expectedFeature'."
        Assert-Condition ($status -eq 'deferred') "Deferred feature evidence #$number was not marked deferred."
        Assert-Condition (-not [string]::IsNullOrWhiteSpace($evidenceText)) "Deferred feature evidence #$number had no evidence description."
    }

    $pdlPassthroughWithAttributes = @($DeferredFeatureEvidence |
        Where-Object { [int](Get-ResultProperty -Object $_ -Name 'number') -eq 28 } |
        Select-Object -First 1)[0]
    Assert-Condition ($null -ne $pdlPassthroughWithAttributes) 'Deferred feature evidence omitted row 28.'

    $artifact = Get-ResultProperty -Object $pdlPassthroughWithAttributes -Name 'artifact'
    Assert-Condition ($null -ne $artifact) 'Deferred row 28 did not include runtime artifact evidence.'

    $capabilityRefresh = Get-ResultProperty -Object $artifact -Name 'capabilityRefresh'
    $pdfPassthroughProvider = Get-ResultProperty -Object $artifact -Name 'pdfPassthroughProvider'
    $physicalWorkflowStart = Get-ResultProperty -Object $artifact -Name 'physicalWorkflowStart'
    $physicalWorkflow = Get-ResultProperty -Object $artifact -Name 'physicalWorkflow'
    $physicalWorkflowStatus = [string](Get-ResultProperty -Object $artifact -Name 'physicalWorkflowStatus')
    $physicalWorkflowDetail = [string](Get-ResultProperty -Object $artifact -Name 'physicalWorkflowDetail')
    $capabilityRefreshDetail = [string](Get-ResultProperty -Object $capabilityRefresh -Name 'detail')
    $pdfPassthroughProviderDetail = [string](Get-ResultProperty -Object $pdfPassthroughProvider -Name 'detail')
    Assert-Condition ($capabilityRefreshDetail -like '*pdlPassthroughWithJobAttributes=enabled*') 'Deferred row 28 did not enable passthrough-with-job-attributes during capability refresh.'
    Assert-Condition ($pdfPassthroughProviderDetail -like '*provider2=*') 'Deferred row 28 omitted provider-v2 availability detail.'
    Assert-Condition ($pdfPassthroughProviderDetail -like '*provider2Submit=*') 'Deferred row 28 omitted provider-v2 submission detail.'
    Assert-Condition ($pdfPassthroughProviderDetail -notlike '*projection-unavailable*') 'Deferred row 28 still reports the provider-v2 projection as unavailable.'
    Assert-Condition ($pdfPassthroughProviderDetail -notlike '*provider2=error*') 'Deferred row 28 provider-v2 probe failed.'
    if ($pdfPassthroughProviderDetail -like '*provider2=supported*') {
        Assert-Condition ($pdfPassthroughProviderDetail -like '*provider2Submit=used*') 'Deferred row 28 reported provider-v2 support but did not submit through provider-v2.'
        Assert-Condition ($pdfPassthroughProviderDetail -like '*ippAttributeSource=*') 'Deferred row 28 provider-v2 submission omitted its IPP attribute source.'
        Assert-Condition ($pdfPassthroughProviderDetail -like '*ippJobAttributeBytes=*') 'Deferred row 28 provider-v2 submission omitted encoded job-attribute bytes.'
        Assert-Condition ($pdfPassthroughProviderDetail -like '*ippOperationAttributeBytes=*') 'Deferred row 28 provider-v2 submission omitted encoded operation-attribute bytes.'
        if ($pdfPassthroughProviderDetail -like '*ippAttributeSource=core-fallback*') {
            Assert-Condition ($pdfPassthroughProviderDetail -like '*ippAttributeFallbackHResult=*') 'Deferred row 28 provider-v2 core fallback omitted the converter HRESULT.'
            Assert-Condition ($pdfPassthroughProviderDetail -like '*ippAttributeFallbackException=*') 'Deferred row 28 provider-v2 core fallback omitted the converter exception type.'
            Assert-Condition ($pdfPassthroughProviderDetail -like '*ippMappedJobAttributes=*') 'Deferred row 28 provider-v2 core fallback omitted the mapped attribute count.'
            Assert-Condition ($pdfPassthroughProviderDetail -like '*ippMappedJobAttributeNames=*') 'Deferred row 28 provider-v2 core fallback omitted the mapped attribute names.'
        }
    }
    elseif ($pdfPassthroughProviderDetail -like '*provider2=runtime-unusable*') {
        Assert-Condition ($pdfPassthroughProviderDetail -like '*provider2Submit=fallback-v1*') 'Deferred row 28 runtime-unusable provider-v2 path did not fall back to v1.'
        Assert-Condition ($pdfPassthroughProviderDetail -like '*provider2Fallback=*') 'Deferred row 28 runtime-unusable provider-v2 path omitted the fallback reason.'
        Assert-Condition ($pdfPassthroughProviderDetail -like '*provider2FallbackHResult=*' -or $pdfPassthroughProviderDetail -like '*provider2ProbeHResult=*') 'Deferred row 28 runtime-unusable provider-v2 path omitted the HRESULT.'
    }
    else {
        Assert-Condition ($pdfPassthroughProviderDetail -like '*provider2Submit=fallback-v1*') 'Deferred row 28 provider-v2 fallback was not explicit.'
    }

    Assert-Condition ((Get-ResultProperty -Object $physicalWorkflowStart -Name 'message') -eq 'Workflow job starting') 'Deferred row 28 omitted physical workflow-start evidence.'
    if ($null -ne $physicalWorkflow) {
        $physicalWorkflowDiagnosticDetail = [string](Get-ResultProperty -Object $physicalWorkflow -Name 'detail')
        Assert-Condition ($physicalWorkflowDiagnosticDetail -like '*passthroughWithAttributes=*') 'Deferred row 28 omitted workflow passthrough-with-attributes detail.'
        Assert-Condition ($physicalWorkflowDiagnosticDetail -notlike '*passthroughWithAttributes=error*') 'Deferred row 28 workflow passthrough-with-attributes probe failed.'
        Assert-Condition ($physicalWorkflowStatus -eq 'pdl-modification-delivered') 'Deferred row 28 reported physical workflow evidence without delivered status.'
    }
    else {
        Assert-Condition ($physicalWorkflowStatus -eq 'pdl-modification-not-delivered') 'Deferred row 28 omitted physical workflow non-delivery status.'
        Assert-Condition (-not [string]::IsNullOrWhiteSpace($physicalWorkflowDetail)) 'Deferred row 28 omitted physical workflow non-delivery detail.'
    }
}

function Assert-QueuePersistence {
    param(
        [object] $Result
    )

    $snapshots = @(Get-ResultProperty -Object $Result -Name 'queueSnapshots')
    Assert-Condition ($snapshots.Count -ge $requiredSnapshotContexts.Count) 'The E2E result did not include all required queue-persistence snapshots.'
    $contexts = @($snapshots | ForEach-Object { [string](Get-ResultProperty -Object $_ -Name 'context') })
    Assert-SetEqual -Actual $contexts -Expected $requiredSnapshotContexts -Description 'Queue snapshot contexts'

    foreach ($snapshot in $snapshots) {
        Assert-InstalledQueueSnapshot -Snapshot $snapshot
    }

    $persistence = Get-ResultProperty -Object $Result -Name 'queuePersistence'
    Assert-Condition ($null -ne $persistence) 'The E2E result did not include queuePersistence evidence.'
    Assert-Condition ([int](Get-ResultProperty -Object $persistence -Name 'snapshots') -eq $requiredSnapshotContexts.Count) 'Queue-persistence snapshot count did not match the required contexts.'
}

function Assert-ExtensionCapabilities {
    param(
        [object] $ExtensionCapabilities
    )

    Assert-Condition ($null -ne $ExtensionCapabilities) 'The E2E result did not include extension capability evidence.'
    Assert-Condition ([string](Get-ResultProperty -Object $ExtensionCapabilities -Name 'message') -eq 'Capabilities updated') 'Extension capability evidence did not report Capabilities updated.'
    $detail = [string](Get-ResultProperty -Object $ExtensionCapabilities -Name 'detail')
    Assert-Condition ($detail -like "*$expectedPdcFeatureDetail*") 'Extension capability evidence did not report the applied PDC feature set.'
    Assert-Condition ($detail -like "*$expectedPdcOptionDetail*") 'Extension capability evidence did not report the applied PDC option set.'
    Assert-Condition ($detail -like '*pdr=updated*') 'Extension capability evidence did not report PDR refresh.'
    Assert-Condition ($detail -like '*pdrResources=13*') 'Extension capability evidence did not report the expected PDR resource count.'
    Assert-Condition ($detail -like "*$expectedPdrResourceDetail*") 'Extension capability evidence did not report the localized PDR resource names.'
    Assert-Condition ($detail -like '*mxdc=configured*') 'Extension capability evidence did not report MXDC configuration.'
    Assert-Condition ($detail -like "*$expectedMxdcQualityDetail*") 'Extension capability evidence did not report the full MXDC quality mapping.'
}

function Assert-ManagementUi {
    param(
        [object] $ManagementUi
    )

    Assert-Condition ($null -ne $ManagementUi) 'The E2E result did not include management UI evidence.'
    Assert-Condition (-not [string]::IsNullOrWhiteSpace([string](Get-ResultProperty -Object $ManagementUi -Name 'windowTitle'))) 'Management UI evidence omitted the window title.'
    $visibleActions = @(Get-ResultProperty -Object $ManagementUi -Name 'visibleActions')
    Assert-SetEqual `
        -Actual $visibleActions `
        -Expected @('Install queues', 'Remove queues', 'Refresh queues', 'Refresh capabilities') `
        -Description 'Management UI visible actions'
    $invokedActions = @(Get-ResultProperty -Object $ManagementUi -Name 'invokedActions')
    Assert-SetEqual `
        -Actual $invokedActions `
        -Expected @('Remove queues', 'Install queues', 'Refresh queues', 'Refresh capabilities', 'Set default copies', 'Enable Job UI', 'Headless jobs') `
        -Description 'Management UI invoked actions'

    $removedQueues = @(Get-ResultProperty -Object $ManagementUi -Name 'removedQueues')
    Assert-SetEqual `
        -Actual @($removedQueues | ForEach-Object { Get-ResultProperty -Object $_ -Name 'name' }) `
        -Expected $expectedQueues `
        -Description 'Management UI removed queue names'
    foreach ($queue in $removedQueues) {
        Assert-Condition (-not [bool](Get-ResultProperty -Object $queue -Name 'installed')) "Management UI remove left queue installed: $($queue.name)"
    }

    $installedQueues = @(Get-ResultProperty -Object $ManagementUi -Name 'installedQueues')
    Assert-SetEqual `
        -Actual @($installedQueues | ForEach-Object { Get-ResultProperty -Object $_ -Name 'name' }) `
        -Expected $expectedQueues `
        -Description 'Management UI installed queue names'
    foreach ($queue in $installedQueues) {
        Assert-Condition ([bool](Get-ResultProperty -Object $queue -Name 'installed')) "Management UI install did not restore queue: $($queue.name)"
    }

    $queuesRefreshed = Get-ResultProperty -Object $ManagementUi -Name 'queuesRefreshed'
    Assert-Condition ([string](Get-ResultProperty -Object $queuesRefreshed -Name 'message') -eq 'Management UI queues refreshed') 'Management UI did not record a queue-refresh diagnostic.'
    Assert-Condition ([string](Get-ResultProperty -Object $queuesRefreshed -Name 'detail') -like '*Installed queues refreshed:*6 found.*') 'Management UI queue-refresh diagnostic did not report six queues.'

    $managementCapabilityRefresh = Get-ResultProperty -Object $ManagementUi -Name 'managementCapabilityRefresh'
    Assert-Condition ([string](Get-ResultProperty -Object $managementCapabilityRefresh -Name 'message') -eq 'Management UI capabilities refreshed') 'Management UI did not record a capability-refresh diagnostic.'
    Assert-Condition ([string](Get-ResultProperty -Object $managementCapabilityRefresh -Name 'detail') -like '*Capabilities refreshed for PrintSink - PDF*') 'Management UI capability-refresh diagnostic did not include the refreshed PDF queue.'

    $extensionCapabilityRefresh = Get-ResultProperty -Object $ManagementUi -Name 'extensionCapabilityRefresh'
    Assert-ExtensionCapabilities -ExtensionCapabilities $extensionCapabilityRefresh
    Assert-ResultTimestampIsNotBefore `
        -Later $extensionCapabilityRefresh `
        -Earlier $ManagementUi `
        -Description 'Management UI capability refresh extension diagnostic' `
        -EarlierTimestampName 'capabilityRefreshRequestedUtc'

    $defaultCopiesSet = Get-ResultProperty -Object $ManagementUi -Name 'defaultCopiesSet'
    Assert-Condition ([string](Get-ResultProperty -Object $defaultCopiesSet -Name 'message') -eq 'Management UI default copies updated') 'Management UI did not record the default-copy set diagnostic.'
    Assert-Condition ([string](Get-ResultProperty -Object $defaultCopiesSet -Name 'detail') -like '*copies=2*verifiedCopies=2*') 'Management UI default-copy set diagnostic did not verify two copies.'

    $defaultCopiesRestore = Get-ResultProperty -Object $ManagementUi -Name 'defaultCopiesRestore'
    Assert-Condition ([string](Get-ResultProperty -Object $defaultCopiesRestore -Name 'message') -eq 'Management UI default copies updated') 'Management UI did not record the default-copy restore diagnostic.'
    Assert-Condition ([string](Get-ResultProperty -Object $defaultCopiesRestore -Name 'detail') -like '*copies=1*verifiedCopies=1*') 'Management UI default-copy restore diagnostic did not verify one copy.'

    $jobUiEnabled = Get-ResultProperty -Object $ManagementUi -Name 'jobUiEnabled'
    Assert-Condition ([string](Get-ResultProperty -Object $jobUiEnabled -Name 'message') -eq 'Management UI Job UI mode updated') 'Management UI did not record the Job UI enabled diagnostic.'
    Assert-Condition ([string](Get-ResultProperty -Object $jobUiEnabled -Name 'detail') -like '*Job UI enabled.*') 'Management UI Job UI enabled diagnostic did not report enabled mode.'

    $jobUiHeadless = Get-ResultProperty -Object $ManagementUi -Name 'jobUiHeadless'
    Assert-Condition ([string](Get-ResultProperty -Object $jobUiHeadless -Name 'message') -eq 'Management UI Job UI mode updated') 'Management UI did not record the headless jobs diagnostic.'
    Assert-Condition ([string](Get-ResultProperty -Object $jobUiHeadless -Name 'detail') -like '*Headless jobs enabled.*') 'Management UI headless jobs diagnostic did not report headless mode.'
}

function Assert-SettingsUiOwner {
    param(
        [object] $SettingsUiOwner
    )

    Assert-Condition ($null -ne $SettingsUiOwner) 'The E2E result did not include settings UI owner evidence.'
    Assert-Condition ([string](Get-ResultProperty -Object $SettingsUiOwner -Name 'queue') -eq 'PrintSink - PDF') 'Settings UI owner evidence did not target the PDF queue.'
    Assert-Condition ([string](Get-ResultProperty -Object $SettingsUiOwner -Name 'mode') -eq 'settings-ui-owner') 'Settings UI owner evidence reported the wrong mode.'
    Assert-Condition ([string](Get-ResultProperty -Object $SettingsUiOwner -Name 'sourceApplication') -eq 'printsink-app.exe') 'Settings UI owner evidence reported the wrong source application.'
    Assert-Condition ([string](Get-ResultProperty -Object $SettingsUiOwner -Name 'ownerWindowTitle') -eq 'PrintSink WinRT E2E Source - Print') 'Settings UI owner evidence reported the wrong owner window.'
    Assert-Condition ([string](Get-ResultProperty -Object $SettingsUiOwner -Name 'settingsWindowTitle') -eq 'Print preferences') 'Settings UI owner evidence reported the wrong Settings window.'
    Assert-Condition ([bool](Get-ResultProperty -Object $SettingsUiOwner -Name 'ownerDisabled')) 'Settings UI owner evidence did not prove the owner was disabled while modal.'
    Assert-Condition ([bool](Get-ResultProperty -Object $SettingsUiOwner -Name 'ownerRestored')) 'Settings UI owner evidence did not prove the owner was restored after close.'
    Assert-Condition ([bool](Get-ResultProperty -Object $SettingsUiOwner -Name 'renderErrorAbsent')) 'Settings UI owner evidence did not prove the Reactor surface rendered without error.'
    Assert-Condition ([string](Get-ResultProperty -Object $SettingsUiOwner -Name 'modalStatus') -eq 'Modal to print preferences owner.') 'Settings UI owner evidence omitted the modal owner status text.'
    Assert-Condition (-not [string]::IsNullOrWhiteSpace([string](Get-ResultProperty -Object $SettingsUiOwner -Name 'packageFamilyName'))) 'Settings UI owner evidence omitted package identity.'
    Assert-PrinterSelectedDiagnostic `
        -PrinterSelected (Get-ResultProperty -Object $SettingsUiOwner -Name 'printerSelected') `
        -Description 'Settings UI owner printer selection'
}

function Assert-CleanupEvidence {
    param(
        [object] $Cleanup
    )

    if (-not $RequireCleanup) {
        return
    }

    Assert-Condition ($null -ne $Cleanup) 'The E2E result did not include cleanup evidence.'
    Assert-Condition ([bool](Get-ResultProperty -Object $Cleanup -Name 'requested')) 'Cleanup was required but not requested.'
    Assert-Condition ([bool](Get-ResultProperty -Object $Cleanup -Name 'completed')) 'Cleanup was required but did not complete.'

    $cleanupQueues = @(Get-ResultProperty -Object $Cleanup -Name 'queues')
    Assert-SetEqual `
        -Actual @($cleanupQueues | ForEach-Object { Get-ResultProperty -Object $_ -Name 'name' }) `
        -Expected $expectedQueues `
        -Description 'Cleanup queue names'

    foreach ($queue in $cleanupQueues) {
        Assert-Condition (-not [bool](Get-ResultProperty -Object $queue -Name 'installed')) "Cleanup reported $((Get-ResultProperty -Object $queue -Name 'name')) as still installed."
    }
}

function Assert-RealPrintOutputs {
    param(
        [object[]] $RealPrints
    )

    Assert-SetEqual `
        -Actual @($RealPrints | ForEach-Object { Get-ResultProperty -Object $_ -Name 'queue' }) `
        -Expected $expectedQueues `
        -Description 'Real print queues'

    $pdf = Get-ResultByQueue -Results $RealPrints -Queue 'PrintSink - PDF'
    Assert-CompletedJob -Result $pdf -Queue 'PrintSink - PDF'
    Assert-SourceApplication -Result $pdf -ExpectedSourceApplication 'powershell.exe' -Description 'PDF real print'
    Assert-Route -Result $pdf -ExpectedRoute 'application/oxps -> Pdf; Convert; Convert XPS to PDF.' -Description 'PDF real print'
    Assert-Document -Format 'pdf' -Path $pdf.outputPath -ExpectedBytes $pdf.bytes -Contains 'foo'

    $xps = Get-ResultByQueue -Results $RealPrints -Queue 'PrintSink - XPS'
    Assert-CompletedJob -Result $xps -Queue 'PrintSink - XPS'
    Assert-SourceApplication -Result $xps -ExpectedSourceApplication 'powershell.exe' -Description 'XPS real print'
    Assert-Route -Result $xps -ExpectedRoute 'application/oxps -> Oxps; Copy; Endpoint supports passthrough.' -Description 'XPS real print'
    Assert-Document -Format 'oxps' -Path $xps.outputPath -ExpectedBytes $xps.bytes -Contains 'foo'

    $postScript = Get-ResultByQueue -Results $RealPrints -Queue 'PrintSink - PostScript'
    Assert-CompletedJob -Result $postScript -Queue 'PrintSink - PostScript'
    Assert-SourceApplication -Result $postScript -ExpectedSourceApplication 'powershell.exe' -Description 'PostScript real print'
    Assert-Route -Result $postScript -ExpectedRoute 'application/postscript -> PostScript; Copy; Endpoint supports passthrough.' -Description 'PostScript real print'
    Assert-Document -Format 'postscript' -Path $postScript.outputPath -ExpectedBytes $postScript.bytes

    $pwg = Get-ResultByQueue -Results $RealPrints -Queue 'PrintSink - PWG Raster'
    Assert-CompletedJob -Result $pwg -Queue 'PrintSink - PWG Raster'
    Assert-SourceApplication -Result $pwg -ExpectedSourceApplication 'powershell.exe' -Description 'PWG Raster real print'
    Assert-Route -Result $pwg -ExpectedRoute 'application/oxps -> PwgRaster; Convert; Convert XPS to PWG Raster.' -Description 'PWG Raster real print'
    Assert-Document -Format 'pwg' -Path $pwg.outputPath -ExpectedBytes $pwg.bytes

    $pclm = Get-ResultByQueue -Results $RealPrints -Queue 'PrintSink - PCLm'
    Assert-CompletedJob -Result $pclm -Queue 'PrintSink - PCLm'
    Assert-SourceApplication -Result $pclm -ExpectedSourceApplication 'powershell.exe' -Description 'PCLm real print'
    Assert-Route -Result $pclm -ExpectedRoute 'application/oxps -> Pclm; Convert; Convert XPS to PCLm.' -Description 'PCLm real print'
    Assert-Document -Format 'pclm' -Path $pclm.outputPath -ExpectedBytes $pclm.bytes

    $cloud = Get-ResultByQueue -Results $RealPrints -Queue 'PrintSink - Cloud'
    Assert-CompletedJob -Result $cloud -Queue 'PrintSink - Cloud'
    Assert-SourceApplication -Result $cloud -ExpectedSourceApplication 'powershell.exe' -Description 'Cloud real print'
    Assert-Route -Result $cloud -ExpectedRoute 'application/oxps -> Pdf; Convert; Convert XPS to PDF.' -Description 'Cloud real print'
    Assert-Condition ([string]::IsNullOrWhiteSpace([string]$cloud.outputPath)) 'Cloud queue unexpectedly reported a Save-As output path.'
    Assert-Condition ([long]$cloud.bytes -eq 0) 'Cloud queue unexpectedly reported file-backed output bytes.'
    $cloudArtifact = Get-ResultProperty -Object $cloud -Name 'sinkArtifact'
    Assert-Condition ($null -ne $cloudArtifact) 'Cloud queue did not report a sink artifact.'
    Assert-Document -Format 'pdf' -Path $cloudArtifact.artifactCopyPath -ExpectedBytes $cloudArtifact.bytes -Contains 'foo'
}

function Assert-AdditionalOutputs {
    param(
        [object] $Result
    )

    $notepad = Get-ResultProperty -Object $Result -Name 'notepadPrint'
    Assert-CompletedJob -Result $notepad -Queue 'Notepad PDF print'
    Assert-SourceApplication -Result $notepad -ExpectedSourceApplication 'notepad.exe' -Description 'Notepad PDF print'
    Assert-Route -Result $notepad -ExpectedRoute 'application/oxps -> Pdf; Convert; Convert XPS to PDF.' -Description 'Notepad PDF print'
    Assert-Document -Format 'pdf' -Path $notepad.outputPath -ExpectedBytes $notepad.bytes -Contains 'foo'

    $concurrent = Get-ResultProperty -Object $Result -Name 'concurrentPrints'
    Assert-Condition ([bool](Get-ResultProperty -Object $concurrent -Name 'overlapped')) 'Concurrent print evidence did not report overlapping jobs.'
    $concurrentJobs = @(Get-ResultProperty -Object $concurrent -Name 'jobs')
    Assert-Condition ($concurrentJobs.Count -eq 2) 'Concurrent print evidence did not include two jobs.'
    $concurrentPclm = Get-ResultByQueue -Results $concurrentJobs -Queue 'PrintSink - PCLm'
    Assert-CompletedJob -Result $concurrentPclm -Queue 'Concurrent PCLm print'
    Assert-SourceApplication -Result $concurrentPclm -ExpectedSourceApplication 'powershell.exe' -Description 'Concurrent PCLm print'
    Assert-Route -Result $concurrentPclm -ExpectedRoute 'application/oxps -> Pclm; Convert; Convert XPS to PCLm.' -Description 'Concurrent PCLm print'
    Assert-Document -Format 'pclm' -Path $concurrentPclm.outputPath -ExpectedBytes $concurrentPclm.bytes
    $concurrentCloud = Get-ResultByQueue -Results $concurrentJobs -Queue 'PrintSink - Cloud'
    Assert-CompletedJob -Result $concurrentCloud -Queue 'Concurrent cloud print'
    Assert-SourceApplication -Result $concurrentCloud -ExpectedSourceApplication 'powershell.exe' -Description 'Concurrent cloud print'
    Assert-Route -Result $concurrentCloud -ExpectedRoute 'application/oxps -> Pdf; Convert; Convert XPS to PDF.' -Description 'Concurrent cloud print'
    Assert-Document -Format 'pdf' -Path $concurrentCloud.sinkArtifact.artifactCopyPath -ExpectedBytes $concurrentCloud.sinkArtifact.bytes -Contains 'foo concurrent cloud'

    $pdfPassthrough = Get-ResultProperty -Object $Result -Name 'pdfPassthrough'
    Assert-CompletedJob -Result $pdfPassthrough -Queue 'PDF passthrough'
    Assert-SourceApplication -Result $pdfPassthrough -ExpectedSourceApplication 'printsink-app.exe' -Description 'PDF passthrough'
    Assert-Route -Result $pdfPassthrough -ExpectedRoute 'application/pdf -> Pdf; Copy; Endpoint supports passthrough.' -Description 'PDF passthrough'
    Assert-Document -Format 'pdf' -Path $pdfPassthrough.outputPath -ExpectedBytes $pdfPassthrough.bytes -Contains 'foo'
    Assert-FilesEqual -ExpectedPath $pdfPassthrough.sourcePath -ActualPath $pdfPassthrough.outputPath -Description 'PDF passthrough output'
    $pdfPassthroughProvider = Get-ResultProperty -Object $pdfPassthrough -Name 'provider'
    Assert-Condition ($null -ne $pdfPassthroughProvider) 'PDF passthrough did not report provider evidence.'
    Assert-Condition ([string]$pdfPassthroughProvider.detail -like '*pdlPassthroughProvider=*') 'PDF passthrough provider evidence omitted provider detail.'
    Assert-Condition ([string]$pdfPassthroughProvider.detail -like '*provider2=*') 'PDF passthrough provider evidence omitted provider2 availability detail.'
    Assert-Condition ([string]$pdfPassthroughProvider.detail -like '*provider2Submit=*') 'PDF passthrough provider evidence omitted provider2 submission detail.'
    Assert-Condition ([string]$pdfPassthroughProvider.detail -notlike '*projection-unavailable*') 'PDF passthrough provider evidence still reports provider2 projection unavailable.'
    Assert-Condition ([string]$pdfPassthroughProvider.detail -notlike '*provider2=error*') 'PDF passthrough provider2 probe failed.'
    if ([string]$pdfPassthroughProvider.detail -like '*provider2Submit=used*') {
        Assert-Condition ([string]$pdfPassthroughProvider.detail -like '*ippAttributeSource=*') 'PDF passthrough provider-v2 submission omitted its IPP attribute source.'
        Assert-Condition ([string]$pdfPassthroughProvider.detail -like '*ippJobAttributeBytes=*') 'PDF passthrough provider-v2 submission omitted encoded job-attribute bytes.'
        Assert-Condition ([string]$pdfPassthroughProvider.detail -like '*ippOperationAttributeBytes=*') 'PDF passthrough provider-v2 submission omitted encoded operation-attribute bytes.'
    }

    $winRtSource = Get-ResultProperty -Object $Result -Name 'winRtSource'
    Assert-CompletedJob -Result $winRtSource -Queue 'WinRT source print'
    Assert-SourceApplication -Result $winRtSource -ExpectedSourceApplication 'printsink-app.exe' -Description 'WinRT source print'
    Assert-Route -Result $winRtSource -ExpectedRoute 'application/oxps -> Pdf; Convert; Convert XPS to PDF.' -Description 'WinRT source print'
    Assert-Document -Format 'pdf' -Path $winRtSource.outputPath -ExpectedBytes $winRtSource.bytes -Contains 'foo winrt source e2e'

    $settingsWatermark = Get-ResultProperty -Object $Result -Name 'settingsWatermark'
    Assert-CompletedJob -Result $settingsWatermark -Queue 'Settings text watermark print'
    Assert-SourceApplication -Result $settingsWatermark -ExpectedSourceApplication 'powershell.exe' -Description 'Settings text watermark print'
    Assert-Route -Result $settingsWatermark -ExpectedRoute 'application/oxps -> Pdf; Convert; Convert XPS to PDF.' -Description 'Settings text watermark print'
    Assert-Document -Format 'pdf' -Path $settingsWatermark.outputPath -ExpectedBytes $settingsWatermark.bytes -Contains 'CI DEFAULT WATERMARK'

    $settingsImageWatermark = Get-ResultProperty -Object $Result -Name 'settingsImageWatermark'
    Assert-CompletedJob -Result $settingsImageWatermark -Queue 'Settings image watermark print'
    Assert-SourceApplication -Result $settingsImageWatermark -ExpectedSourceApplication 'powershell.exe' -Description 'Settings image watermark print'
    Assert-Route -Result $settingsImageWatermark -ExpectedRoute 'application/oxps -> Pdf; Convert; Convert XPS to PDF.' -Description 'Settings image watermark print'
    Assert-Document -Format 'pdf' -Path $settingsImageWatermark.outputPath -ExpectedBytes $settingsImageWatermark.bytes -Contains 'foo' -RequiresImage

    $failedImageWatermark = Get-ResultProperty -Object $Result -Name 'failedImageWatermark'
    Assert-SourceApplication -Result $failedImageWatermark -ExpectedSourceApplication 'powershell.exe' -Description 'Failed image watermark print'
    Assert-Route -Result $failedImageWatermark -ExpectedRoute 'application/oxps -> Pdf; Convert; Convert XPS to PDF.' -Description 'Failed image watermark print'
    Assert-Condition ($failedImageWatermark.diagnostic.message -eq 'Job failed') 'Failed image watermark evidence did not report Job failed.'
    Assert-EmptyOrMissingFile -Path $failedImageWatermark.outputPath -Description 'Failed image watermark job'

    $jobUiWatermark = Get-ResultProperty -Object $Result -Name 'jobUiWatermark'
    Assert-CompletedJob -Result $jobUiWatermark -Queue 'Job UI watermark print'
    Assert-SourceApplication -Result $jobUiWatermark -ExpectedSourceApplication 'powershell.exe' -Description 'Job UI watermark print'
    Assert-Route -Result $jobUiWatermark -ExpectedRoute 'application/oxps -> Pdf; Convert; Convert XPS to PDF.' -Description 'Job UI watermark print'
    Assert-Document -Format 'pdf' -Path $jobUiWatermark.outputPath -ExpectedBytes $jobUiWatermark.bytes -Contains 'CI WATERMARK' -NotContains 'ci-password'
    Assert-Condition ($jobUiWatermark.jobPassword -eq 'present-not-applicable') 'Job UI password evidence did not report present-not-applicable.'
    Assert-Condition (-not [bool]$jobUiWatermark.jobPasswordSecretExposed) 'Job UI password secret was exposed in the result.'

    $jobUiCancel = Get-ResultProperty -Object $Result -Name 'jobUiCancel'
    Assert-SourceApplication -Result $jobUiCancel -ExpectedSourceApplication 'powershell.exe' -Description 'Job UI cancel'
    Assert-Condition ($jobUiCancel.diagnostic.message -eq 'Job canceled') 'Job UI cancel evidence did not report Job canceled.'
    Assert-EmptyOrMissingFile -Path $jobUiCancel.outputPath -Description 'Job UI cancel'
}

$ResultPath = [System.IO.Path]::GetFullPath($ResultPath)
Assert-Condition (Test-Path -LiteralPath $ResultPath -PathType Leaf) "E2E result was not found: $ResultPath"

$result = Get-Content -LiteralPath $ResultPath -Raw | ConvertFrom-Json
Assert-Condition ($null -ne $result) "E2E result could not be parsed: $ResultPath"
Assert-SupportedWindowsVersion -WindowsVersion ([string]$result.windowsVersion)
Assert-PackageEvidence -Package $result.package
Assert-SetEqual -Actual @($result.queues) -Expected $expectedQueues -Description 'E2E queue list'

Assert-FeatureEvidence `
    -FeatureEvidence @($result.featureEvidence) `
    -DeferredFeatureEvidence @($result.deferredFeatureEvidence)
Assert-QueuePersistence -Result $result
Assert-ExtensionCapabilities -ExtensionCapabilities $result.extensionCapabilities
Assert-ManagementUi -ManagementUi $result.managementUi
Assert-SettingsUiOwner -SettingsUiOwner $result.settingsUiOwner
Assert-CleanupEvidence -Cleanup $result.cleanup
Assert-RealPrintOutputs -RealPrints @($result.realPrints)
Assert-AdditionalOutputs -Result $result

Write-Host "ok: PrintSink E2E result is complete: $ResultPath"
