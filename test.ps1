param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug',

    [ValidateSet('x64', 'ARM64')]
    [string] $Platform = 'x64',

    [switch] $NoBuild
)

$ErrorActionPreference = 'Stop'

$testProjects = @(
    @{
        Path = 'tests\PrintSink.Architecture.Tests\PrintSink.Architecture.Tests.csproj'
        UsePlatform = $false
    },
    @{
        Path = 'tests\PrintSink.Cli.Tests\PrintSink.Cli.Tests.csproj'
        UsePlatform = $false
    },
    @{
        Path = 'tests\PrintSink.Core.Tests\PrintSink.Core.Tests.csproj'
        UsePlatform = $false
    },
    @{
        Path = 'tests\PrintSink.E2E.Assertions.Tests\PrintSink.E2E.Assertions.Tests.csproj'
        UsePlatform = $false
    },
    @{
        Path = 'tests\PrintSink.Xps.Tests\PrintSink.Xps.Tests.csproj'
        UsePlatform = $true
    }
)

$resultsDirectory = Join-Path $PSScriptRoot "artifacts\test-results\$Platform"
New-Item -ItemType Directory -Force -Path $resultsDirectory | Out-Null

foreach ($testProject in $testProjects) {
    $projectPath = Join-Path $PSScriptRoot $testProject.Path
    $arguments = @(
        'test',
        '--project',
        $projectPath,
        '--configuration',
        $Configuration
    )

    if ($testProject.UsePlatform) {
        $arguments += "-p:Platform=$Platform"
    }

    if ($NoBuild) {
        $arguments += '--no-build'
    }

    $arguments += @(
        '--report-trx',
        '--results-directory',
        $resultsDirectory
    )

    & dotnet @arguments

    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}
