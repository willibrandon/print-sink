param(
    [Parameter(Mandatory)]
    [string] $ResultPath,

    [switch] $RequireCleanup
)

$ErrorActionPreference = 'Stop'

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

function Assert-Condition {
    param(
        [bool] $Condition,
        [string] $Message
    )

    if (-not $Condition) {
        throw $Message
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

    $supportedNumbers = @(1..21) + @(23, 24, 25, 27)
    $actualNumbers = @($FeatureEvidence | ForEach-Object { [int](Get-ResultProperty -Object $_ -Name 'number') })
    Assert-SetEqual -Actual $actualNumbers -Expected $supportedNumbers -Description 'Supported feature evidence numbers'

    foreach ($evidence in $FeatureEvidence) {
        $number = [int](Get-ResultProperty -Object $evidence -Name 'number')
        $artifact = Get-ResultProperty -Object $evidence -Name 'artifact'
        Assert-Condition ($null -ne $artifact) "Feature evidence #$number had no artifact."
    }

    $deferredNumbers = @($DeferredFeatureEvidence | ForEach-Object { [int](Get-ResultProperty -Object $_ -Name 'number') })
    Assert-SetEqual -Actual $deferredNumbers -Expected @(22, 26, 28) -Description 'Deferred feature evidence numbers'
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
    Assert-Document -Format 'pdf' -Path $pdf.outputPath -ExpectedBytes $pdf.bytes -Contains 'foo'

    $xps = Get-ResultByQueue -Results $RealPrints -Queue 'PrintSink - XPS'
    Assert-CompletedJob -Result $xps -Queue 'PrintSink - XPS'
    Assert-Document -Format 'oxps' -Path $xps.outputPath -ExpectedBytes $xps.bytes -Contains 'foo'

    $postScript = Get-ResultByQueue -Results $RealPrints -Queue 'PrintSink - PostScript'
    Assert-CompletedJob -Result $postScript -Queue 'PrintSink - PostScript'
    Assert-Document -Format 'postscript' -Path $postScript.outputPath -ExpectedBytes $postScript.bytes

    $pwg = Get-ResultByQueue -Results $RealPrints -Queue 'PrintSink - PWG Raster'
    Assert-CompletedJob -Result $pwg -Queue 'PrintSink - PWG Raster'
    Assert-Document -Format 'pwg' -Path $pwg.outputPath -ExpectedBytes $pwg.bytes

    $pclm = Get-ResultByQueue -Results $RealPrints -Queue 'PrintSink - PCLm'
    Assert-CompletedJob -Result $pclm -Queue 'PrintSink - PCLm'
    Assert-Document -Format 'pclm' -Path $pclm.outputPath -ExpectedBytes $pclm.bytes

    $cloud = Get-ResultByQueue -Results $RealPrints -Queue 'PrintSink - Cloud'
    Assert-CompletedJob -Result $cloud -Queue 'PrintSink - Cloud'
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
    Assert-Document -Format 'pdf' -Path $notepad.outputPath -ExpectedBytes $notepad.bytes -Contains 'foo'

    $concurrent = Get-ResultProperty -Object $Result -Name 'concurrentPrints'
    Assert-Condition ([bool](Get-ResultProperty -Object $concurrent -Name 'overlapped')) 'Concurrent print evidence did not report overlapping jobs.'
    $concurrentJobs = @(Get-ResultProperty -Object $concurrent -Name 'jobs')
    Assert-Condition ($concurrentJobs.Count -eq 2) 'Concurrent print evidence did not include two jobs.'
    $concurrentPclm = Get-ResultByQueue -Results $concurrentJobs -Queue 'PrintSink - PCLm'
    Assert-CompletedJob -Result $concurrentPclm -Queue 'Concurrent PCLm print'
    Assert-Document -Format 'pclm' -Path $concurrentPclm.outputPath -ExpectedBytes $concurrentPclm.bytes
    $concurrentCloud = Get-ResultByQueue -Results $concurrentJobs -Queue 'PrintSink - Cloud'
    Assert-CompletedJob -Result $concurrentCloud -Queue 'Concurrent cloud print'
    Assert-Document -Format 'pdf' -Path $concurrentCloud.sinkArtifact.artifactCopyPath -ExpectedBytes $concurrentCloud.sinkArtifact.bytes -Contains 'foo concurrent cloud'

    $pdfPassthrough = Get-ResultProperty -Object $Result -Name 'pdfPassthrough'
    Assert-CompletedJob -Result $pdfPassthrough -Queue 'PDF passthrough'
    Assert-Document -Format 'pdf' -Path $pdfPassthrough.outputPath -ExpectedBytes $pdfPassthrough.bytes -Contains 'foo'
    Assert-FilesEqual -ExpectedPath $pdfPassthrough.sourcePath -ActualPath $pdfPassthrough.outputPath -Description 'PDF passthrough output'
    $pdfPassthroughProvider = Get-ResultProperty -Object $pdfPassthrough -Name 'provider'
    Assert-Condition ($null -ne $pdfPassthroughProvider) 'PDF passthrough did not report provider evidence.'
    Assert-Condition ([string]$pdfPassthroughProvider.detail -like '*pdlPassthroughProvider=*') 'PDF passthrough provider evidence omitted provider detail.'
    Assert-Condition ([string]$pdfPassthroughProvider.detail -like '*provider2=*') 'PDF passthrough provider evidence omitted provider2 availability detail.'

    $winRtSource = Get-ResultProperty -Object $Result -Name 'winRtSource'
    Assert-CompletedJob -Result $winRtSource -Queue 'WinRT source print'
    Assert-Document -Format 'pdf' -Path $winRtSource.outputPath -ExpectedBytes $winRtSource.bytes -Contains 'foo winrt source e2e'

    $settingsWatermark = Get-ResultProperty -Object $Result -Name 'settingsWatermark'
    Assert-CompletedJob -Result $settingsWatermark -Queue 'Settings text watermark print'
    Assert-Document -Format 'pdf' -Path $settingsWatermark.outputPath -ExpectedBytes $settingsWatermark.bytes -Contains 'CI DEFAULT WATERMARK'

    $settingsImageWatermark = Get-ResultProperty -Object $Result -Name 'settingsImageWatermark'
    Assert-CompletedJob -Result $settingsImageWatermark -Queue 'Settings image watermark print'
    Assert-Document -Format 'pdf' -Path $settingsImageWatermark.outputPath -ExpectedBytes $settingsImageWatermark.bytes -Contains 'foo' -RequiresImage

    $failedImageWatermark = Get-ResultProperty -Object $Result -Name 'failedImageWatermark'
    Assert-Condition ($failedImageWatermark.diagnostic.message -eq 'Job failed') 'Failed image watermark evidence did not report Job failed.'
    Assert-EmptyOrMissingFile -Path $failedImageWatermark.outputPath -Description 'Failed image watermark job'

    $jobUiWatermark = Get-ResultProperty -Object $Result -Name 'jobUiWatermark'
    Assert-CompletedJob -Result $jobUiWatermark -Queue 'Job UI watermark print'
    Assert-Document -Format 'pdf' -Path $jobUiWatermark.outputPath -ExpectedBytes $jobUiWatermark.bytes -Contains 'CI WATERMARK' -NotContains 'ci-password'
    Assert-Condition ($jobUiWatermark.jobPassword -eq 'present-not-applicable') 'Job UI password evidence did not report present-not-applicable.'
    Assert-Condition (-not [bool]$jobUiWatermark.jobPasswordSecretExposed) 'Job UI password secret was exposed in the result.'

    $jobUiCancel = Get-ResultProperty -Object $Result -Name 'jobUiCancel'
    Assert-Condition ($jobUiCancel.diagnostic.message -eq 'Job canceled') 'Job UI cancel evidence did not report Job canceled.'
    Assert-EmptyOrMissingFile -Path $jobUiCancel.outputPath -Description 'Job UI cancel'
}

$ResultPath = [System.IO.Path]::GetFullPath($ResultPath)
Assert-Condition (Test-Path -LiteralPath $ResultPath -PathType Leaf) "E2E result was not found: $ResultPath"

$result = Get-Content -LiteralPath $ResultPath -Raw | ConvertFrom-Json
Assert-Condition ($null -ne $result) "E2E result could not be parsed: $ResultPath"
Assert-SetEqual -Actual @($result.queues) -Expected $expectedQueues -Description 'E2E queue list'

Assert-FeatureEvidence `
    -FeatureEvidence @($result.featureEvidence) `
    -DeferredFeatureEvidence @($result.deferredFeatureEvidence)
Assert-QueuePersistence -Result $result
Assert-CleanupEvidence -Cleanup $result.cleanup
Assert-RealPrintOutputs -RealPrints @($result.realPrints)
Assert-AdditionalOutputs -Result $result

Write-Host "ok: PrintSink E2E result is complete: $ResultPath"
