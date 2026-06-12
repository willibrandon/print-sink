param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug',

    [ValidateSet('x64', 'ARM64', 'X86')]
    [string] $Platform = 'x64',

    [switch] $NoBuild
)

$ErrorActionPreference = 'Stop'

$testProjects = @(
    'tests\PrintSink.Architecture.Tests\PrintSink.Architecture.Tests.csproj',
    'tests\PrintSink.Cli.Tests\PrintSink.Cli.Tests.csproj',
    'tests\PrintSink.Core.Tests\PrintSink.Core.Tests.csproj',
    'tests\PrintSink.Xps.Tests\PrintSink.Xps.Tests.csproj'
)

$resultsDirectory = Join-Path $PSScriptRoot "artifacts\test-results\$Platform"
New-Item -ItemType Directory -Force -Path $resultsDirectory | Out-Null

foreach ($testProject in $testProjects) {
    $projectPath = Join-Path $PSScriptRoot $testProject
    $arguments = @(
        'test',
        '--project',
        $projectPath,
        '--configuration',
        $Configuration,
        "-p:Platform=$Platform"
    )

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
