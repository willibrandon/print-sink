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
    [pscustomobject]@{ printerUri = 'printsink:print-to-pdf'; displayName = 'ms-resource:PdfPrintDisplayName'; queue = 'PrintSink - PDF' },
    [pscustomobject]@{ printerUri = 'printsink:print-to-xps'; displayName = 'ms-resource:XpsPrintDisplayName'; queue = 'PrintSink - XPS' },
    [pscustomobject]@{ printerUri = 'printsink:print-to-ps'; displayName = 'ms-resource:PostScriptPrintDisplayName'; queue = 'PrintSink - PostScript' },
    [pscustomobject]@{ printerUri = 'printsink:print-to-cloud'; displayName = 'ms-resource:CloudPrintDisplayName'; queue = 'PrintSink - Cloud' },
    [pscustomobject]@{ printerUri = 'printsink:print-to-pwgr'; displayName = 'ms-resource:PwgRasterPrintDisplayName'; queue = 'PrintSink - PWG Raster' },
    [pscustomobject]@{ printerUri = 'printsink:print-to-pclm'; displayName = 'ms-resource:PclmPrintDisplayName'; queue = 'PrintSink - PCLm' }
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

        if ($number -eq 19) {
            Assert-PrinterSelectedDiagnostic -PrinterSelected $artifact -Description 'Feature evidence #19 artifact'
        }

        if ($number -eq 25) {
            Assert-LocalizedQueueNameEvidence -Artifact $artifact
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
        if ($pdfPassthroughProviderDetail -like '*ippAttributeSource=minimal-fallback*') {
            Assert-Condition ($pdfPassthroughProviderDetail -like '*ippAttributeFallbackHResult=*') 'Deferred row 28 provider-v2 minimal fallback omitted the converter HRESULT.'
            Assert-Condition ($pdfPassthroughProviderDetail -like '*ippAttributeFallbackException=*') 'Deferred row 28 provider-v2 minimal fallback omitted the converter exception type.'
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
