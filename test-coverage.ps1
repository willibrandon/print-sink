param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug',

    [ValidateSet('x64', 'ARM64', 'X86')]
    [string] $Platform = 'x64',

    [ValidateRange(0.0, 1.0)]
    [double] $MinimumLineRate = 0.90
)

$ErrorActionPreference = 'Stop'

$coverageDirectory = Join-Path $PSScriptRoot 'artifacts\coverage'
$coveragePath = Join-Path $coverageDirectory "core.$Platform.cobertura.xml"
$coreTestsProject = Join-Path $PSScriptRoot 'tests\PrintSink.Core.Tests\PrintSink.Core.Tests.csproj'

New-Item -ItemType Directory -Force -Path $coverageDirectory | Out-Null

& dotnet test `
    --project $coreTestsProject `
    --configuration $Configuration `
    "-p:Platform=$Platform" `
    --coverage `
    --coverage-output $coveragePath `
    --coverage-output-format cobertura

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

[xml] $coverage = Get-Content -LiteralPath $coveragePath -Raw
$corePackage = @($coverage.coverage.packages.package | Where-Object { $_.name -eq 'PrintSink.Core' })[0]

if ($null -eq $corePackage) {
    throw "Coverage report '$coveragePath' does not contain a PrintSink.Core package."
}

$lineRate = [double]::Parse($corePackage.'line-rate', [System.Globalization.CultureInfo]::InvariantCulture)
$minimumPercent = $MinimumLineRate.ToString('P2', [System.Globalization.CultureInfo]::InvariantCulture)
$actualPercent = $lineRate.ToString('P2', [System.Globalization.CultureInfo]::InvariantCulture)

Write-Host "PrintSink.Core line coverage: $actualPercent"

if ($lineRate -lt $MinimumLineRate) {
    throw "PrintSink.Core line coverage is $actualPercent, below the required $minimumPercent."
}
