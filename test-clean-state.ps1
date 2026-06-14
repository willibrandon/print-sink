param(
    [switch] $Cleanup
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

function Get-PrintSinkState {
    $packages = @(Get-AppxPackageFullNamesQuietly -Name 'PrintSink*')
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
        Remove-AppxPackageQuietly -PackageFullName $package
    }
}

function Remove-AppxPackageQuietly {
    param(
        [string] $PackageFullName
    )

    $environmentVariableName = 'PRINTSINK_APPX_PACKAGE_TO_REMOVE'
    $previousPackageFullName = [Environment]::GetEnvironmentVariable($environmentVariableName, 'Process')
    $command = '$ErrorActionPreference = ''Stop''; $ProgressPreference = ''SilentlyContinue''; Remove-AppxPackage -Package $env:PRINTSINK_APPX_PACKAGE_TO_REMOVE -ErrorAction SilentlyContinue'
    $encodedCommand = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($command))

    try {
        [Environment]::SetEnvironmentVariable($environmentVariableName, $PackageFullName, 'Process')
        & powershell.exe -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand $encodedCommand *> $null
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Remove-AppxPackage failed for $PackageFullName with exit code $LASTEXITCODE."
        }
    }
    finally {
        [Environment]::SetEnvironmentVariable($environmentVariableName, $previousPackageFullName, 'Process')
    }
}

function Get-AppxPackageFullNamesQuietly {
    param(
        [string] $Name
    )

    $environmentVariableName = 'PRINTSINK_APPX_PACKAGE_NAME'
    $previousName = [Environment]::GetEnvironmentVariable($environmentVariableName, 'Process')
    $command = '$ErrorActionPreference = ''Stop''; $ProgressPreference = ''SilentlyContinue''; Get-AppxPackage -Name $env:PRINTSINK_APPX_PACKAGE_NAME -ErrorAction SilentlyContinue | ForEach-Object { $_.PackageFullName }'
    $encodedCommand = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($command))

    try {
        [Environment]::SetEnvironmentVariable($environmentVariableName, $Name, 'Process')
        $packageFullNames = @(& powershell.exe -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand $encodedCommand 2> $null)
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Get-AppxPackage failed for $Name with exit code $LASTEXITCODE."
        }

        return @(
            $packageFullNames |
                Where-Object { -not [string]::IsNullOrWhiteSpace($_) -and $_ -notlike '#< CLIXML*' -and $_ -notlike '<Objs*' }
        )
    }
    finally {
        [Environment]::SetEnvironmentVariable($environmentVariableName, $previousName, 'Process')
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
