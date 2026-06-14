param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug',

    [ValidateSet('x64', 'ARM64')]
    [string] $Platform = 'x64',

    [switch] $NoBuild,

    [switch] $KeepPackage
)

$ErrorActionPreference = 'Stop'

function Find-VSTestConsole {
    if ($env:VSTEST_CONSOLE -and (Test-Path -LiteralPath $env:VSTEST_CONSOLE)) {
        return $env:VSTEST_CONSOLE
    }

    $command = Get-Command vstest.console.exe -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $installationPaths = @()
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path -LiteralPath $vswhere) {
        $installationPaths = & $vswhere -all -prerelease -products * -property installationPath
    }

    $visualStudioRoot = Join-Path $env:ProgramFiles 'Microsoft Visual Studio'
    if (Test-Path -LiteralPath $visualStudioRoot) {
        $installationPaths += Get-ChildItem -LiteralPath $visualStudioRoot -Directory |
            ForEach-Object { Get-ChildItem -LiteralPath $_.FullName -Directory } |
            ForEach-Object { $_.FullName }
    }

    foreach ($installationPath in ($installationPaths | Select-Object -Unique)) {
        $candidates = @(
            (Join-Path $installationPath 'Common7\IDE\Extensions\TestPlatform\vstest.console.exe'),
            (Join-Path $installationPath 'Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe')
        )

        foreach ($candidate in $candidates) {
            if (Test-Path -LiteralPath $candidate) {
                return $candidate
            }
        }
    }

    throw 'Could not find vstest.console.exe. Install Visual Studio Test Platform V2 CLI or set VSTEST_CONSOLE.'
}

function Remove-PackagedAppTestPackage {
    Get-Process -Name 'PrintSink.App.Tests' -ErrorAction SilentlyContinue |
        Stop-Process -Force -ErrorAction SilentlyContinue

    $packages = @(Get-AppxPackage -Name 'PrintSink.App.Tests')
    foreach ($package in $packages) {
        Write-Host "Removing test package $($package.PackageFullName)"
        Remove-AppxPackage -Package $package.PackageFullName -ErrorAction Stop
    }
}

$runtimeIdentifier = switch ($Platform) {
    'x64' { 'win-x64' }
    'ARM64' { 'win-arm64' }
}

$projectPath = Join-Path $PSScriptRoot 'tests\PrintSink.App.Tests\PrintSink.App.Tests.csproj'

if (-not $NoBuild) {
    msbuild $projectPath /t:Build /p:Configuration=$Configuration /p:Platform=$Platform /nologo /v:minimal

    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

$targetFramework = 'net10.0-windows10.0.26100.0'
$recipePath = Join-Path $PSScriptRoot "tests\PrintSink.App.Tests\bin\$Platform\$Configuration\$targetFramework\$runtimeIdentifier\PrintSink.App.Tests.build.appxrecipe"
if (-not (Test-Path -LiteralPath $recipePath)) {
    throw "Packaged app test recipe not found: $recipePath"
}

$resultsDirectory = Join-Path $PSScriptRoot "artifacts\test-results\$Platform"
New-Item -ItemType Directory -Force -Path $resultsDirectory | Out-Null

$vstest = Find-VSTestConsole
$arguments = @(
    $recipePath,
    '/Logger:trx',
    "/ResultsDirectory:$resultsDirectory"
)

$testExitCode = 0
try {
    & $vstest @arguments
    $testExitCode = $LASTEXITCODE
}
finally {
    if (-not $KeepPackage) {
        Remove-PackagedAppTestPackage
    }
}

if ($testExitCode -ne 0) {
    exit $testExitCode
}
