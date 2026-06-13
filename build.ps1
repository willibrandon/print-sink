param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug',

    [ValidateSet('x64', 'ARM64')]
    [string] $Platform = 'x64',

    [string] $Target = 'Build',

    [switch] $NoRestore
)

$ErrorActionPreference = 'Stop'

$arguments = @(
    '.\PrintSink.slnx',
    "/t:$Target",
    "/p:Configuration=$Configuration",
    "/p:Platform=$Platform",
    '/nologo',
    '/v:minimal'
)

if (-not $NoRestore) {
    $arguments = @('/restore') + $arguments
}

& msbuild @arguments

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
