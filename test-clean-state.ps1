param(
    [switch] $Cleanup
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

function Get-PrintSinkState {
    $packages = @(Get-AppxPackage 'PrintSink*' | ForEach-Object { $_.PackageFullName })
    $queues = @(Get-Printer -Name 'PrintSink*' -ErrorAction SilentlyContinue | ForEach-Object { $_.Name })
    $processes = @(
        Get-CimInstance Win32_Process |
            Where-Object { $_.Name -like 'PrintSink*' -or $_.Name -ieq 'printsink-app.exe' } |
            ForEach-Object { "$($_.Name):$($_.ProcessId)" }
    )

    return [pscustomobject]@{
        packages = $packages
        queues = $queues
        processes = $processes
    }
}

function Test-PrintSinkStateClean {
    param(
        [pscustomobject] $State
    )

    return $State.packages.Count -eq 0 -and
        $State.queues.Count -eq 0 -and
        $State.processes.Count -eq 0
}

function Write-PrintSinkState {
    param(
        [string] $Label,
        [pscustomobject] $State
    )

    Write-Host $Label
    $State | ConvertTo-Json -Depth 4 | Write-Host
}

function Clear-PrintSinkState {
    param(
        [pscustomobject] $State
    )

    foreach ($process in $State.processes) {
        $processId = [int]($process -split ':')[-1]
        Stop-Process -Id $processId -Force -ErrorAction SilentlyContinue
    }

    foreach ($queue in $State.queues) {
        Remove-Printer -Name $queue -ErrorAction SilentlyContinue
    }

    foreach ($package in $State.packages) {
        Remove-AppxPackage -Package $package -ErrorAction SilentlyContinue
    }
}

$state = Get-PrintSinkState
if (Test-PrintSinkStateClean -State $state) {
    Write-PrintSinkState -Label 'PrintSink state is clean.' -State $state
    return
}

Write-PrintSinkState -Label 'PrintSink state is not clean.' -State $state

if ($Cleanup) {
    Clear-PrintSinkState -State $state
    $stateAfterCleanup = Get-PrintSinkState
    Write-PrintSinkState -Label 'PrintSink state after cleanup attempt.' -State $stateAfterCleanup
}

throw 'PrintSink package, queue, or process state was left behind.'
